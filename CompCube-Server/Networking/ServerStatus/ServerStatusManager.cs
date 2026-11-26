using CompCube_Models.Models.Server;

namespace CompCube_Server.Networking.ServerStatus;

public class ServerStatusManager(IConfiguration config)
{
    private ServerState.State _state = ServerState.State.Online;

    public ServerState.State State
    {
        get => _state;
        set
        {
            _state = value;
            OnStateChanged?.Invoke(value);
        }
    }
    
    public event Action<ServerState.State>? OnStateChanged;

    public CompCube_Models.Models.Server.ServerStatus GetServerStatus()
    {
        var serverSection = config.GetSection("Gameplay");
        
        var allowedModVersions = serverSection.GetSection("AllowedModVersions").Get<string[]>();
        
        if (allowedModVersions == null)
            throw new Exception("Could not parse allowed mod versions!");
        
        return new CompCube_Models.Models.Server.ServerStatus(allowedModVersions, State);
    }
}