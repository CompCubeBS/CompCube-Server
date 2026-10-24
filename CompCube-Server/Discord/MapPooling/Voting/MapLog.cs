using BeatSaverSharp.Models;
using CompCube_Models.Models.Map;
using CompCube_Server.Api.BeatSaver;
using NetCord;
using NetCord.Rest;

namespace CompCube_Server.Discord.MapPooling.Voting;

public class MapLog(RestClient restClient, BeatSaverService beatSaver, DiscordConfigHelper configHelper)
{
    public async Task LogQueuedBeatmap(GuildThread thread)
    {
        var metaData = await GetMapDataFromGuildThread(thread);

        await restClient.SendMessageAsync(configHelper.MapPoolLoggingChannel, new MessageProperties()
        {
            Content = $"[✅] {metaData.Item1} ({metaData.Item2.ToString()}) has been queued as {metaData.Item3.ToString()}."
        });
    }

    public async Task LogDeniedBeatmap(GuildThread thread, bool inactivity)
    {
        var data = await GetMapDataFromGuildThread(thread);
        
        await restClient.SendMessageAsync(configHelper.MapPoolLoggingChannel,
            $"[❌{(inactivity ? "💤" : string.Empty)}] {data.Item1} ({data.Item2}) has been denied.");
    }

    private async Task<Tuple<string, VotingMap.DifficultyType, VotingMap.Category>> GetMapDataFromGuildThread(GuildThread thread)
    {
        var message = await thread.GetMessageAsync(thread.Id);

        var name = message.Embeds.First().Title;
        var difficulty = Enum.Parse<VotingMap.DifficultyType>(message.Embeds.First().Fields.First(i => i.Name == "Difficulty").Value);
        var category =
            Enum.Parse<VotingMap.Category>(message.Embeds.First().Fields.First(i => i.Name == "Category").Value);
        
        return new Tuple<string, VotingMap.DifficultyType, VotingMap.Category>(name!, difficulty, category);
    }
}