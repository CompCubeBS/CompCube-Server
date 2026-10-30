using CompCube_Server.Data;
using CompCube_Server.Discord.MapPooling.Voting;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;

namespace CompCube_Server.Discord.MapPooling.Commands;

public class ForceControlCommands(BeatmapQueueData beatmapQueueData, ForumChecker checker, MapVoteHelper voteHelper) : ApplicationCommandModule<ApplicationCommandContext>
{
    [SlashCommand("forceaccept", "Forcefully accept a map into queue")]
    public async Task<InteractionMessageProperties> ForceAcceptMap()
    {
        var beatmap = beatmapQueueData.GetBeatmapFromThreadId(Context.Channel.Id);

        if (beatmap == null)
            return "No beatmap found!";
        
        var upvotes = await voteHelper.GetVotesFromThread(beatmap.ThreadId);
        
        checker.AcceptMap(beatmap, upvotes);
        return "Forcefully queued map.";
    }

    [SlashCommand("forcedeny", "Forcefully deny a map")]
    public async Task<InteractionMessageProperties> ForceDenyMap()
    {
        var beatmap = beatmapQueueData.GetBeatmapFromThreadId(Context.Channel.Id);

        if (beatmap == null)
            return "No beatmap found!";
        
        var upvotes = await voteHelper.GetVotesFromThread(beatmap.ThreadId);
        
        checker.DenyMap(beatmap, upvotes);
        return "Forcefully queued map.";
    }
}