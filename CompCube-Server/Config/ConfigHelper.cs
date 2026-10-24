namespace CompCube_Server.Config;

public class ConfigHelper(IConfiguration config)
{
    public string BeatKhanaAuthorizationUrl => config.GetSection("BeatKhana").GetValue<string>("AuthorizationUrl") ?? throw new Exception("Could not get BeatKhana Authorization URL from config!");
    
    public string BeatKhanaClientId => config.GetSection("BeatKhana").GetValue<string>("ClientId") ?? throw new Exception("Could not get BeatKhana ClientId from config!");
    
    public string BeatKhanaClientSecret => config.GetSection("BeatKhana").GetValue<string>("ClientSecret") ?? throw new Exception("Could not get BeatKhana ClientSecret from config!");
    
    public string BeatKhanaCallbackUrl => config.GetSection("BeatKhana").GetValue<string>("CallbackUrl") ?? throw new Exception("Could not get BeatKhana CallbackUrl from config!");
    
    public string[] BeatKhanaScope => config.GetSection("BeatKhana").GetSection("Scope").Get<string[]>() ?? throw new Exception("Could not get BeatKhana Scope from config!");
    
    public string BeatKhanaApiUrl => config.GetSection("BeatKhana").GetValue<string>("ApiUrl") ?? throw new Exception("Could not get BeatKhana ApiUrl from config!");

    public string BeatKhanaPublicKeyUrl => config.GetSection("BeatKhana").GetValue<string>("PublicKeyUrl") ??
                                           throw new Exception("Could not get BeatKhana PublicKeyUrl from config!");
    
    public string WebsiteUrl => config.GetSection("Website").GetValue<string>("WebsiteUrl") ?? throw new Exception("Could not get WebsiteUrl from config!");
    
    public string AuthCookieDomain => config.GetSection("BeatKhana").GetValue<string>("AuthCookieDomain") ?? throw new Exception("Could not get AuthCookieDomain from config!");
    
    public string BeatKhanaLinkingUrl => config.GetSection("BeatKhana").GetValue<string>("LinkingUrl") ?? throw new Exception("Could not get BeatKhana LinkingUrl from config!");
    public int Season => config.GetSection("Gameplay").GetValue("Season", 0);
    
    public int[] ActivePools => config.GetSection("Maps").GetSection("ActivePools").Get<int[]>() ?? throw new Exception("ActivePools not present in appsettings.json!");

    public string Secret => config.GetSection("Api").GetValue<string>("Secret")!;
    
    public bool WhitelistEnabled => config.GetSection("Whitelist").GetValue("Enabled", false);
    
    public string[] WhitelistedIds => config.GetSection("Whitelist").GetSection("AllowedIds").Get<string[]>() ?? [];
    
    public int TimeoutTime => config.GetSection("Gameplay").GetValue("TimeoutTime", 10);
}