using CompCube_Models.Models.Map;
using CompCube_Server.Api.BeatSaver;
using CompCube_Server.Data;
using CompCube_Server.Extensions;
using NetCord;
using NetCord.Rest;
using NetCord.Services.ComponentInteractions;
using Newtonsoft.Json.Linq;

namespace CompCube_Server.Discord.MapPooling;

public class SubmitFromBeatsaverModalInteractionModule(
    IConfiguration config, 
    RestClient client, 
    BeatSaverService beatSaverApi,
    BeatmapQueueData beatmapQueueData) : ComponentInteractionModule<ModalInteractionContext>
{
    [ComponentInteraction("submitMapFromBeatsaverModal")]
    public async Task SubmitMapFromBeatsaverModalInteraction()
    {
        await SendEmptyResponse();
        
        var category = Context.Components.OfType<Label>().Select(i => i.Component).OfType<StringMenu>().First().SelectedValues?.FirstOrDefault();
        var bsr = Context.Components.OfType<Label>().Select(i => i.Component).OfType<TextInput>().FirstOrDefault()?.Value;
        var difficulty = Context.Components.OfType<Label>().Select(i => i.Component).OfType<StringMenu>().Last().SelectedValues?.FirstOrDefault();

        if (category is null)
        {
            await DmUser("Failed to get category from form!");
            return;
        }

        if (bsr is null)
        {
            await DmUser("Failed to get BSR from form!");
            return;
        }

        if (difficulty is null)
        {
            await DmUser("Failed to fetch difficulty from form!");
            return;
        }
        
        var categoryEnum = Enum.Parse<VotingMap.Category>(category);
        var diffEnum = Enum.Parse<VotingMap.DifficultyType>(difficulty);

        JObject beatSaverBeatmap;

        try
        {
            beatSaverBeatmap = await beatSaverApi.GetBeatmapFromKey(bsr);
        }
        catch
        {
            await DmUser($"Failed to fetch beatmap {bsr} from beatsaver! (Are you sure this is the right key?)");
            return;
        }
        
        var poolingChannelId = config.GetSection("Discord").GetSection("PoolingChannelIds").GetValue<ulong>(category);

        var forumThread = await client.CreateForumGuildThreadAsync(poolingChannelId, new ForumGuildThreadProperties($"({beatSaverBeatmap["metadata"]!.Value<string>("levelAuthorName")}) {beatSaverBeatmap.Value<string>("name")}", new ForumGuildThreadMessageProperties
        {
            Embeds = [new EmbedProperties
            {
                Image = new EmbedImageProperties(beatSaverBeatmap["versions"]![0]!.Value<string>("coverURL")),
                Title = beatSaverBeatmap.Value<string>("name"),
                // Description = beatSaverBeatmap.Description,
                Fields = [new EmbedFieldProperties
                    {
                        Name = "Map Link",
                        Value = $"https://beatsaver.com/maps/{bsr}"
                    },
                    new EmbedFieldProperties
                    {
                        Name = "Difficulty",
                        Value = difficulty,
                        Inline = true,
                    },
                    new EmbedFieldProperties()
                    {
                        Inline = true,
                        Name = "Submitted by:",
                        Value = Context.User.GetMention()
                    },
                    new EmbedFieldProperties()
                    {
                        Inline = false,
                        Name = "Category",
                        Value = category
                    },
                    new EmbedFieldProperties()
                    {
                        Inline = false,
                        Name = "Mapper",
                        Value = $"{beatSaverBeatmap["metadata"]!.Value<string>("levelAuthorName")} ({beatSaverBeatmap["uploader"]!.Value<string>("name")})"
                    },
                ]
            }]
        }));
        
        beatmapQueueData.AddBeatmap(beatSaverBeatmap["versions"]![0]!.Value<string>("hash")!, categoryEnum, diffEnum, Context.User.Id, forumThread.Id);

        await forumThread.SendMessageAsync(new MessageProperties()
        {
            Content = $"<@&{1438697609383514163}>",
            AllowedMentions = new AllowedMentionsProperties()
            {
                AllowedRoles = [1438697609383514163],
                AllowedUsers = null
            },
            
            Embeds = [
                new EmbedProperties()
                {
                    Description = $"React to the forum post to vote on the map\n👍 to upvote\n👎 to downvote",
                }
            ]
        });
    }

    private async Task SendEmptyResponse() => await RespondAsync(InteractionCallback.DeferredModifyMessage);

    private async Task DmUser(MessageProperties message)
    {
        var dmChannel = await Context.User.GetDMChannelAsync();

        await dmChannel.SendMessageAsync(message);
    }
}