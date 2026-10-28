using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Map;
using CompCube_Models.Models.Match;
using CompCube_Models.Models.Packets.ServerPackets;
using CompCube_Models.Models.Packets.UserPackets;
using CompCube_Server.Data;
using CompCube_Server.Gameplay.Match.Dealer;
using CompCube_Server.Gameplay.Matchmaking;
using CompCube_Server.Interfaces;
using CompCube_Server.Models.CompCube_Models.Models.Packets.UserPackets;

namespace CompCube_Server.Gameplay.Match;

public class GameMatch(ILogger<GameMatch> logger, MapData mapData, UserData userData) : IDisposable
{
    private MatchSettings _matchSettings;
    
    private MatchPlayer _red;
    private MatchPlayer _blue;

    private int _roundCount = 0;
    
    private CancellationTokenSource _playerDisconnectionCts = new();

    public float Multiplier
    {
        get
        {
            if (_roundCount <= 2)
                return 1f;

            return _roundCount * 1.5f;
        }
    }
    
    public void Init(MatchSettings matchSettings, IConnectedClient red, IConnectedClient blue)
    {
        _matchSettings = matchSettings;

        _red = new MatchPlayer(red, new DealerV0(mapData), true);
        _blue = new MatchPlayer(blue, new DealerV0(mapData), false);
    }

    public void StartMatch()
    {
        try
        {
            StartMatchAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled exception during match!");
        }
    }

    public async Task StartMatchAsync()
    {
        try
        {
            _red.ClientDidDisconnect += HandleClientDisconnected;
            _blue.ClientDidDisconnect += HandleClientDisconnected;
            
            await Task.WhenAll(
                _red.StartAndWaitForDiscardPhaseToComplete(_blue.UserStatistics),
                _blue.StartAndWaitForDiscardPhaseToComplete(_red.UserStatistics)
            );

            while (true)
            {
                await StartRound();

                if (_red.AvailablePicks.Count <= 0 && _blue.AvailablePicks.Count <= 0)
                    break;

                if (_red.Health <= 0 || _blue.Health <= 0)
                    break;
            }

            var winner = _red;

            if (_blue.Health > _red.Health)
                winner = _blue;

            var loser = GetOther(winner);

            var eloChange = ComputeEloChange(winner.UserStatistics, loser.UserStatistics);
            
            await Task.Delay(500);

            await Task.WhenAll(
                winner.SendMatchResults(eloChange, true),
                loser.SendMatchResults(eloChange, false)
            );
            
            Dispose();
        }
        catch (OperationCanceledException)
        {
            Dispose();
        }
    }

    private async void HandleClientDisconnected(MatchPlayer loser)
    {
        try
        {
            loser.ClientDidDisconnect -= HandleClientDisconnected;
            GetOther(loser).ClientDidDisconnect -= HandleClientDisconnected;

            await _playerDisconnectionCts.CancelAsync();

            logger.LogInformation("Match forfeit: Player {player} disconnected early!", loser.UserStatistics.Username);

            var winner = GetOther(loser);

            ApplyEloChanges(winner, loser, ComputeEloChange(winner.UserStatistics, loser.UserStatistics));

            await winner.DisconnectAbruptly("Player Forfeit");
            
            Dispose();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled exception during client disconnect!");
        }
    }
    
    private void ApplyEloChanges(MatchPlayer winner, MatchPlayer loser, int eloChange)
    {
        if (!_matchSettings.Competitive)
            return;
        
        userData.AdjustElo(winner.UserStatistics.PlatformId, eloChange);
        userData.AdjustElo(loser.UserStatistics.PlatformId, -eloChange);
        
        userData.IncrementTotalGames(winner.UserStatistics.PlatformId);
        userData.IncrementTotalGames(loser.UserStatistics.PlatformId);
        
        userData.IncrementWins(winner.UserStatistics.PlatformId);
        userData.ResetWinstreak(winner.UserStatistics.PlatformId);
    }

    private async Task StartRound()
    {
        _roundCount++;
        
        var pickingClient = _roundCount % 2 == 1 ? _red : _blue;

        var other = GetOther(pickingClient);
        
        var pickReturns = await Task.WhenAll(
            pickingClient.StartPickPhaseForClient(true, Multiplier, _red.Health, _blue.Health),
            other.StartPickPhaseForClient(false, Multiplier, _red.Health, _blue.Health)
        );

        var map = pickReturns.First(i => i is not null);

        var scores = await Task.WhenAll(
            _red.PlayMap(map!),
            _blue.PlayMap(map!)
        );

        var redScore = scores.First(i => i.Item1.IsRed).Item2;
        var blueScore = scores.First(i => !i.Item1.IsRed).Item2;

        var timeToContinue = DateTime.UtcNow.AddSeconds(10);

        var damage = CurveDamageDifference(redScore, blueScore, map!);

        var replayRequestResults = await Task.WhenAll(
            _red.SendRoundResults(redScore, blueScore, damage),
            _blue.SendRoundResults(redScore, blueScore, damage)
        );
        
        await Task.Delay(Math.Max(0, (int)(timeToContinue - DateTime.UtcNow).TotalMilliseconds));

        if (replayRequestResults.Any(i => i.Item2))
        {
            var replayRequester = replayRequestResults.First(i => i.Item2).Item1.UserStatistics;
            
            var newScores = await Task.WhenAll(
                _red.ReplayMap(map!, replayRequester),
                _blue.ReplayMap(map!, replayRequester)
                );

            var newRedScore = newScores.First(i => i.Item1.IsRed).Item2;
            var newBlueScore = newScores.First(i => !i.Item1.IsRed).Item2;
            
            if (newRedScore.Points > redScore.Points)
                redScore = newRedScore;
            
            if (newBlueScore.Points > blueScore.Points)
                blueScore = newBlueScore;

            damage = CurveDamageDifference(redScore, blueScore, map!);
            
            await Task.WhenAll(
                _red.SendRoundResults(redScore, blueScore, damage),
                _blue.SendRoundResults(blueScore, blueScore, damage)
            );
            
            await Task.Delay(new TimeSpan(0, 0, 10));
        }

        var loser = _red;

        if (blueScore.Points < redScore.Points)
            loser = _blue;
        
        loser.Damage(CurveDamageDifference(redScore, blueScore, map!), Multiplier);
    }
    
    private static float CurveDamageDifference(Score redScore, Score blueScore, VotingMap map)
    {
        var difference = Math.Abs(((float) redScore.Points / map!.MaxScore) - ((float) blueScore.Points / map.MaxScore));
        
        var curved = map.MapCategory switch
        {
            VotingMap.Category.MidSpeed => 5.17319f * Math.Pow(difference, .579536f),
            VotingMap.Category.Accuracy => 48.34573f * Math.Pow(difference, 1.10281f),
            VotingMap.Category.Speed => 2.47073f * Math.Pow(difference, .467712f),
            VotingMap.Category.Extreme => 2.23623f * Math.Pow(difference, .505025f),
            VotingMap.Category.Tech => 2.35749f * Math.Pow(difference, .492089f),
            VotingMap.Category.Special => 2 * difference,
            _ => difference
        };

        return (float) Math.Clamp(curved, 0, 1);
    }
    
    private int ComputeEloChange(UserStatistics winner, UserStatistics loser)
    {
        if (!_matchSettings.Competitive)
            return 0;
        
        var p = (1.0 / (1.0 + Math.Pow(10, ((winner.Elo - loser.Elo) / 400.0))));

        return (int) (_matchSettings.KFactor * p);
    }
    
    private MatchPlayer GetOther(MatchPlayer player) => player.IsRed ? _blue : _red;

    public void Dispose()
    {
        _playerDisconnectionCts.Dispose();
        
        _red.Dispose();
        _blue.Dispose();
    }
}