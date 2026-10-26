using System.Security.Cryptography;
using System.Text;
using CompCube_Server.Data.Schema;

namespace CompCube_Server.Data;

public class AuthData(IServiceScope scopeService, ILogger<AuthData> logger)
{
    public void AddOAuthState(string state, string returnTo, AuthState.ResponseModeType responseMode)
    {
        try
        {
            using var scope = scopeService.ServiceProvider.CreateScope();

            var context = scope.ServiceProvider.GetService<DataContext>()!;

            var oauthState = new AuthState()
            {
                StateHash = Hash(state),
                ReturnTo = returnTo,
                ResponseMode = responseMode,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            };

            context.AuthStates.Add(oauthState);

            context.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to add oauth state {state}");
            throw;
        }
    }

    public AuthState? ConsumeOAuthState(string state)
    {
        using var scope = scopeService.ServiceProvider.CreateScope();
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var authState = context.AuthStates
            .Where(i => i.ConsumedAt == null)
            .Where(i => i.ExpiresAt < DateTime.UtcNow)
            .FirstOrDefault(i => i.StateHash == Hash(state));

        if (authState == null)
            return null;

        try
        {
            authState.ConsumedAt = DateTime.UtcNow;
            context.SaveChanges();
            
            return authState;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to consume oauth state {state}");
            throw;
        }
    }

    private static string Hash(string value)
    {
        byte[] inputBytes = Encoding.UTF8.GetBytes(value);
        byte[] hashBytes = SHA256.HashData(inputBytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}