using CompCube_Server.Discord.MapPooling.Voting;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace CompCube_Server.Discord.MapPooling.Commands;

public class ForceControlCommands(RestClient restClient, DiscordConfigHelper config, ForumChecker checker, MapVoteHelper voteHelper) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("forceaccept", "Forcefully accept a map into queue")]
    public async Task<InteractionMessageProperties> ForceAcceptMap()
    {
        var thread =
            (await restClient.GetActiveGuildThreadsAsync(config.GuildId)).FirstOrDefault(i => i.Id == Context.Channel.Id);

        if (thread == null)
            return "No thread found";
        
        var upvotes = await voteHelper.GetVotesFromThread(thread.Id);
        
        await checker.AcceptMap(thread, upvotes, true);
        return "Forcefully queued map.";
    }

    [SlashCommand("forcedeny", "Forcefully deny a map")]
    public async Task<InteractionMessageProperties> ForceDenyMap()
    {
        var thread =
            (await restClient.GetActiveGuildThreadsAsync(config.GuildId)).FirstOrDefault(i => i.Id == Context.Channel.Id);

        if (thread == null)
            return "No thread found";
        
        var upvotes = await voteHelper.GetVotesFromThread(thread.Id);
        
        await checker.DenyMap(thread, upvotes, true);
        return "Forcefully queued map.";
    }
}