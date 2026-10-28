using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Map;
using CompCube_Models.Models.Match;
using CompCube_Models.Models.Packets.ServerPackets;
using CompCube_Models.Models.Packets.UserPackets;
using CompCube_Server.Interfaces;
using CompCube_Server.Models.CompCube_Models.Models.Packets.UserPackets;

namespace CompCube_Server.Gameplay.Match;

public class MatchPlayer : IDisposable
{
    public UserStatistics UserStatistics => _client.UserStatistics;

    public float Health { get; private set; } = 1f;

    public int RemainingReplays { get; private set; } = 1;
    
    public bool IsRed { get; }

    public IReadOnlyList<VotingMap> AvailablePicks => _availablePicks;

    private VotingMap[] _availablePicks;
    private readonly IConnectedClient _client;
    private readonly IDealer _dealer;

    public event Action<MatchPlayer>? ClientDidDisconnect;

    public MatchPlayer(IConnectedClient client, IDealer dealer, bool isRed)
    {
        _client = client;
        _dealer = dealer;
        IsRed = isRed;
        _availablePicks = dealer.PullNewCards(5);
        
        _client.OnDisconnected += HandleDisconnect;
    }

    private void HandleDisconnect(IConnectedClient client)
    {
        client.OnDisconnected -= HandleDisconnect;
        
        ClientDidDisconnect?.Invoke(this);
    }
    
    private Action<DiscardMapsPacket, IConnectedClient>? _handleDiscardMapsWrapper;

    public async Task StartAndWaitForDiscardPhaseToComplete(UserStatistics opponent)
    {
        var red = IsRed ? UserStatistics : opponent;
        var blue = !IsRed ? UserStatistics : opponent;
        
        var tcs = new TaskCompletionSource();
        
        _handleDiscardMapsWrapper = HandleClientDidSubmitDiscards;
        _client.OnUserDiscardedMaps += _handleDiscardMapsWrapper;
        
        await _client.SendPacket(new MatchCreatedPacket(red, blue, AvailablePicks.ToArray()));
        
        await tcs.Task;
        
        await _client.SendPacket(new UpdateCardsPacket(_availablePicks));
        return;
        
        void HandleClientDidSubmitDiscards(DiscardMapsPacket packet, IConnectedClient connectedClient)
        {
            connectedClient.OnUserDiscardedMaps -= _handleDiscardMapsWrapper;

            _availablePicks = _dealer.CompleteDeck(_availablePicks.Where(i => !packet.Maps.Contains(i)).ToArray(), 5);
            
            tcs.SetResult();
        }
    }

    private Action<MapSelectionPacket, IConnectedClient>? _clientPickPhaseWrapper;
    
    public async Task<VotingMap?> StartPickPhaseForClient(bool isPicking, float multiplier, float redHealth, float blueHealth)
    {
        if (!isPicking)
        {
            await _client.SendPacket(new StartPickPhasePacket(_availablePicks, false, multiplier, redHealth, blueHealth));
            return null;
        }
        
        var tcs = new TaskCompletionSource<VotingMap>();
        
        _clientPickPhaseWrapper = HandleClientSelectedMap;
        
        _client.OnMapSelection += _clientPickPhaseWrapper;
        
        await _client.SendPacket(new StartPickPhasePacket(_availablePicks, true, multiplier, redHealth, blueHealth));
        
        return await tcs.Task;
        
        void HandleClientSelectedMap(MapSelectionPacket packet, IConnectedClient connectedClient)
        {
            connectedClient.OnMapSelection -= _clientPickPhaseWrapper;
            
            if (!_availablePicks.Contains(packet.Selection))
                throw new Exception($"Client sent invalid data!");

            _availablePicks = _availablePicks.Where(i => i != packet.Selection).ToArray();
            
            tcs.SetResult(packet.Selection);
        }
    }

    private Action<ScoreSubmissionPacket, IConnectedClient>? _handleScoreSubmissionWrapper;

    public async Task<Tuple<MatchPlayer, Score>> PlayMap(VotingMap map, UserStatistics? opponentWhoRequestedReplayIfApplies = null)
    {
        var tcs = new TaskCompletionSource<Score>();
        
        _handleScoreSubmissionWrapper = HandleScoreSubmission;
        _client.OnScoreSubmission += _handleScoreSubmissionWrapper;

        if (opponentWhoRequestedReplayIfApplies == null)
            await _client.SendPacket(new PlayerSelectedMapPacket(map));
        else
            await _client.SendPacket(new PlayerDidRequestReplayPacket(map, opponentWhoRequestedReplayIfApplies));
        
        return new Tuple<MatchPlayer, Score>(this, await tcs.Task);
        
        void HandleScoreSubmission(ScoreSubmissionPacket submission, IConnectedClient connectedClient)
        {
            connectedClient.OnScoreSubmission -= _handleScoreSubmissionWrapper;
        
            tcs.SetResult(submission.Score);
        }
    }
    
    private Action<ReplayRequestPacket, IConnectedClient>? _handleReplayRequestWrapper;

    public async Task<Tuple<MatchPlayer, bool>> SendRoundResults(Score red, Score blue, float damageAmount)
    {
        if ((red.Points > blue.Points && IsRed) || (blue.Points > red.Points && !IsRed))
            return new Tuple<MatchPlayer, bool>(this, false);
        
        var tcs = new TaskCompletionSource<bool>();
        
        _handleReplayRequestWrapper = HandleReplayRequest;
        
        _client.OnReplayRequest += _handleReplayRequestWrapper;
        
        await _client.SendPacket(new RoundResultsPacket(red, blue, damageAmount));

        return new Tuple<MatchPlayer, bool>(this, await tcs.Task);
        
        void HandleReplayRequest(ReplayRequestPacket packet, IConnectedClient connectedClient)
        {
            connectedClient.OnReplayRequest -= _handleReplayRequestWrapper;

            var sanitized = packet.WantsToReplay && RemainingReplays > 0;
            
            tcs.SetResult(sanitized);
        }
    }

    public async Task<Tuple<MatchPlayer, Score>> ReplayMap(VotingMap map, UserStatistics? opponentWhoRequestedReplayIfApplies)
    {
        return await PlayMap(map, opponentWhoRequestedReplayIfApplies);
    }

    public async Task SendMatchResults(int eloChange, bool won)
    {
        await _client.SendPacket(new MatchFinishedPacket(eloChange, won));
    }

    public void Damage(float amount, float multiplier)
    {
        Health = Math.Max(0, Health - (amount * multiplier));
    }

    public async Task DisconnectAbruptly(string reason) => await _client.DisconnectAbruptlyAsync(reason);
    
    public void Dispose()
    {
        _client.OnScoreSubmission -= _handleScoreSubmissionWrapper;
        _client.OnReplayRequest -= _handleReplayRequestWrapper;
        _client.OnMapSelection -= _clientPickPhaseWrapper;
        _client.OnUserDiscardedMaps -= _handleDiscardMapsWrapper;
        
        _client.Dispose();
    }
}