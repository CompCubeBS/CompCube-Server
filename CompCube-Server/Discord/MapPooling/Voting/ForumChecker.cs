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
    
    public ForumChecker(RestClient restClient, DiscordConfigHelper configHelper, MapVoteHelper mapVoteHelper, ILogger<ForumChecker> logger, MapLog mapLog)
    {
        _restClient = restClient;
        _configHelper = configHelper;
        _mapVoteHelper = mapVoteHelper;
        _logger = logger;
        _mapLog = mapLog;
        
        // Console.WriteLine("started");
        
        Task.Factory.StartNew(CheckMaps, TaskCreationOptions.LongRunning);
    }

    private async Task CheckMaps()
    {
        while (true)
        {
            try
            {
                // Console.WriteLine("checking maps");

                var activeThreads = await _restClient.GetActiveGuildThreadsAsync(_configHelper.GuildId);

                // Console.WriteLine(activeThreads.Count);

                var self = await _restClient.GetCurrentApplicationAsync();

                activeThreads = activeThreads.Where(i => i.OwnerId == self.Id).ToArray();

                // Console.WriteLine(activeThreads.Count);

                foreach (var thread in activeThreads)
                    await CheckForum(thread);

                await Task.Delay(new TimeSpan(1, 0, 0));
            }
            catch (Exception e)
            {
                _logger.LogError(e.ToString());
            }
        }
    }

    private async Task CheckForum(GuildThread thread)
    {
        // Console.WriteLine($"checking for {thread.Id}");
        
        if (thread.CreatedAt.AddDays(7) > DateTimeOffset.Now)
            return;

        if (thread.CreatedAt.AddDays(14) <= DateTimeOffset.Now)
        {
            // Console.WriteLine("rejected for inactivity");
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
            await DenyMap(thread, voteState);
            return;
        }

        if (voteState.Upvotes.Count >= 3)
            await AcceptMap(thread, voteState);
    }

    public async Task AcceptMap(GuildThread forumThread, MapThreadUpvotes voteState, bool silent = false)
    {
        await _mapLog.LogQueuedBeatmap(forumThread);
        
        if (!silent)
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
    }

    public async Task DenyMap(GuildThread forumThread, MapThreadUpvotes voteState, bool silent = false)
    {
        await _mapLog.LogDeniedBeatmap(forumThread, false);

        if (!silent)
            await forumThread.SendMessageAsync(new MessageProperties()
            {
                Embeds = [
                    new EmbedProperties()
                    {
                        Description = $"❌ This map has been denied with {voteState.Upvotes.Count} upvotes and {voteState.Downvotes.Count} downvotes."
                    }
                ]
            });
        
        await forumThread.ModifyAsync(options => options.WithName($"[❌] {forumThread.Name}").WithArchived().WithLocked()); // .WithLocked().WithAppliedTags([acceptedTag.Id]));
    }
}