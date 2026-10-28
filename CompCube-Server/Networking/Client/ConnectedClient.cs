using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Packets;
using CompCube_Models.Models.Packets.ServerPackets;
using CompCube_Models.Models.Packets.UserPackets;
using CompCube_Server.Interfaces;
using CompCube_Server.Models.CompCube_Models.Models.Packets.UserPackets;

namespace CompCube_Server.Networking.Client;

public class ConnectedClient(ILogger<ConnectedClient> logger) : IConnectedClient, IAsyncDisposable
{
    private WebSocket _client;
    private TaskCompletionSource _socketFinishedTcs;

    public event Action<DiscardMapsPacket, IConnectedClient>? OnUserDiscardedMaps;
    public event Action<MapSelectionPacket, IConnectedClient>? OnMapSelection;
    public event Action<ScoreSubmissionPacket, IConnectedClient>? OnScoreSubmission;
    public event Action<IConnectedClient>? OnDisconnected;
    
    public event Action<ReplayRequestPacket, IConnectedClient>? OnReplayRequest;

    private UserStatistics? _userStatistics;

    public UserStatistics UserStatistics => _userStatistics ?? throw new Exception("Client accessed before initialization!");
    
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private bool _isDisconnected = false;

    public void Init(WebSocket socket, UserStatistics userInfo, TaskCompletionSource socketFinishedTcs)
    {
        _client = socket;
        _userStatistics = userInfo;
        _socketFinishedTcs = socketFinishedTcs;
        
        Task.Factory.StartNew(ListenToClient, TaskCreationOptions.LongRunning);
    }

    private async Task ListenToClient()
    {
        try
        {
            while (true)
            {
                var buffer = new byte[4096];

                var result = await _client.ReceiveAsync(new ArraySegment<byte>(buffer), _cancellationTokenSource.Token);
                Array.Resize(ref buffer, result.Count);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await Disconnect();
                    return;
                }

                var json = Encoding.UTF8.GetString(buffer);

                if (json == "")
                    continue;

                if (!UserPacket.TryDeserialize(json, out var packet))
                {
                    logger.LogError("Failed to deserialize packet from client {UserInfoUserId}", UserStatistics.PlatformId);
                    await Disconnect();
                    return;
                }

                await ProcessRecievedPacket(packet!);
            }
        }
        catch (OperationCanceledException)
        {

        }
        catch (WebSocketException)
        {
            
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    public async Task Disconnect()
    {
        if (_isDisconnected)
            return;
        _isDisconnected = true;
        
        OnDisconnected?.Invoke(this);
        
        try
        {
            await _client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
        finally
        {
            _socketFinishedTcs.SetResult();
            _client.Dispose();
        }
    }

    public async Task DisconnectAbruptlyAsync(string reason)
    {
        await SendPacket(new AbruptDisconnectionPacket(reason));
        await Disconnect();
    }

    public bool IsConnectionAlive => _client.State is WebSocketState.Open or WebSocketState.Connecting;

    private async Task ProcessRecievedPacket(UserPacket packet)
    {
        switch (packet.PacketType)
        {
            case UserPacket.UserPacketTypes.DiscardMaps:
                OnUserDiscardedMaps?.Invoke(packet as DiscardMapsPacket ?? throw new InvalidOperationException(), this);
                break;
            case UserPacket.UserPacketTypes.MapSelection:
                OnMapSelection?.Invoke(packet as MapSelectionPacket ?? throw new InvalidOperationException(), this);
                break;
            case UserPacket.UserPacketTypes.ScoreSubmission:
                OnScoreSubmission?.Invoke(packet as ScoreSubmissionPacket ?? throw new InvalidOperationException(), this);
                break;
            case UserPacket.UserPacketTypes.ClientDisconnectPacket:
                await Disconnect();
                break;
            default:
                await Disconnect();
                throw new Exception("Unknown packet type!");
        }
    }

    public async Task SendPacket(ServerPacket packet)
    {
        try
        {
            await _client.SendAsync(new ArraySegment<byte>(packet.SerializeToBytes()), WebSocketMessageType.Text, true,
                _cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (WebSocketException)
        {
            await Disconnect();
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    public async ValueTask DisposeAsync()
    {
        await Disconnect();
    }
}