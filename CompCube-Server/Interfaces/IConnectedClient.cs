using System.Runtime.CompilerServices;
using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Packets;
using CompCube_Models.Models.Packets.UserPackets;
using CompCube_Server.Models.CompCube_Models.Models.Packets.UserPackets;

namespace CompCube_Server.Interfaces;

public interface IConnectedClient
{
    public event Action<DiscardMapsPacket, IConnectedClient>? OnUserDiscardedMaps;
    public event Action<MapSelectionPacket, IConnectedClient>? OnMapSelection;
    public event Action<ScoreSubmissionPacket, IConnectedClient>? OnScoreSubmission;
    
    public event Action<IConnectedClient>? OnDisconnected;
    
    public event Action<ReplayRequestPacket, IConnectedClient>? OnReplayRequest;

    public bool IsConnectionAlive { get; }

    public UserStatistics UserStatistics { get; }

    public Task SendPacket(ServerPacket packet);

    public Task Disconnect();

    public Task DisconnectAbruptlyAsync(string reason);
}