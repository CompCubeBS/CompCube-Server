using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using CompCube_Server.Config;
using CompCube_Server.Models.CompCube_Models.Models.Auth.BeatKhana;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CompCube_Server.Api.BeatSaver;

public class BeatKhanaService(ConfigHelper config, ILogger<BeatKhanaService> logger)
{
    private readonly HttpClient _httpClient = new();
    
    private RSA? _publicKey;
    
    private const int ClockToleranceSeconds = 30;
    
    public string AuthorizationUrl(string state)
    {
        var uri = new UriBuilder(config.BeatKhanaAuthorizationUrl);
        
        var query = HttpUtility.ParseQueryString(uri.Query);
        query["response_type"] = "code";
        query["client_id"] = config.BeatKhanaClientId;
        query["redirect_uri"] = config.BeatKhanaCallbackUrl;
        query["scope"] = "compcube";
        query["state"] = state;
        
        uri.Query = query.ToString();
        return uri.ToString();
    }

    public async Task<TokenResponse> ExchangeCode(string code)
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.BeatKhanaClientId}:{config.BeatKhanaClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BeatKhanaApiUrl}/oauth/token");
        request.Headers.Add("Authorization", $"Basic {basic}");
        request.Headers.Add("content-type", "application/x-www-form-urlencoded");

        var content = new Dictionary<string, string>()
        {
            { "grant_type", "authorization_code" },
            { "code", code },
            { "redirect_uri", config.BeatKhanaCallbackUrl },
        };
        
        request.Content = new FormUrlEncodedContent(content);
        
        var response = await _httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        return JsonConvert.DeserializeObject<TokenResponse>(await response.Content.ReadAsStringAsync()) ?? throw new Exception("Could not get token");
    }

    private async Task Initialize()
    {
        var response = await _httpClient.GetAsync($"{config.BeatKhanaPublicKeyUrl}");
        response.EnsureSuccessStatusCode();
        
        var responseJson = JObject.Parse(await response.Content.ReadAsStringAsync());

        _publicKey = RSA.Create();
        
        _publicKey.ImportFromPem(responseJson["publicKey"]?.Value<string>());
    }

    public async Task<TokenClaims> VerifyAccessToken(string accessToken)
    {
        if (_publicKey == null)
            await Initialize();

        var parts = accessToken.Split('.');

        if (parts.Length != 3)
            throw new Exception("Malformed BeatKhana token");

        var header = JObject.Parse(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parts[0])));

        if (!header.ContainsKey("alg") && header["alg"]?.Value<string>() != "RS256")
            throw new Exception("Unexpected JWT Algorithm");

        var dataBytes = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");

        var signatureBytes = WebEncoders.Base64UrlDecode(parts[2]);

        var validSignature =
            _publicKey?.VerifyData(dataBytes, signatureBytes, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1) ?? false;

        if (!validSignature)
            throw new Exception("Invalid JWT signature");
        
        var claims = JObject.Parse(Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(parts[1])));

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var exp = claims["exp"]?.Value<long>();
        var iat = claims["iat"]?.Value<long>();
        var nbf = claims["nbf"]?.Value<long>();

        if (exp <= now - ClockToleranceSeconds) throw new Exception("Expired JWT");
        if (iat > now + ClockToleranceSeconds) throw new Exception("Invalid JWT Issue date");
        if (nbf > now + ClockToleranceSeconds) throw new Exception("JWT not active yet");

        var scopes = AccessTokenScopes(claims);

        if (!scopes.Contains("compcube"))
            throw new Exception("Scopes does not contain required scope!");

        var discordId = claims["discordId"]?.Value<string>() ?? claims["id"]?.Value<string>();
        var guid = claims["guid"]?.Value<string>();

        var platformId = claims["platformId"]?.Value<string>() is string id && id.All(char.IsDigit) ? id : null;

        if ((guid == null || discordId == null)
            && platformId == null)
            throw new Exception("JWT has no usable identity");

        var platformIds = (claims.TryGetValue("platformIds", out JToken? idsToken) && idsToken is JArray idsArray)
            ? idsArray
                .Select(token => token.ToString())
                .Where(id => !string.IsNullOrEmpty(id) && id.All(char.IsDigit))
                .Distinct()
                .ToArray()
            : (platformId != null ? new[] { platformId } : Array.Empty<string>());

        return new TokenClaims(
            guid,
            id: discordId,
            discordId,
            claims["username"]?.Value<string>().Trim(),
            claims["avatarUrl"]?.Value<string>(),
            claims["global_name"]?.Value<string>(),
            claims["platform"]?.Value<string>(),
            platformId,
            platformIds,
            claims["tokenType"]?.Value<string>() ?? null,
            scopes,
            null,
            iat,
            exp,
            nbf);
    }
    
    //i dont care anymore who gaf
    private string[] AccessTokenScopes(JObject claims)
    {
        if (claims.TryGetValue("scopes", out JToken? scopesToken) && scopesToken is JArray scopesArray)
        {
            return scopesArray
                .Select(token => token.ToString())
                .Where(scope => !string.IsNullOrWhiteSpace(scope))
                .Select(scope => scope.Trim())
                .Distinct()
                .ToArray();
        }

        if (claims.TryGetValue("scope", out JToken? scopeToken) && scopeToken.Type == JTokenType.String)
        {
            string scopeString = scopeToken.ToString();
            
            return scopeString
                .Split([' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
                .Select(scope => scope.Trim())
                .Distinct()
                .ToArray();
        }

        throw new InvalidOperationException("JWT scopes are missing");
    }

    public async Task<TokenResponse> Refresh(string refreshToken)
    {
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.BeatKhanaClientId}:{config.BeatKhanaClientSecret}"));

        var request = new HttpRequestMessage(HttpMethod.Post, $"{config.BeatKhanaApiUrl}/oauth/token");
        request.Headers.Add("Authorization", $"Basic {basic}");
        request.Headers.Add("content-type", "application/x-www-form-urlencoded");

        var content = new Dictionary<string, string>()
        {
            { "grant_type", "authorization_code" },
            { "refresh_token", refreshToken },
            { "redirect_uri", config.BeatKhanaCallbackUrl },
        };
        
        request.Content = new FormUrlEncodedContent(content);
        
        var response = await _httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        return JsonConvert.DeserializeObject<TokenResponse>(await response.Content.ReadAsStringAsync()) ?? throw new Exception("Could not get token");
    }
}