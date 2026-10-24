using System.Text;
using System.Web;
using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Config;
using CompCube_Server.Data;
using CompCube_Server.Data.Schema;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CompCube_Server.Api.Controllers;

[ApiController]
public class AuthenticationApiController(
    AuthData authData, 
    BeatKhanaService beatKhanaService, 
    ILogger<AuthenticationApiController> logger,
    UserData userData,
    ConfigHelper config) : ControllerBase
{
    private static readonly Random Random = new Random();
    
    [Route("/oauth/login")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Login(string returnTo, string mode)
    {
        try
        {
            var bytes = new byte[32];

            Random.NextBytes(bytes);

            var state = WebEncoders.Base64UrlEncode(bytes);

            returnTo = returnTo.StartsWith('/') &&
                       !returnTo.StartsWith("//")
                ? returnTo
                : "/";
            
            var responseMode = mode == "json" ? AuthState.ResponseModeType.Json : AuthState.ResponseModeType.Redirect;
            
            authData.AddOAuthState(state, returnTo, responseMode);
            
            return Redirect(beatKhanaService.AuthorizationUrl(state));
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Failed to start BeatKhana Login");
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet]
    [Route("/oauth/callback")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Callback(string code, string state)
    {
        var saved = authData.ConsumeOAuthState(state);
            
        if (saved == null)
            return BadRequest("INVALID_STATE");

        try
        {
            var token = await beatKhanaService.ExchangeCode(code);
            var claims = await beatKhanaService.VerifyAccessToken(token.AccessToken);
            var account = userData.UpsertFromBeatKhanaToken(claims, config.BeatKhanaLinkingUrl);

            if (saved.ResponseMode == AuthState.ResponseModeType.Json)
                return Ok(new
                {
                    token, account
                });

            var secure = config.BeatKhanaCallbackUrl.StartsWith("https://");

            Response.Cookies.Append("cc_auth_token", token.AccessToken, new CookieOptions()
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromSeconds(token.ExpiresIn),
                Path = "/",
                Domain = config.AuthCookieDomain
            });

            Response.Cookies.Append("cc_refresh_token", token.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = secure,
                SameSite = SameSiteMode.Lax,
                MaxAge = TimeSpan.FromDays(365),
                Path = "/",
                Domain = config.AuthCookieDomain
            });

            var redirect = new UriBuilder(new Uri(new Uri(config.WebsiteUrl), saved.ReturnTo));

            if (account.Banned)
            {
                var query = HttpUtility.ParseQueryString(redirect.Query);
                query["linkRequired"] = "true";

                redirect.Query = query.ToString();
            }

            return Redirect(redirect.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create BeatKhana callback");

            if (saved.ResponseMode == AuthState.ResponseModeType.Json)
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            
            var redirect = new UriBuilder(new Uri(new Uri(config.WebsiteUrl), "/auth/error"));

            var query = HttpUtility.ParseQueryString(redirect.Query);
            query["reason"] = "beatkhana_unavailable";

            redirect.Query = query.ToString();
                
            return Redirect(redirect.ToString());
        }
    }

    [HttpGet]
    [Route("/oauth/refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
            return Unauthorized();
        
        var header = headerValues.ToString();

        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status401Unauthorized);
            
        var refreshToken = header[7..].Trim();

        try
        {
            return Ok(await beatKhanaService.Refresh(refreshToken));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh token");
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    [HttpPost]
    [Route("/oauth/logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("cc_auth_token");
        Response.Cookies.Delete("cc_refresh_token");
        return NoContent();
    }
}