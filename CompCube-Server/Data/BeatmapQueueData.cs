using BeatSaverSharp.Models;
using CompCube_Models.Models.Map;
using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Data.Schema;

namespace CompCube_Server.Data;

public class BeatmapQueueData(
    IServiceScopeFactory scopeFactory, 
    BeatmapPoolData poolData, 
    BeatSaverService beatSaverService,
    ILogger<BeatmapQueueData> logger)
{
    public void AddBeatmap(string hash, VotingMap.Category category, VotingMap.DifficultyType difficulty, ulong poolerId, ulong threadId)
    {
        using var scope = scopeFactory.CreateScope();
        
        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;

        dataContext.PooledBeatmaps.Add(new PooledBeatmap()
        {
            Guid = Guid.NewGuid().ToString(),
            Hash = hash,
            Category = category,
            Difficulty = difficulty,
            PoolerId = poolerId,
            ThreadId = threadId,
            Status = PooledBeatmap.AcceptanceStatus.InProgress,
            DateAdded = DateTime.UtcNow
        });
        
        dataContext.SaveChanges();
    }

    public PooledBeatmap[] GetAllBeatmapsWithoutVerdict()
    {
        using var scope = scopeFactory.CreateScope();

        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;
        
        return dataContext.PooledBeatmaps.Where(i => i.Status == PooledBeatmap.AcceptanceStatus.InProgress).ToArray();
    }

    public PooledBeatmap? GetBeatmapFromThreadId(ulong threadId)
    {
        using var scope = scopeFactory.CreateScope();

        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;
        
        return dataContext.PooledBeatmaps.FirstOrDefault(i => i.ThreadId == threadId);
    }

    public async Task SetVerdict(string guid, PooledBeatmap.AcceptanceStatus verdict)
    {
        using var scope = scopeFactory.CreateScope();

        await using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;

        var map = dataContext.PooledBeatmaps.FirstOrDefault(i => i.Guid == guid);
        
        if (map == null)
            throw new Exception($"Could not find pooled Beatmap with guid '{guid}'");
        
        map.Status = verdict;
        map.DateVerdictDecided = DateTime.UtcNow;
        
        await dataContext.SaveChangesAsync();

        if (verdict == PooledBeatmap.AcceptanceStatus.Accepted)
        {
            var beatSaverMap = await beatSaverService.GetBeatmapFromHash(map.Hash);

            var diff = beatSaverMap["versions"]![0]!["diffs"]!.FirstOrDefault(i =>
                i.Value<string>("characteristic") == "Standard"
                && i.Value<string>("difficulty") == map.Difficulty.ToString()
            );
            
            if (diff == null)
                throw new Exception($"Could not find difficulty '{map.Difficulty}'");

            var duration = (int) Math.Ceiling(diff.Value<float>("seconds"));

            var maxScore = diff.Value<int>("maxScore");
            
            poolData.AddMap(map.Hash, map.Difficulty, map.Category, maxScore, duration);
        }
    }
}