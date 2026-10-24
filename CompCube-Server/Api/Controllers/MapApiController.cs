using CompCube_Models.Models.Map;
using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Config;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace CompCube_Server.Api.Controllers;

[ApiController]
public class MapApiController(MapData mapData, BeatmapPoolData beatmapPoolData, BeatSaverService beatSaver, ConfigHelper config) : ControllerBase
{
    public static readonly string BeatmapsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Beatmaps");

    [HttpGet("/api/maps/hashes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string[]> GetAllMapHashes() => mapData.GetAllMapsFromActiveBatches().Select(i => i.Hash).ToArray();

    [HttpGet("/api/maps/download/{hash}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult DownloadMap(string hash)
    {
        var filePath = Path.Combine(BeatmapsPath, $"{hash}.zip");

        if (!System.IO.File.Exists(filePath))
            return NotFound();
        
        var bytes = System.IO.File.ReadAllBytes(filePath);
        
        return File(bytes, "application/zip", $"{hash}.zip");
    }

    [HttpGet("/api/maps/playlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string> GetPlaylist()
    {
        var allMaps = mapData.GetAllMapsFromActiveBatches();

        var songs = new List<PlaylistSong>();

        foreach (var song in allMaps)
        {
            if (songs.Any(i => i.Hash == song.Hash))
                continue;
            
            var allSimilarHashes = allMaps.Where(i => i.Hash.Equals(song.Hash, StringComparison.CurrentCultureIgnoreCase));

            var playlistSong = new PlaylistSong(song.Hash, allSimilarHashes.Select(i => i.Difficulty).ToArray());
            
            songs.Add(playlistSong);
        }

        var jObject = new JObject
        {
            {"playlistTitle", "CompCube Maps"},
            {"playlistAuthor", "CompCube Team"},
            {"songs", new JArray(songs.Select(i => i.GetJsonObject()))}
        };

        return jObject.ToString();
    }

    [HttpPost("/api/maps/forceadd")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddMap(string secret, 
        string hash, 
        string difficulty, 
        string category,
        int maxScore,
        int durationSeconds
        )
    {
        if (secret != config.Secret)
            return Forbid();
        
        if (!Enum.TryParse<VotingMap.DifficultyType>(difficulty, out var difficultyType))
            return BadRequest();
        
        if (!Enum.TryParse<VotingMap.Category>(category, out var categoryType))
            return BadRequest();
        
        beatmapPoolData.AddMap(hash, difficultyType, categoryType, maxScore, durationSeconds);

        await beatSaver.DownloadAllMissingBeatmaps();

        return Ok();
    }
}

public class PlaylistSong(string hash, VotingMap.DifficultyType[] difficultyTypes)
{
    public readonly string Hash = hash;

    public readonly VotingMap.DifficultyType[] DifficultyTypes = difficultyTypes;

    public JObject GetJsonObject()
    {
        var difficultiesObject = new JArray();

        foreach (var diff in DifficultyTypes)
        {
            difficultiesObject.Add(new JObject
            {
                {"characteristic", "Standard"},
                {"name", diff.ToString()}
            });
        }
        
        var jObject = new JObject
        {
            {"hash", Hash},
            {"difficulties", difficultiesObject}
        };

        return jObject;
    }
}