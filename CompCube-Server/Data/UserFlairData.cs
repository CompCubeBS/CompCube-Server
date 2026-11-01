using CompCube_Models.Models.ClientData;
using CompCube_Server.Data.Schema;

namespace CompCube_Server.Data;

public class UserFlairData(IServiceScopeFactory scopeFactory)
{
    public Flair[] GetAllFlairs()
    {
        using var scope = scopeFactory.CreateScope();
        
        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;

        return dataContext.UserFlairs.Select(ConvertToFlairFromUserFlair).ToArray();
    }

    public Flair CreateFlair(string name, string color, string[] permissions)
    {
        using var scope = scopeFactory.CreateScope();
        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;

        var flair = new UserFlair()
        {
            Guid = Guid.NewGuid().ToString(),
            Name = name,
            Color = color,
            Permissions = permissions.ToList()
        };
        
        dataContext.UserFlairs.Add(flair);

        return ConvertToFlairFromUserFlair(flair);
    }

    public bool DeleteFlair(string guid)
    {
        using var scope = scopeFactory.CreateScope();
        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;
        
        var flair = dataContext.UserFlairs.FirstOrDefault(flair => flair.Guid == guid);
        
        if (flair == null)
            return false;
        
        dataContext.UserFlairs.Remove(flair);

        return true;
    }

    public bool UpdateFlair(string guid, string? name = null, string? color = null, string[]? permissions = null)
    {
        using var scope = scopeFactory.CreateScope();
        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;
        
        var flair = dataContext.UserFlairs.FirstOrDefault(f => f.Guid == guid);

        if (flair == null)
            return false;

        if (name != null)
            flair.Name = name;
        
        if (color != null)
            flair.Color = color;
        
        if (permissions != null)
            flair.Permissions = permissions.ToList();

        return true;
    }

    public static Flair ConvertToFlairFromUserFlair(UserFlair userFlair) => new(userFlair.Guid, userFlair.Name, userFlair.Color, userFlair.Permissions.ToArray());
}