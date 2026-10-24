using CompCube_Models.Models.Map;
using CompCube_Server.Data.Schema;
using Microsoft.EntityFrameworkCore;

namespace CompCube_Server.Data;

public class BeatmapPoolData(IServiceScopeFactory scopeFactory, ILogger<BeatmapPoolData> logger)
{
    public void AssignBatch(string guid, int batch)
    {
        using var scope = scopeFactory.CreateScope();

        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        using var transaction = context.Database.BeginTransaction();

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
            .Select(i => new VotingMap(i.Guid, i.Hash, i.Difficulty, i.Category))
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

        using var transaction = context.Database.BeginTransaction();

        try
        {
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
            transaction.Commit();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to add beatmap {hash}");
            throw;
        }
    }
}