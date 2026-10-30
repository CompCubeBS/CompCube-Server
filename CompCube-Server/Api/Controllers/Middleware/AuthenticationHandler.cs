using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Config;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authorization;

namespace CompCube_Server.Api.Controllers.Middleware;

public class AuthenticationHandler(BeatKhanaService beatKhanaService, UserData userData, ConfigHelper configHelper, ILogger<AuthenticationHandler> logger) : AuthorizationHandler<AuthenticationCheck>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AuthenticationCheck requirement)
    {
        var httpContext = context.Resource as HttpContext;
        
        var header = httpContext!.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer "))
        {
            context.Fail();
            return;
        }

        try
        {
            var token = header.Substring("Bearer ".Length).Trim();
        
            var claims = await beatKhanaService.VerifyAccessToken(token);

            var user = userData.UpsertFromBeatKhanaToken(claims, configHelper.BeatKhanaLinkingUrl);
        
            httpContext.Items["user"] = user;
        
            context.Succeed(requirement);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to authenticate user!");
            context.Fail();
        }
    }
}