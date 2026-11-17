using CompCube_Models.Models.Map;

namespace CompCube_Server.Discord;

public class DiscordConfigHelper(IConfiguration config)
{
    private Dictionary<BeatmapData.Category, ulong> _roleIds => Enum.GetValues<BeatmapData.Category>().Where(i => i != BeatmapData.Category.Special).Select(i => new KeyValuePair<BeatmapData.Category, ulong>(i, config.GetSection("Discord").GetSection("RoleIds").GetValue<ulong>(i.ToString()))).ToDictionary();
    public Dictionary<BeatmapData.Category, ulong> ForumChannels => Enum.GetValues<BeatmapData.Category>().Select(i => new KeyValuePair<BeatmapData.Category,ulong>(i, config.GetSection("Discord").GetSection("ForumChannels").GetValue<ulong>(i.ToString()))).ToDictionary();

    public ulong MasterPoolerRoleId => config.GetSection("Discord").GetSection("RoleIds").GetValue<ulong>("MasterPooler");
    
    public ulong PoolerRoleId => config.GetSection("Discord").GetSection("RoleIds").GetValue<ulong>("Pooler");
    
    public ulong MapPoolLoggingChannel => config.GetSection("Discord").GetValue<ulong>("MapPoolLoggingChannel");
    
    public ulong GuildId => config.GetSection("Discord").GetValue<ulong>("GuildId");

    public BeatmapData.Category[] GetCategoriesFromRoles(ulong[]? roleIds)
    {
        if (roleIds == null)
            return [];

        if (roleIds.Contains(MasterPoolerRoleId))
            return Enum.GetValues<BeatmapData.Category>();

        return _roleIds.Where(i => roleIds.Contains(i.Value)).Select(i => i.Key).ToArray();
    }
}