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

public class GameMatch(ILogger<GameMatch> logger, BeatmapPoolData beatmapPoolData, UserData userData, TimeoutManager timeoutManager) : IDisposable
{
    private MatchSettings _matchSettings;
    
    private MatchPlayer _red;
    private MatchPlayer _blue;

    private int _roundCount = 0;

    private MatchEloSubmissionController _matchEloSubmissionController;
    
    private readonly CancellationTokenSource _cts = new();

    public float Multiplier
    {
        get
        {
            if (_roundCount <= 2)
                return 1f;

            return (_roundCount - 2) * 1.25f;
        }
    }
    
    public void Init(MatchSettings matchSettings, IConnectedClient red, IConnectedClient blue)
    {
        _matchSettings = matchSettings;

        _red = new MatchPlayer(red, new DealerV0(beatmapPoolData), true);
        _blue = new MatchPlayer(blue, new DealerV0(beatmapPoolData), false);

        _matchEloSubmissionController = new MatchEloSubmissionController(matchSettings, userData);
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
                _red.StartAndWaitForDiscardPhaseToComplete(_blue.UserProfile),
                _blue.StartAndWaitForDiscardPhaseToComplete(_red.UserProfile)
            ).WaitAsync(_cts.Token);

            while (true)
            {
                await StartRound().WaitAsync(_cts.Token);

                if (_red.AvailablePicks.Count <= 0 && _blue.AvailablePicks.Count <= 0)
                    break;

                if (_red.Health <= 0 || _blue.Health <= 0)
                    break;
            }

            var winner = _red;

            if (_blue.Health > _red.Health)
                winner = _blue;

            var loser = GetOther(winner);

            await Task.Delay(500);

            var eloChange = _matchEloSubmissionController.SubmitEloForMatch(winner, loser);

            await Task.WhenAll(
                winner.SendMatchResults(eloChange, true),
                loser.SendMatchResults(eloChange, false)
            );
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Match cancelled!");
        }
        finally
        {
            Dispose();
        }
    }

    private async void HandleClientDisconnected(MatchPlayer loser)
    {
        try
        {
            logger.LogInformation("Match forfeit: Player {player} disconnected early!", loser.UserProfile.UserName);

            var winner = GetOther(loser);

            _matchEloSubmissionController.SubmitEloForMatch(winner, loser);

            if (_matchSettings.Competitive)
                timeoutManager.TimeoutUser(loser.UserProfile.PlatformId);
            
            winner.ClientDidDisconnect -= HandleClientDisconnected;
            
            await winner.DisconnectAbruptly("Player Forfeit");
            
            await _cts.CancelAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e, "Unhandled exception during client disconnect!");
        }
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
            var replayRequester = replayRequestResults.First(i => i.Item2).Item1.UserProfile;
            
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
                _blue.SendRoundResults(redScore, blueScore, damage)
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
    
    private MatchPlayer GetOther(MatchPlayer player) => player.IsRed ? _blue : _red;

    public void Dispose()
    {
        _red.ClientDidDisconnect -= HandleClientDisconnected;
        _blue.ClientDidDisconnect -= HandleClientDisconnected;
        
        _red.Dispose();
        _blue.Dispose();
    }
    
    private class MatchEloSubmissionController(MatchSettings settings, UserData userData)
    {
        private bool _wasSubmitted = false;

        public int SubmitEloForMatch(MatchPlayer winner, MatchPlayer loser)
        {
            var eloChange = ComputeEloChange(winner.UserProfile, loser.UserProfile);
            
            if (_wasSubmitted)
                return eloChange;

            _wasSubmitted = true;
            
            ApplyEloChanges(winner, loser, eloChange);

            return eloChange;
        }
        
        private void ApplyEloChanges(MatchPlayer winner, MatchPlayer loser, int eloChange)
        {
            if (!settings.Competitive)
                return;
        
            userData.AdjustElo(winner.UserProfile.PlatformId, eloChange);
            userData.AdjustElo(loser.UserProfile.PlatformId, -eloChange);
        
            userData.IncrementTotalGames(winner.UserProfile.PlatformId);
            userData.IncrementTotalGames(loser.UserProfile.PlatformId);
        
            userData.IncrementWins(winner.UserProfile.PlatformId);
            userData.ResetWinstreak(winner.UserProfile.PlatformId);
            
            userData.UpdateUserLastPlayedTime(winner.UserProfile.PlatformId);
            userData.UpdateUserLastPlayedTime(loser.UserProfile.PlatformId);
        }
        
        private int ComputeEloChange(UserProfile winner, UserProfile loser)
        {
            if (!settings.Competitive)
                return 0;
        
            var p = (1.0 / (1.0 + Math.Pow(10, ((winner.CompetitiveStats.Elo - loser.CompetitiveStats.Elo) / 400.0))));

            return (int) (settings.KFactor * p);
        }
    }
}