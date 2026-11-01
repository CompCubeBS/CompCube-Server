using System.Security.Claims;
using CompCube_Models.Models.ClientData;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authorization;

namespace CompCube_Server.Api.Controllers.Middleware;

public class PermissionHandler(UserData userData, ILogger<PermissionHandler> logger) : AuthorizationHandler<PermissionCheck>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionCheck requirement)
    {
        if (context.User.Identity is not { IsAuthenticated: true })
        {
            context.Fail(new AuthorizationFailureReason(this, "NOT_AUTHENTICATED"));
            return Task.CompletedTask;
        }
        
        var user = userData.GetUserInfoByPlatformId(context.User.Claims.FirstOrDefault(c => c.Type == "platformId")?.Value!);
        
        if (user == null)
        {
            context.Fail(new AuthorizationFailureReason(this, "USER_NOT_FOUND"));
            return Task.CompletedTask;
        }

        if (userData.GetUserInfoByPermission(requirement.Permission).Any(i => i.PlatformId == user.PlatformId))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        
        context.Fail(new AuthorizationFailureReason(this, "FORBIDDEN"));
        return Task.CompletedTask;
    }
}