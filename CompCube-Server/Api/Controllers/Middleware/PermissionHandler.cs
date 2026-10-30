using CompCube_Models.Models.ClientData;
using Microsoft.AspNetCore.Authorization;

namespace CompCube_Server.Api.Controllers.Middleware;

public class PermissionHandler() : AuthorizationHandler<PermissionCheck>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionCheck requirement)
    {
        var httpContext = context.Resource as HttpContext;

        if (httpContext!.Items["user"] is not UserInfo user)
        {
            context.Fail();
            return Task.CompletedTask;
        }

        if (user.Flairs.Any(i => i.Permissions.Contains(requirement.Permission)))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
        context.Fail();
        return Task.CompletedTask;
    }
}