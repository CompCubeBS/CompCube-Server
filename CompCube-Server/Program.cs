using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Api.Controllers;
using CompCube_Server.Config;
using CompCube_Server.Data;
using CompCube_Server.Discord;
using CompCube_Server.Discord.MapPooling;
using CompCube_Server.Discord.MapPooling.Voting;
using CompCube_Server.Gameplay.Match;
using CompCube_Server.Gameplay.Matchmaking;
using CompCube_Server.Interfaces;
using CompCube_Server.Networking.ServerStatus;
using Microsoft.EntityFrameworkCore;
using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.Commands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Services.ComponentInteractions;

namespace CompCube_Server;

public class Program
{
    private static bool _useDiscordIntegration = false;
    
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder();

        _useDiscordIntegration = builder.Configuration.GetSection("Discord").GetValue<bool>("UseDiscordIntegration");
        
        InstallBindings(builder.Services);
        
        builder.Services.AddDbContext<DataContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"),
                npgOptions => {
                    npgOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorCodesToAdd: null);
                });
        });
        
        if (_useDiscordIntegration)
            builder.Services.AddDiscordGateway(options => options.Intents = GatewayIntents.All).AddApplicationCommands().AddComponentInteractions<ButtonInteraction, ButtonInteractionContext>().AddComponentInteractions<ModalInteraction, ModalInteractionContext>().AddGatewayHandlers(typeof(Program).Assembly);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var webSocketPort = builder.Configuration.GetSection("Gameplay").GetValue("WebsocketListeningPort", -1);

        if (webSocketPort == -1)
        {
            Console.WriteLine("No websocket port configured. Defaulting to 8008");
            webSocketPort = 8008;
        }
        
        var apiPort = builder.Configuration.GetSection("Gameplay").GetValue("ApiListeningPort", -1);

        if (apiPort == -1)
        {
            Console.WriteLine("No API port configured. Defaulting to 7198");
            apiPort = 7198;
        }

        builder.WebHost.ConfigureKestrel(options =>
        {
            options.ListenAnyIP(webSocketPort);
            options.ListenAnyIP(apiPort);
        });
        
        var host = builder.Build();
            
        host.UseSwagger();
        host.UseSwaggerUI();

        // host.UseHttpsRedirection();
        host.MapControllers();

        if (_useDiscordIntegration)
        {
            host.AddModules(typeof(Program).Assembly);
        }
        
        var connectionManager = host.Services.GetRequiredService<ConnectionManager>();
        if (_useDiscordIntegration)
            host.Services.GetRequiredService<ForumChecker>();

        host.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30),
        });
        
        host.Run();
    }
        
    private static void InstallBindings(IServiceCollection services)
    {
        services.AddSingleton<ConfigHelper>();

        services.AddSingleton<UserData>();
        services.AddSingleton<BeatmapPoolData>();
        services.AddSingleton<AuthData>();
        
        services.AddSingleton<ServerStatusManager>();
        
        services.AddSingleton<ConnectionManager>();
        services.AddSingleton<TimeoutManager>();

        services.AddSingleton<GameMatchFactory>();
        services.AddSingleton<ClientFactory>();
        
        services.AddSingleton<QueueManager>();
        
        services.AddSingleton<IQueue, DebugQueue>();
        services.AddSingleton<IQueue, StandardCasualQueue>();
        services.AddSingleton<IQueue, StandardCompetitiveQueue>();

        services.AddSingleton<BeatSaverService>();
        services.AddSingleton<BeatKhanaService>();

        if (!_useDiscordIntegration) 
            return;
        
        services.AddSingleton<DiscordConfigHelper>();
        services.AddSingleton<MapVoteHelper>();
        services.AddSingleton<ForumChecker>();
        services.AddSingleton<MapLog>();
    }
}