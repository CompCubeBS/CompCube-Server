using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Map;
using CompCube_Models.Models.Match;
using CompCube_Models.Models.Packets.ServerPackets;
using CompCube_Models.Models.Packets.UserPackets;
using CompCube_Server.Interfaces;
using CompCube_Server.Models.CompCube_Models.Models.Packets.UserPackets;

namespace CompCube_Server.Gameplay.Match;

public class MatchPlayer(IConnectedClient client, IDealer dealer, bool isRed)
{
    public UserStatistics UserStatistics => client.UserStatistics;

    public float Health { get; private set; } = 1f;

    public int RemainingReplays { get; private set; } = 1;
    
    public bool IsRed => isRed;

    public IReadOnlyList<VotingMap> AvailablePicks => _availablePicks;

    private VotingMap[] _availablePicks = dealer.PullNewCards(5);

    public async Task StartAndWaitForDiscardPhaseToComplete(UserStatistics opponent)
    {
        var red = IsRed ? UserStatistics : opponent;
        var blue = !IsRed ? UserStatistics : opponent;
        
        var tcs = new TaskCompletionSource();

        client.OnUserDiscardedMaps += HandleClientDidSubmitDiscards;
        
        await client.SendPacket(new MatchCreatedPacket(red, blue, AvailablePicks.ToArray()));
        
        await tcs.Task;
        
        await client.SendPacket(new UpdateCardsPacket(_availablePicks));
        return;

        void HandleClientDidSubmitDiscards(DiscardMapsPacket packet, IConnectedClient connectedClient)
        {
            connectedClient.OnUserDiscardedMaps -= HandleClientDidSubmitDiscards;

            _availablePicks = dealer.CompleteDeck(_availablePicks.Where(i => !packet.Maps.Contains(i)).ToArray(), 5);
            
            tcs.SetResult();
        }
    }

    public async Task<VotingMap?> StartPickPhaseForClient(bool isPicking, float multiplier)
    {
        if (!isPicking)
        {
            await client.SendPacket(new StartPickPhasePacket(_availablePicks, false, multiplier));
            return null;
        }
        
        var tcs = new TaskCompletionSource<VotingMap>();
        
        client.OnMapSelection += HandleClientSelectedMap;
        
        await client.SendPacket(new StartPickPhasePacket(_availablePicks, true, multiplier));
        
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

    public async Task<Tuple<MatchPlayer, Score>> PlayMap(VotingMap map, bool isReplay = false)
    {
        var tcs = new TaskCompletionSource<Score>();
        
        client.OnScoreSubmission += HandleScoreSubmission;

        await client.SendPacket(isReplay ? new PlayerDidRequestReplayPacket(map) : new PlayerSelectedMapPacket(map));
        
        return new Tuple<MatchPlayer, Score>(this, await tcs.Task);

        void HandleScoreSubmission(ScoreSubmissionPacket submission, IConnectedClient connectedClient)
        {
            client.OnScoreSubmission -= HandleScoreSubmission;
            
            tcs.SetResult(submission.Score);
        }
    }

    public async Task<Tuple<MatchPlayer, bool>> SendRoundResults(Score red, Score blue)
    {
        if ((red.Points > blue.Points && IsRed) || (blue.Points > red.Points && !IsRed))
            return new Tuple<MatchPlayer, bool>(this, false);
        
        var tcs = new TaskCompletionSource<bool>();
        
        client.OnReplayRequest += HandleReplayRequest;
        
        await client.SendPacket(new RoundResultsPacket(red, blue));

        return new Tuple<MatchPlayer, bool>(this, await tcs.Task);

        void HandleReplayRequest(ReplayRequestPacket packet, IConnectedClient connectedClient)
        {
            connectedClient.OnReplayRequest -= HandleReplayRequest;

            var sanitized = packet.WantsToReplay && RemainingReplays > 0;
            
            tcs.SetResult(sanitized);
        }
    }

    public async Task<Tuple<MatchPlayer, Score>> ReplayMap(VotingMap map)
    {
        return await PlayMap(map, true);
    }

    public async Task SendMatchResults(int eloChange, bool won)
    {
        await client.SendPacket(new MatchFinishedPacket(eloChange, won));
    }

    public void Damage(float amount, float multiplier)
    {
        Health = Math.Max(0, Health - (amount * multiplier));
    }
}