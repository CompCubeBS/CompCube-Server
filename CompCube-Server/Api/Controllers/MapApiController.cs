using CompCube_Models.Models.Map;
using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Config;
using CompCube_Server.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace CompCube_Server.Api.Controllers;

[ApiController]
public class MapApiController(BeatmapPoolData beatmapPoolData, BeatSaverService beatSaver, ConfigHelper config) : ControllerBase
{
    [HttpGet("/api/maps/hashes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string[]> GetAllMapHashes() => beatmapPoolData.GetAllMapsFromActiveBatches().Select(i => i.Hash).ToArray();

    [HttpGet("/api/maps/playlist")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<string> GetPlaylist()
    {
        var allMaps = beatmapPoolData.GetAllMapsFromActiveBatches();

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
    [Authorize(Policy = "RequireMapPooler")]
    public IActionResult AddMap( 
        string hash, 
        string difficulty, 
        string category,
        int maxScore,
        int durationSeconds,
        string poolIds
        )
    {
        if (!Enum.TryParse<VotingMap.DifficultyType>(difficulty, out var difficultyType) || 
            !Enum.TryParse<VotingMap.Category>(category, out var categoryType))
            return BadRequest();

        beatmapPoolData.AddMap(hash, difficultyType, categoryType, maxScore, durationSeconds, poolIds.Split(",").Select(i => int.Parse(i)).ToArray());

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