using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Map;
using CompCube_Models.Models.Match;
using CompCube_Models.Models.Packets.ServerPackets;
using CompCube_Models.Models.Packets.UserPackets;
using CompCube_Server.Interfaces;
using CompCube_Server.Models.CompCube_Models.Models.Packets.UserPackets;

namespace CompCube_Server.Gameplay.Match;

public class MatchPlayer
{
    public UserStatistics UserStatistics => _client.UserStatistics;

    public float Health { get; private set; } = 1f;

    public int RemainingReplays { get; private set; } = 1;
    
    public bool IsRed => _isRed;

    public IReadOnlyList<VotingMap> AvailablePicks => _availablePicks;

    private VotingMap[] _availablePicks;
    private readonly IConnectedClient _client;
    private readonly IDealer _dealer;
    private readonly bool _isRed;

    public event Action<MatchPlayer>? ClientDidDisconnect;

    public MatchPlayer(IConnectedClient client, IDealer dealer, bool isRed)
    {
        _client = client;
        _dealer = dealer;
        _isRed = isRed;
        _availablePicks = dealer.PullNewCards(5);
        
        _client.OnDisconnected += HandleDisconnect;
    }

    private void HandleDisconnect(IConnectedClient client)
    {
        client.OnDisconnected -= HandleDisconnect;
        
        ClientDidDisconnect?.Invoke(this);
    }

    public async Task StartAndWaitForDiscardPhaseToComplete(UserStatistics opponent)
    {
        var red = IsRed ? UserStatistics : opponent;
        var blue = !IsRed ? UserStatistics : opponent;
        
        var tcs = new TaskCompletionSource();

        _client.OnUserDiscardedMaps += HandleClientDidSubmitDiscards;
        
        await _client.SendPacket(new MatchCreatedPacket(red, blue, AvailablePicks.ToArray()));
        
        await tcs.Task;
        
        await _client.SendPacket(new UpdateCardsPacket(_availablePicks));
        return;

        void HandleClientDidSubmitDiscards(DiscardMapsPacket packet, IConnectedClient connectedClient)
        {
            connectedClient.OnUserDiscardedMaps -= HandleClientDidSubmitDiscards;

            _availablePicks = _dealer.CompleteDeck(_availablePicks.Where(i => !packet.Maps.Contains(i)).ToArray(), 5);
            
            tcs.SetResult();
        }
    }

    public async Task<VotingMap?> StartPickPhaseForClient(bool isPicking, float multiplier, float redHealth, float blueHealth)
    {
        if (!isPicking)
        {
            await _client.SendPacket(new StartPickPhasePacket(_availablePicks, false, multiplier, redHealth, blueHealth));
            return null;
        }
        
        var tcs = new TaskCompletionSource<VotingMap>();
        
        _client.OnMapSelection += HandleClientSelectedMap;
        
        await _client.SendPacket(new StartPickPhasePacket(_availablePicks, true, multiplier, redHealth, blueHealth));
        
        return await tcs.Task;

        void HandleClientSelectedMap(MapSelectionPacket packet, IConnectedClient connectedClient)
        {
            connectedClient.OnMapSelection -= HandleClientSelectedMap;
            
            if (!_availablePicks.Contains(packet.Selection))
                throw new Exception($"Client sent invalid data!");

            _availablePicks = _availablePicks.Where(i => i != packet.Selection).ToArray();
            
            tcs.SetResult(packet.Selection);
        }
    }

    public async Task<Tuple<MatchPlayer, Score>> PlayMap(VotingMap map, UserStatistics? opponentWhoRequestedReplayIfApplies = null)
    {
        var tcs = new TaskCompletionSource<Score>();
        
        _client.OnScoreSubmission += HandleScoreSubmission;

        await _client.SendPacket(opponentWhoRequestedReplayIfApplies != null ? new PlayerDidRequestReplayPacket(map, opponentWhoRequestedReplayIfApplies) : new PlayerSelectedMapPacket(map));
        
        return new Tuple<MatchPlayer, Score>(this, await tcs.Task);

        void HandleScoreSubmission(ScoreSubmissionPacket submission, IConnectedClient connectedClient)
        {
            _client.OnScoreSubmission -= HandleScoreSubmission;
            
            tcs.SetResult(submission.Score);
        }
    }

    public async Task<Tuple<MatchPlayer, bool>> SendRoundResults(Score red, Score blue, float damageAmount)
    {
        if ((red.Points > blue.Points && IsRed) || (blue.Points > red.Points && !IsRed))
            return new Tuple<MatchPlayer, bool>(this, false);
        
        var tcs = new TaskCompletionSource<bool>();
        
        _client.OnReplayRequest += HandleReplayRequest;
        
        await _client.SendPacket(new RoundResultsPacket(red, blue, damageAmount));

        return new Tuple<MatchPlayer, bool>(this, await tcs.Task);

        void HandleReplayRequest(ReplayRequestPacket packet, IConnectedClient connectedClient)
        {
            connectedClient.OnReplayRequest -= HandleReplayRequest;

            var sanitized = packet.WantsToReplay && RemainingReplays > 0;
            
            tcs.SetResult(sanitized);
        }
    }

    public async Task<Tuple<MatchPlayer, Score>> ReplayMap(VotingMap map, UserStatistics? opponentWhoRequestedReplayIfApplies = null)
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
}