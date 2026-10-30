using CompCube_Models.Models.Map;
using CompCube_Server.Config;
using CompCube_Server.Data.Schema;
using Microsoft.EntityFrameworkCore;

namespace CompCube_Server.Data;

public class BeatmapPoolData(IServiceScopeFactory scopeFactory, ILogger<BeatmapPoolData> logger, ConfigHelper helper)
{
    public List<VotingMap> GetAllMapsFromActiveBatches()
    {
        using var scope = scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var pools = context.Pools.Include(p => p.Maps).Where(i => helper.ActivePools.AsEnumerable().Contains(i.Id));
        
        var maps = new List<VotingMap>();

        foreach (var pool in pools)
        {
            maps = maps.Concat(pool.Maps.Select(i => new VotingMap(i.Guid, i.Hash, i.Difficulty, i.Category, i.MaxScore, i.DurationSeconds))).ToList();
        }

        return maps;
    }

    public VotingMap? GetBeatmapByGuid(string guid)
    {
        using var scope = scopeFactory.CreateScope();

        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;
        
        var beatmap = dataContext.Beatmaps.FirstOrDefault(i => i.Guid == guid);

        if (beatmap == null)
            return null;
        
        return new VotingMap(beatmap.Guid, beatmap.Hash, beatmap.Difficulty, beatmap.Category, beatmap.MaxScore, beatmap.DurationSeconds);
    }
    
    public VotingMap[] GetQualifiedMaps()
    {
        using var scope = scopeFactory.CreateScope();

        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;
        
        return dataContext.Beatmaps
            .Include(i => i.Pools)
            .Where(i => i.Pools.Count == 0)
            .Select(i => new VotingMap(i.Guid, i.Hash, i.Difficulty, i.Category, i.MaxScore, i.DurationSeconds))
            .ToArray();
    }
    
    public void AssignBatch(string guid, int batch)
    {
        using var scope = scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetService<DataContext>()!;

        try
        {
            var map = context.Beatmaps
                .Include(p => p.Pools)
                .FirstOrDefault(i => i.Guid == guid);

            if (map == null)
                return;

            var pool = context.Pools.FirstOrDefault(i => i.Id == batch);

            if (pool == null)
                return;

            map.Pools.Add(pool);
            
            context.SaveChanges();
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to assign map {guid} to batch {batch}");
            throw;
        }
    }
    
    public List<VotingMap> GetMaps()
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        return context.Beatmaps
            .Include(i => i.Pools)
            .Where(i => i.Pools.Count == 0)
            .ToArray()
            .Select(i => new VotingMap(i.Guid, i.Hash, i.Difficulty, i.Category, i.MaxScore, i.DurationSeconds))
            .ToList();
    }
    
    public void AddMap(string hash, 
        VotingMap.DifficultyType difficulty,
        VotingMap.Category category,
        int maxScore,
        int durationSeconds,
        int[]? poolIds = null)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        poolIds ??= [];

        try
        {
            foreach (var poolId in poolIds)
            {
                if (context.Pools.Any(i => i.Id == poolId))
                    continue;

                context.Pools.Add(new Pool()
                {
                    Id = poolId,
                    Maps = []
                });
            }
            
            var pools = poolIds
                .Select(i => context.Pools.FirstOrDefault(j => j.Id == i))
                .Where(i => i != null);

            context.Beatmaps.Add(new Beatmap()
            {
                Guid = Guid.NewGuid().ToString(),
                Hash = hash,
                Difficulty = difficulty,
                Category = category,
                MaxScore = maxScore,
                DurationSeconds = durationSeconds,
                Pools = pools.ToList()!
            });

            context.SaveChanges();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to add beatmap {hash}");
            throw;
        }
    }
}