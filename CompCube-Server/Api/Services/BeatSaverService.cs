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
}