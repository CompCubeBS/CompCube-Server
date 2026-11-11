using System.Net.WebSockets;
using CompCube_Models.Models.ClientData;
using CompCube_Server.Data;
using CompCube_Server.Interfaces;
using CompCube_Server.Networking.Client;

namespace CompCube_Server.Gameplay.Matchmaking;

public class ClientFactory(UserData userData, IServiceProvider services)
{
    public IConnectedClient Create(UserProfile userInfo, WebSocket socket, TaskCompletionSource finishedTask, CancellationToken cancellationToken)
    {
        var client = ActivatorUtilities.CreateInstance<ConnectedClient>(services);
        
        client.Init(socket, userInfo, finishedTask, cancellationToken);
        return client;
    }

    public IConnectedClient CreateDebugClient()
    {
        var client = ActivatorUtilities.CreateInstance<DummyConnectedClient>(services);
        
        client.Init(userData.Debug);
        return client;
    }
}