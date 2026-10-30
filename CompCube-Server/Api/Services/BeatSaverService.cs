using BeatSaverSharp;
using BeatSaverSharp.Models;
using CompCube_Server.Api.Controllers;
using CompCube_Server.Data;
using Newtonsoft.Json.Linq;

namespace CompCube_Server.Api.BeatSaver;

public class BeatSaverService(ILogger<BeatSaverService> logger)
{
    private readonly HttpClient _httpClient = new();
    
    public async Task<JObject> GetBeatmapFromHash(string hash)
    {
        var response = await _httpClient.GetAsync($"https://api.beatsaver.com/maps/hash/{hash}");
        
        response.EnsureSuccessStatusCode();
        
        return JObject.Parse(await response.Content.ReadAsStringAsync());
    }

    public async Task<JObject> GetBeatmapFromKey(string key)
    {
        var response = await _httpClient.GetAsync($"https://api.beatsaver.com/maps/id/{key}");
        
        response.EnsureSuccessStatusCode();

        return JObject.Parse(await response.Content.ReadAsStringAsync());
    }

    public async Task<Dictionary<string, JObject>> GetBeatmapsFromHashes(string[] hashes)
    {
        var response = await _httpClient.GetAsync($"https://api.beatsaver.com/maps/hash/{string.Join(',', hashes)}");
        
        response.EnsureSuccessStatusCode();
        
        var jObject = JObject.Parse(await response.Content.ReadAsStringAsync());
        
        var dictionary = new Dictionary<string, JObject>();

        foreach (var entry in jObject)
        {
            if (entry.Value == null)
            {
                logger.LogWarning("No hash {hash} found on BeatSaver!", entry.Key);
                continue;
            }
            
            dictionary.Add(entry.Key, entry.Value.ToObject<JObject>()!);
        }

        return dictionary;
    }
}