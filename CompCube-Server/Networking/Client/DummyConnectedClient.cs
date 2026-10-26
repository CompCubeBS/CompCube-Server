using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Map;
using CompCube_Models.Models.Match;
using CompCube_Models.Models.Packets;
using CompCube_Models.Models.Packets.ServerPackets;
using CompCube_Models.Models.Packets.UserPackets;
using CompCube_Server.Data;
using CompCube_Server.Interfaces;

namespace CompCube_Server.Networking.Client;

public class DummyConnectedClient(MapData mapData) : IConnectedClient
{
    public event Action<DiscardMapsPacket, IConnectedClient>? OnUserDiscardedMaps;
    public event Action<MapSelectionPacket, IConnectedClient>? OnMapSelection;
    public event Action<ScoreSubmissionPacket, IConnectedClient>? OnScoreSubmission;
    public event Action<IConnectedClient>? OnDisconnected;

    public bool IsConnectionAlive => true;
    public UserStatistics UserStatistics => _userInfo ?? throw new Exception("Debug client accessed before initialization!");
    
    private UserStatistics? _userInfo;
    
    private List<VotingMap> _picks = [];

    public void Init(UserStatistics userInfo)
    {
        _userInfo = userInfo;
    }

    public async Task SendPacket(ServerPacket packet)
    {
        await Task.Delay(70);

        SendPacketInternal(packet);
    }

    private async Task SendPacketInternal(ServerPacket packet)
    {
        if (packet.PacketType == ServerPacket.ServerPacketTypes.MatchFinished)
        {
            Disconnect();
            return;
        }
        
        await Task.Delay(5000);

        var score = new Score(10000, .10f, true, 0, true);
        
        switch (packet.PacketType)
        {
            case ServerPacket.ServerPacketTypes.MatchCreated:
                _picks = ((MatchCreatedPacket)packet).InitialMaps.ToList();
                OnUserDiscardedMaps?.Invoke(new DiscardMapsPacket([]), this);
                break;
            case ServerPacket.ServerPacketTypes.PlayerSelectedMap:
                OnScoreSubmission?.Invoke(new ScoreSubmissionPacket(score), this);
                // await Task.Delay(3500);
                // Disconnect();
                break;
            case ServerPacket.ServerPacketTypes.StartPickPhase:
                var pickPhasePacket = packet as StartPickPhasePacket;
                if (pickPhasePacket!.IsOwnPick)
                {
                    await Task.Delay(15000);

                    var map = _picks.First();
                    
                    _picks.Remove(map);
                    OnMapSelection?.Invoke(new MapSelectionPacket(map), this);
                    await Task.Delay(5000);
                    OnScoreSubmission?.Invoke(new ScoreSubmissionPacket(score), this);
                }
                break;
        }
    }

    public Task Disconnect()
    {
        OnDisconnected?.Invoke(this);
        return Task.CompletedTask;
    }

    public Task DisconnectAbruptlyAsync(string reason)
    {
        Disconnect();
        return Task.CompletedTask;
    }
}