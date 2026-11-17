using CompCube_Models.Models.Map;
using NetCord;
using NetCord.Rest;
using NetCord.Services;
using NetCord.Services.ComponentInteractions;

namespace CompCube_Server.Discord.MapPooling;

public class MapPoolButtonInteractionModule(DiscordConfigHelper configHelper) : ComponentInteractionModule<ButtonInteractionContext>
{
    [ComponentInteraction("submitMapFromBeatSaverButton")]
    public async Task SubmitMapFromBeatSaverInteraction()
    {
        var guildUser = await Context.Guild?.GetUserAsync(Context.User.Id)!;

        var roles = guildUser.RoleIds.ToArray();
        
        var categoryOptions = configHelper.GetCategoriesFromRoles(roles).Select(i => new StringMenuSelectOptionProperties(i.ToString(), i.ToString()));
        var difficultyOptions = Enum.GetNames<BeatmapData.DifficultyType>().Select(j => new StringMenuSelectOptionProperties(j, j));
        
        var modal = new ModalProperties("submitMapFromBeatsaverModal", "Submit From BeatSaver")
        {
            Components = [
                new LabelProperties("Category", new StringMenuProperties("category", categoryOptions)),
                new LabelProperties("BSR", new TextInputProperties("bsr", TextInputStyle.Short)),
                new LabelProperties("Difficulty", new StringMenuProperties("difficulty", difficultyOptions))
            ]
        };
        
        await Context.Interaction.SendResponseAsync(InteractionCallback.Modal(modal));
    }
}