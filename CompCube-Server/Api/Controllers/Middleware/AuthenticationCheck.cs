using Microsoft.AspNetCore.Authorization;

namespace CompCube_Server.Api.Controllers.Middleware;

public class AuthenticationCheck(string token) : IAuthorizationRequirement
{
    public string Token { get; } = token;
}