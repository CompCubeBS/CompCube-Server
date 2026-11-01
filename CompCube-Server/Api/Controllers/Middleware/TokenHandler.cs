using System.Text.Encodings.Web;
using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Config;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CompCube_Server.Api.Controllers.Middleware;

public class TokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder,
    BeatKhanaService beatKhanaService,
    UserData userData,
    ConfigHelper configHelper,
    ILogger<TokenHandler> logger)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var bearerToken))
            return AuthenticateResult.Fail("NO_TOKEN");

        if (string.IsNullOrEmpty(bearerToken.ToString()) || !bearerToken.ToString().StartsWith("Bearer "))
            return AuthenticateResult.Fail("MALFORMED_TOKEN");

        try
        {
            var token = bearerToken.ToString().Substring("Bearer ".Length).Trim();

            var claims = await beatKhanaService.VerifyAccessToken(token);

            var ticket = new AuthenticationTicket(claims, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error authenticating token!");
            return AuthenticateResult.Fail("INTERNAL_SERVER_ERROR");
        }
    }
}