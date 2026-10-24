using System.Reflection.Metadata;
using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Map;
using CompCube_Models.Models.Match;
using CompCube_Models.Models.Packets;
using CompCube_Models.Models.Packets.ServerPackets;
using CompCube_Models.Models.Packets.UserPackets;
using CompCube_Server.Gameplay.Match.Dealer;
using CompCube_Server.Interfaces;
using CompCube_Server.Data;
using CompCube_Server.Gameplay.Matchmaking;
using Microsoft.Extensions.Logging.Abstractions;

namespace CompCube_Server.Gameplay.Match;

public class GameMatch(MapData mapData, ILogger<GameMatch> logger, TimeoutManager timeoutManager, UserData userData)
{
    private MatchSettings _matchSettings;

    private ClientManager _red;
    private ClientManager _blue;

    private bool _firstClientAlreadyFinishedDiscarding = false;

    private bool _lastPickWasRed = false;

    private Score? _cachedScore = null;

    private int _currentRound = 0;

    private VotingMap? _lastMapPick = null;
    
    public void Init(MatchSettings matchSettings, IConnectedClient red, IConnectedClient blue)
    {
        _matchSettings = matchSettings;
        
        _red = new ClientManager(red, new DealerV0(mapData), true, logger);
        _blue = new ClientManager(blue, new DealerV0(mapData), false, logger);
        
        _red.ClientDidDisconnect += HandleClientDisconnected;
        _blue.ClientDidDisconnect += HandleClientDisconnected;
    }

    public void StartMatch()
    {
        try
        {
            StartMatchAsync();
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    public async Task StartMatchAsync()
    {
        _red.OnClientFinishedDiscarding += HandleClientFinishedDiscarding;
        _blue.OnClientFinishedDiscarding += HandleClientFinishedDiscarding;

        await _red.StartMatchForClient(_blue.ConnectedClient.UserStatistics);
        await _blue.StartMatchForClient(_red.ConnectedClient.UserStatistics);
    }

    private async void HandleClientDisconnected(ClientManager client)
    {
        try
        {
            _red.ClientDidDisconnect -= HandleClientDisconnected;
            _blue.ClientDidDisconnect -= HandleClientDisconnected;

            var winner = GetOtherClient(client);
            var loser = client;
            
            if (_matchSettings.Competitive)
                timeoutManager.TimeoutUser(loser.ConnectedClient.UserStatistics.PlatformId);

            var eloChange = ComputeEloChange(winner.ConnectedClient.UserStatistics, loser.ConnectedClient.UserStatistics);
            
            ApplyEloChanges(winner, loser, eloChange);
            
            await EndMatchAbruptly("Player Forfeit");
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    private async void HandleClientFinishedDiscarding(ClientManager client)
    {
        try
        {
            client.OnClientFinishedDiscarding -= HandleClientFinishedDiscarding;

            if (!_firstClientAlreadyFinishedDiscarding)
            {
                _firstClientAlreadyFinishedDiscarding = true;
                return;
            }
        
            await StartPickPhase();
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    private async Task StartPickPhase()
    {
        _cachedScore = null;
        
        _currentRound++;

        var multiplierForThisRound = GetMultiplierFromRound(_currentRound);
        
        var picker = _lastPickWasRed ? _blue : _red;
        var other  = !_lastPickWasRed ? _blue : _red;
        
        picker.OnDidPickMap += HandlePickerDidPickMap;

        await picker.StartPickPhaseForClient(true, multiplierForThisRound);
        await other.StartPickPhaseForClient(false, multiplierForThisRound);
        
        _lastPickWasRed = !_lastPickWasRed;
    }

    private async void HandlePickerDidPickMap(VotingMap map, ClientManager client)
    {
        try
        {
            client.OnDidPickMap -= HandlePickerDidPickMap;

            var other = client.IsRed ? _blue : _red;

            _lastMapPick = map;
            
            client.OnClientSubmittedScore += HandleClientSubmittedScore;
            other.OnClientSubmittedScore += HandleClientSubmittedScore;

            await other.PlayMap(map);
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    private async void HandleClientSubmittedScore(Score score, ClientManager client)
    {
        try
        {
            client.OnClientSubmittedScore -= HandleClientSubmittedScore;

            if (_cachedScore == null)
            {
                _cachedScore = score;
                return;
            }
        
            var redScore = client.IsRed ? score : _cachedScore;
            var blueScore = !client.IsRed ? score : _cachedScore;
        
            var difference = Math.Abs(score.RelativeScore - _cachedScore.RelativeScore);
        
            var loser = redScore.Points > blueScore.Points ? _blue : _red;
        
            loser.Damage(CurveDamageDifference(difference, _lastMapPick.MapCategory), GetMultiplierFromRound(_currentRound));

            if (loser.Health == 0)
            {
                _red.ClientDidDisconnect -= HandleClientDisconnected;
                _blue.ClientDidDisconnect -= HandleClientDisconnected;
            }

            await _red.SendRoundResults(redScore, blueScore, _red.Health, _blue.Health);
            await _blue.SendRoundResults(redScore, blueScore, _red.Health, _blue.Health);

            await Task.Delay(500);

            if (loser.Health == 0)
            {
                var winner = loser.IsRed ? _blue : _red;

                var eloChange = ComputeEloChange(winner.ConnectedClient.UserStatistics, loser.ConnectedClient.UserStatistics);
                
                ApplyEloChanges(winner, loser, eloChange);

                await winner.EndMatchForClient(eloChange, true);
                await loser.EndMatchForClient(eloChange, false);
                return;
            }

            await StartPickPhase();
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    private void ApplyEloChanges(ClientManager winner, ClientManager loser, int eloChange)
    {
        userData.AdjustElo(winner.ConnectedClient.UserStatistics.PlatformId, eloChange);
        userData.AdjustElo(loser.ConnectedClient.UserStatistics.PlatformId, -eloChange);
        
        userData.IncrementTotalGames(winner.ConnectedClient.UserStatistics.PlatformId);
        userData.IncrementTotalGames(loser.ConnectedClient.UserStatistics.PlatformId);
        
        userData.IncrementWins(winner.ConnectedClient.UserStatistics.PlatformId);
        userData.ResetWinstreak(winner.ConnectedClient.UserStatistics.PlatformId);
    }

    private ClientManager GetOtherClient(ClientManager client)
    {
        if (client.IsRed) 
            return _blue;

        return _red;
    }

    private async Task EndMatchAbruptly(string reason)
    {
        try
        {
            await _red.DisconnectClientAbruptly(reason);
        }
        catch{}

        try
        {
            await _blue.DisconnectClientAbruptly(reason);
        }
        catch{}
    }

    private float GetMultiplierFromRound(int round)
    {
        if (round <= 2)
            return 1f;

        return round * 1.5f;
    }

    private static float CurveDamageDifference(float relativePercent, VotingMap.Category category)
    {
        var curved = category switch
        {
            VotingMap.Category.Extreme => 1.8868f * Math.Pow(relativePercent, .4587f),
            VotingMap.Category.Accuracy => 2.1513f * Math.Pow(relativePercent, 0.352641f),
            _ => relativePercent
        };

        return (float) Math.Clamp(0, curved, 1);
    }

    private int ComputeEloChange(UserStatistics winner, UserStatistics loser)
    {
        var p = (1.0 / (1.0 + Math.Pow(10, ((winner.Elo - loser.Elo) / 400.0))));

        return (int) (_matchSettings.KFactor * p);
    }
}

public class ClientManager
{
    public readonly IConnectedClient ConnectedClient;

    private readonly IDealer _dealer;
    
    private List<VotingMap> _availablePicks;
    public IReadOnlyList<VotingMap> AvailablePicks => _availablePicks;
    
    public float Health { get; private set; } = 1f;

    public readonly bool IsRed;

    public event Action<ClientManager>? OnClientFinishedDiscarding;
    
    public event Action<VotingMap, ClientManager>? OnDidPickMap;
    
    public event Action<Score, ClientManager>? OnClientSubmittedScore;
    
    public event Action<ClientManager>? ClientDidDisconnect;
    
    private readonly ILogger<GameMatch> _logger;
    
    public ClientManager(IConnectedClient client, IDealer dealer, bool isRed, ILogger<GameMatch> logger)
    {
        ConnectedClient = client;
        
        _dealer = dealer;
        
        _availablePicks = dealer.PullNewCards(5).ToList();
        
        IsRed = isRed;
        
        _logger = logger;
        
        client.OnDisconnected += HandleClientDisconnected;
    }

    private void HandleClientDisconnected(IConnectedClient client)
    {
        client.OnDisconnected -= HandleClientDisconnected;
        
        ClientDidDisconnect?.Invoke(this);
    }

    public async Task StartMatchForClient(UserStatistics opponent)
    {
        var red = IsRed ? ConnectedClient.UserStatistics : opponent;
        var blue = !IsRed ? ConnectedClient.UserStatistics : opponent;
        
        ConnectedClient.OnUserDiscardedMaps += HandleUserFinishedDiscarding;
        
        await ConnectedClient.SendPacket(new MatchCreatedPacket(red, blue, AvailablePicks.ToArray()));
    }

    public async Task SendRoundResults(Score red, Score blue, float redHealth, float blueHealth)
    {
        await ConnectedClient.SendPacket(new RoundResultsPacket(red, blue, redHealth, blueHealth));
    }

    private async void HandleUserFinishedDiscarding(DiscardMapsPacket packet, IConnectedClient client)
    {
        try
        {
            client.OnUserDiscardedMaps -= HandleUserFinishedDiscarding;
        
            _dealer.AddDiscarded(packet.Maps);

            foreach (var discardedMap in packet.Maps)
                _availablePicks.RemoveAll(i => i == discardedMap);

            _availablePicks = _dealer.CompleteDeck(_availablePicks.ToArray(), 5).ToList();
            OnClientFinishedDiscarding?.Invoke(this);

            await ConnectedClient.SendPacket(new UpdateCardsPacket(_availablePicks.ToArray()));
        }
        catch (Exception e)
        {
            _logger.LogError(e.ToString());
        }
    }

    public async Task PlayMap(VotingMap map)
    {
        ConnectedClient.OnScoreSubmission += HandleClientDidSubmitScore;
        
        await ConnectedClient.SendPacket(new PlayerSelectedMapPacket(map));
    }

    private void HandleClientDidSubmitScore(ScoreSubmissionPacket packet, IConnectedClient client)
    {
        client.OnScoreSubmission -= HandleClientDidSubmitScore;
        
        OnClientSubmittedScore?.Invoke(packet.Score, this);
    }

    public async Task StartPickPhaseForClient(bool isPicking, float multiplier)
    {
        if (isPicking)
            ConnectedClient.OnMapSelection += HandleClientSelectedMap;
        
        await ConnectedClient.SendPacket(new StartPickPhasePacket(AvailablePicks.ToArray(), isPicking, multiplier));
    }

    private void HandleClientSelectedMap(MapSelectionPacket packet, IConnectedClient client)
    {
        client.OnMapSelection -= HandleClientSelectedMap;

        _availablePicks.Remove(packet.Selection);
        
        OnDidPickMap?.Invoke(packet.Selection, this);
        
        ConnectedClient.OnScoreSubmission += HandleClientDidSubmitScore;
    }

    public void Damage(float amount, float multiplier)
    {
        Health = Math.Max(Health - amount * multiplier, 0);
    }

    public async Task EndMatchForClient(int eloChange, bool won)
    {
        await ConnectedClient.SendPacket(new MatchFinishedPacket(eloChange, won));
        await ConnectedClient.Disconnect();
    }

    public async Task DisconnectClientAbruptly(string reason) => await ConnectedClient.DisconnectAbruptlyAsync(reason);
    
}