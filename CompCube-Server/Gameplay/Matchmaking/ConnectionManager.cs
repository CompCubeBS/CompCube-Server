using System.Net.WebSockets;
using CompCube_Models.Models.Packets.ServerPackets;
using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Config;
using CompCube_Server.Data;
using CompCube_Server.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CompCube_Server.Gameplay.Matchmaking;

[ApiExplorerSettings(IgnoreApi = true)]
public partial class ConnectionManager(
    UserData userData,
    ILogger<ConnectionManager> logger,
    QueueManager queueManager,
    ClientFactory clientFactory,
    TimeoutManager timeoutManager,
    ConfigHelper config,
    BeatKhanaService beatKhanaService)
    : ControllerBase
{
    private readonly List<IConnectedClient> _connectedClients = [];

    [Route("queue/{queueName}")]
    public async Task HandleWebSocket(string queueName)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var websocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        if (!HttpContext.Request.Headers.TryGetValue("Authorization", out var tokenValues))
        {
            if (HttpContext.Response.HasStarted)
                return;
            
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var token = tokenValues.First()?["Bearer ".Length..];

        if (token == null)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var claims = await beatKhanaService.VerifyAccessToken(token);

        var user = userData.UpsertFromBeatKhanaToken(claims, config.BeatKhanaLinkingUrl);

        var userId = user.PlatformId;
        var username = user.Username;
        
        if (_connectedClients.Any(i => i.UserStatistics.PlatformId == userId))
        {
            await websocket.SendAsync(new ArraySegment<byte>(new AbruptDisconnectionPacket("You are logged in from another location!").SerializeToBytes()), WebSocketMessageType.Text, true, CancellationToken.None);
            await websocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            return;
        }
        
        var tcs = new TaskCompletionSource();

        var userInfo = userData.GetUserStatisticsByPlatformId(userId);

        if (userInfo == null)
        {
            await websocket.SendAsync(new ArraySegment<byte>(new AbruptDisconnectionPacket("UserData does not exist!").SerializeToBytes()), WebSocketMessageType.Text, true, CancellationToken.None);
            await websocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            return;
        }

        var connectedClient = clientFactory.Create(userInfo, websocket, tcs);
        
        if (userInfo.Banned)
        {
            await connectedClient.DisconnectAbruptlyAsync("You have been banned from CompCube.");
            return;
        }

        if (timeoutManager.IsUserTimedOut(userId))
        {
            await connectedClient.DisconnectAbruptlyAsync($"You have been timed out temporarily.\nTry again in {(int) timeoutManager.GetRemainingTimeoutTime(userId).TotalMinutes + 1} minute(s)");
            return;
        }

        if (config.WhitelistEnabled && !config.WhitelistedIds.Contains(userId))
        {
            await connectedClient.DisconnectAbruptlyAsync("You are not whitelisted!");
            return;
        }
        
        var queue = queueManager.GetQueueFromName(queueName);

        if (queue == null)
        {
            await connectedClient.DisconnectAbruptlyAsync("Invalid Queue!");
            return;
        }
        
        _connectedClients.Add(connectedClient);
        connectedClient.OnDisconnected += OnDisconnected;
        
        queue.AddClientToPool(connectedClient);
        
        LogUsernameUseridJoinedQueueQueue(logger, username, userId, queue);
        
        await tcs.Task;
    }

    private void OnDisconnected(IConnectedClient client)
    {
        client.OnDisconnected -= OnDisconnected;
        
        _connectedClients.Remove(client);
        LogUserinfousernameUserinfouseridDisconnected(logger, client.UserStatistics.Username, client.UserStatistics.PlatformId);
    }

    [LoggerMessage(LogLevel.Information, "{userName} ({userId}) joined queue {queue}")]
    static partial void LogUsernameUseridJoinedQueueQueue(ILogger<ConnectionManager> logger, string userName, string userId, IQueue queue);

    [LoggerMessage(LogLevel.Information, "{UserInfoUsername} ({UserInfoUserId}) disconnected")]
    static partial void LogUserinfousernameUserinfouseridDisconnected(ILogger<ConnectionManager> logger, string UserInfoUsername, string UserInfoUserId);
}