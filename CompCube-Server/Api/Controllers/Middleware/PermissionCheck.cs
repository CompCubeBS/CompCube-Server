using Microsoft.AspNetCore.Authorization;

namespace CompCube_Server.Api.Controllers.Middleware;

public class PermissionCheck(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}