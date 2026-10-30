using CompCube_Server.Data;
using CompCube_Server.Data.Schema;
using NetCord;
using NetCord.Rest;

namespace CompCube_Server.Discord.MapPooling.Voting;

public class ForumChecker
{
    private readonly RestClient _restClient;
    private readonly DiscordConfigHelper _configHelper;
    private readonly MapVoteHelper _mapVoteHelper;
    private readonly ILogger<ForumChecker> _logger;
    private readonly MapLog _mapLog;
    private readonly BeatmapQueueData _beatmapQueueData;

    public ForumChecker(RestClient restClient, DiscordConfigHelper configHelper, MapVoteHelper mapVoteHelper, ILogger<ForumChecker> logger, MapLog mapLog, BeatmapQueueData beatmapQueueData)
    {
        _restClient = restClient;
        _configHelper = configHelper;
        _mapVoteHelper = mapVoteHelper;
        _logger = logger;
        _mapLog = mapLog;
        _beatmapQueueData = beatmapQueueData;

        // Console.WriteLine("started");
        
        Task.Factory.StartNew(CheckMaps, TaskCreationOptions.LongRunning);
    }

    private async Task CheckMaps()
    {
        while (true)
        {
            try
            {
                var allUnacceptedBeatmaps = _beatmapQueueData.GetAllBeatmapsWithoutVerdict();

                foreach (var beatmap in allUnacceptedBeatmaps)
                    await CheckBeatmap(beatmap);

                await Task.Delay(new TimeSpan(1, 0, 0));
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
            }
        }
    }

    private async Task CheckBeatmap(PooledBeatmap beatmap)
    {
        var thread = (await _restClient.GetActiveGuildThreadsAsync(_configHelper.GuildId)).FirstOrDefault(i => i.Id == beatmap.ThreadId);

        if (thread == null)
        {
            _logger.LogWarning("No active thread found for this map! Skipping...");
            return;
        }
        
        if (beatmap.DateAdded.AddDays(7) > DateTime.UtcNow)
            return;

        if (beatmap.DateAdded.AddDays(14) <= DateTime.UtcNow)
        {
            await _mapLog.LogDeniedBeatmap(thread, true);
            
            await _restClient.SendMessageAsync(thread.Id, new MessageProperties()
            {
                Embeds = [
                    new EmbedProperties()
                    {
                        Description = "This map has been denied due to a verdict not being made after 14 days."
                    }
                ]
            });

            await thread.ModifyAsync(options => options.WithName("[❌💤]" + thread.Name).WithArchived().WithLocked());
            return;
        }
        
        var voteState = await _mapVoteHelper.GetVotesFromThread(thread.Id);

        if (voteState.Downvotes.Count >= 3)
        {
            await DenyMap(beatmap, voteState);
            return;
        }

        if (voteState.Upvotes.Count >= 3)
            await AcceptMap(beatmap, voteState);
    }

    public async Task AcceptMap(PooledBeatmap beatmap, MapThreadUpvotes voteState)
    {
        var forumThread = (await _restClient.GetActiveGuildThreadsAsync(_configHelper.GuildId)).FirstOrDefault(i => i.Id == beatmap.ThreadId);
        
        if (forumThread == null)
            throw new Exception("No active thread found.");
        
        await _mapLog.LogQueuedBeatmap(forumThread);
        
        await forumThread.SendMessageAsync(new MessageProperties()
            {
                Embeds = [
                    new EmbedProperties()
                    {
                        Description = $"✅ This map has been accepted with {voteState.Upvotes.Count} upvotes and {voteState.Downvotes.Count} downvotes."
                    }
                ]
            });

        await forumThread.ModifyAsync(options =>
            options.WithName($"[✅] {forumThread.Name}").WithArchived().WithLocked()); // .WithAppliedTags([acceptedTag.Id]));

        await _beatmapQueueData.SetVerdict(beatmap.Guid, PooledBeatmap.AcceptanceStatus.Accepted);
    }

    public async Task DenyMap(PooledBeatmap beatmap, MapThreadUpvotes voteState)
    {
        var forumThread = (await _restClient.GetActiveGuildThreadsAsync(_configHelper.GuildId)).FirstOrDefault(i => i.Id == beatmap.ThreadId);
        
        if (forumThread == null)
            throw new Exception("No active thread found.");

        await forumThread.SendMessageAsync(new MessageProperties()
            {
                Embeds = [
                    new EmbedProperties()
                    {
                        Description = $"❌ This map has been denied with {voteState.Upvotes.Count} upvotes and {voteState.Downvotes.Count} downvotes."
                    }
                ]
            });
        
        await forumThread.ModifyAsync(options => options.WithName($"[❌] {forumThread.Name}").WithArchived().WithLocked());
        
        await _beatmapQueueData.SetVerdict(beatmap.Guid, PooledBeatmap.AcceptanceStatus.Denied);
    }
}