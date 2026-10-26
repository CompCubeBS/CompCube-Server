using CompCube_Models.Models.Map;
using CompCube_Server.Config;
using Microsoft.EntityFrameworkCore;

namespace CompCube_Server.Data;

public class MapData(IServiceScopeFactory scopeFactory, ConfigHelper helper)
{
    public List<VotingMap> GetAllMapsFromActiveBatches()
    {
        using var scope = scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var pools = context.Pools.Include(p => p.Maps).Where(i => helper.ActivePools.Contains(i.Id));
        
        var maps = new List<VotingMap>();

        foreach (var pool in pools)
        {
            maps = maps.Concat(pool.Maps.Select(i => new VotingMap(i.Guid, i.Hash, i.Difficulty, i.Category, i.MaxScore, i.DurationSeconds))).ToList();
        }

        return maps;
    }
}