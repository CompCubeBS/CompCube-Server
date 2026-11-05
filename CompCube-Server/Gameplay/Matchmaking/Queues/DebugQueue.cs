using CompCube_Server.Gameplay.Match;
using CompCube_Server.Interfaces;

namespace CompCube_Server.Gameplay.Matchmaking;

public class DebugQueue(ILogger<DebugQueue> logger, ClientFactory clientFactory, GameMatchFactory gameMatchFactory) : IQueue
{
    public string QueueName => "debug";

    public void AddClientToPool(IConnectedClient client)
    {
        logger.LogInformation("Started debug match with {userName}", client.UserProfile.Username);
        
        var match = gameMatchFactory.CreateNewMatch(client, clientFactory.CreateDebugClient(), new MatchSettings(false, false, 0));
        match.StartMatch();
    }
}