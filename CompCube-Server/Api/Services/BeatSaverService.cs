using BeatSaverSharp;
using BeatSaverSharp.Models;
using CompCube_Server.Api.Controllers;
using CompCube_Server.Data;

namespace CompCube_Server.Api.BeatSaver;

public class BeatSaverService(MapData mapData, ILogger<BeatSaverService> logger)
{
    private readonly BeatSaverSharp.BeatSaver _beatSaver = new(new BeatSaverOptions("CompCube-Server", new Version("1.0.0")));

    public async Task<Beatmap?> GetBeatmapFromHash(string hash) => await _beatSaver.BeatmapByHash(hash);

    public async Task<Beatmap?> GetBeatmapFromKey(string key) => await _beatSaver.Beatmap(key);

    public async Task<Beatmap[]?> GetBeatmapsFromHashes(string[] hashes) => (await _beatSaver.BeatmapByHash(hashes)).Select(i => i.Value).ToArray();

    public async Task DownloadToBeatmapsFolder(string hash)
    {
        var map = await GetBeatmapFromHash(hash);
        
        if (map == null)
            throw new Exception("Beatmap not found: " + hash);

        var bytes = await map.LatestVersion.DownloadZIP();
        
        if (bytes == null)
            throw new Exception("Could not download beatmap: " + hash);
        
        await File.WriteAllBytesAsync(Path.Combine(MapApiController.BeatmapsPath, hash + ".zip"), bytes);
    }

    public async Task DownloadAllMissingBeatmaps()
    {
        var maps = mapData.GetAllMapsFromActiveBatches().DistinctBy(m => m.Hash).Select(i => i.Hash).ToArray();
        foreach (var hash in maps)
        {
            try
            {
                if (File.Exists(Path.Combine(MapApiController.BeatmapsPath, hash + ".zip")))
                    continue;

                await DownloadToBeatmapsFolder(hash);
            }
            catch (Exception e)
            {
                logger.LogError("Failed to download missing beatmaps: {e}", e.ToString());
            }
        }
    }
}