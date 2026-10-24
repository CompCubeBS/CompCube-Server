using CompCube_Models.Models.Map;
using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Data;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace CompCube_Server.Discord.MapPooling.Commands;

public class ListMapsCommand(MapData mapData, BeatSaverService beatSaver, BeatmapPoolData queue)
    : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("maps", "Shows all active and playable maps.", Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> ShowMaps()
    {
        var maps = mapData.GetAllMapsFromActiveBatches().OrderBy(i => i.MapCategory);

        return new InteractionMessageProperties()
        {
            Embeds =
            [
                await GetEmbedFromMapList(maps.ToArray())
            ]
        };
    }

    [SlashCommand("getqueue", "Shows all maps currently in queue", Contexts = [InteractionContextType.Guild])]
    public async Task<InteractionMessageProperties> GetQueue()
    {
        var maps = queue.GetMaps().OrderBy(i => i.MapCategory);

        return new InteractionMessageProperties()
        {
            Embeds = [await GetEmbedFromMapList(maps.ToArray())]
        };
    }

private async Task<EmbedProperties> GetEmbedFromMapList(VotingMap[] maps)
    {
        var mapMetaData = await beatSaver.GetBeatmapsFromHashes(maps.Select(i => i.Hash).ToArray());

        if (mapMetaData == null || mapMetaData.Length == 0)
            return new EmbedProperties()
            {
                Description = "No maps!"
            };

        return new EmbedProperties()
        {
            Description = string.Join("\n", mapMetaData.Select(i => $"{i.Metadata.SongAuthorName} - {i.Metadata.SongName} (Mapped by {i.Metadata.LevelAuthorName})"))
        };
    }
}