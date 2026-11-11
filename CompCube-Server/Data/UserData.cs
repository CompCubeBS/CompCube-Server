using System.Security.Claims;
using CompCube_Models.Models.ClientData;
using CompCube_Server.Config;
using CompCube_Server.Data.Schema;
using CompCube_Server.Models.CompCube_Models.Models.Auth.BeatKhana;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CompCube_Server.Data;

public class UserData(IServiceScopeFactory scopeFactory, ConfigHelper configHelper, ILogger<UserData> logger)
{
    public UserProfile Debug => new(
        "0", 
        "debug", 
        "0", 
        "https://cdn.scoresaber.com/avatars/oculus.png?v=1781213201", 
        false,
        [],
        new CompetitiveInfo(
            configHelper.Season,
            1000,
            1,
            0,
            0,
            0,
            0
            )
        );

    public UserProfileFull[] GetUserInfoByPermission(string permissionFilter)
    {
        using var scope = scopeFactory.CreateScope();
        
        using var context = scope.ServiceProvider.GetService<DataContext>()!;

        return context.Users
            .Include(i => i.Flairs)
            .Include(i => i.CompetetiveStatistics)
            .Where(i => i.Flairs.Any(j => j.Permissions.Contains(permissionFilter)) || i.Permissions.Contains(permissionFilter))
            .Select(i => GetFullUserProfileFromModel(i, context))
            .ToArray();
    }

    public void UpdateUserLastPlayedTime(string platformId)
    {
        using var scope = scopeFactory.CreateScope();

        using var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var user = context.Users.SingleOrDefault(p => p.PlatformId == platformId);

        if (user == null)
            throw new Exception("Could not find user with id " + platformId);

        user.LastPlayed = DateTimeOffset.UtcNow;
        
        context.SaveChanges();
    }

    public List<UserProfile> GetLeaderboardRange(int start, int range, int season = -1)
    {
        using var scope = scopeFactory.CreateScope();
        using var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        if (season == -1)
            season = configHelper.Season;

        range = Math.Min(range, 10);

        var users = context.Users
            .Include(i => i.CompetetiveStatistics)
            .Include(i => i.Flairs)
            .Where(i => i.CompetetiveStatistics.Any(j => j.Season == season))
            .OrderByDescending(i => i.CompetetiveStatistics.Single(j => j.Season == season).Elo)
            .Skip(start)
            .Take(range)
            .ToArray();

        return users.Select(i => GetUserProfileFromModel(i, season, context)).Where(i => i != null).ToList()!;
    }

    public List<UserProfile>? GetAroundUser(string platformId, int season = -1)
    {
        if (season == -1)
            season = configHelper.Season;
        
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;

        var users = context.Users
            .Include(p => p.CompetetiveStatistics)
            .Include(p => p.Flairs)
            .Where(p => p.CompetetiveStatistics.Any(i => i.Season == season))
            .Where(p => !p.Banned)
            .OrderByDescending(p => p.CompetetiveStatistics.First(i => i.Season == season).Elo).ToArray();
        
        var index = Array.FindIndex(users, u => u.PlatformId == platformId);

        if (index == -1)
            return null;

        var startIndex = index - 5;
        
        var count = Math.Min(10, users.Length - startIndex);
        
        return users.Skip(startIndex).Take(count).Select(user => GetUserProfileFromModel(user, season, context)).Where(i => i != null).ToList()!;
    }

    public UserProfile UpsertFromBeatKhanaToken(ClaimsPrincipal claims)
    {
        var platformId = claims.Claims.First(c => c.Type == "platformId").Value;
        var guid = claims.Claims.First(c => c.Type == "guid").Value;
        var globalName = claims.Claims.First(c => c.Type == "global_name").Value;
        var username = claims.Claims.First(c => c.Type == "username").Value;
        var avatarUrl = claims.Claims.First(c => c.Type == "avatarUrl").Value;
        
        if (claims.Claims.FirstOrDefault(i => i.Type == "guid" && i.Value != "") != null)
            return UpsertFromBeatKhana(platformId!, guid, globalName == "" ? username : globalName, avatarUrl);

        if (platformId == "")
            throw new Exception("Token has no usable identity!");
        
        return UpsertPluginAccount(platformId, username);
    }

    public UserProfile UpsertFromBeatKhana(string platformId, string beatKhanaGuid, string userName, string avatarUrl)
    {
        using var scope = scopeFactory.CreateScope();
        using var context = scope.ServiceProvider.GetService<DataContext>()!;

        var user = context.Users
            .Include(i => i.Flairs)
            .Include(i => i.CompetetiveStatistics)
            .SingleOrDefault(i => i.PlatformId == platformId);

        if (user == null)
        {
            user = new User
            {
                Guid = Guid.NewGuid().ToString(),
                PlatformId = platformId,
                Username = userName,
                BeatKhanaId = beatKhanaGuid,
                AvatarUrl = avatarUrl,
                Banned = false,
                Permissions = [],
                Created = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                LastPlayed = null
            };

            context.Users.Add(user);
        }
        else
        {
            user.BeatKhanaId = beatKhanaGuid;
            user.PlatformId = platformId;
            user.Username = userName;
            user.AvatarUrl = avatarUrl;
        }

        context.SaveChanges();

        var stats = CreateOrGetCompetitiveStatistics(user, context);

        user = context.Users
            .Include(i => i.Flairs)
            .Include(i => i.CompetetiveStatistics)
            .Single(i =>
                i.CompetetiveStatistics.Any(j => j.Season == configHelper.Season) && i.PlatformId == platformId);

        return new UserProfile(user.Guid, user.Username, user.PlatformId, user.AvatarUrl, user.Banned, GetFlairsFromModel(user.Flairs), GetCompetitiveInfoFromModel(stats, context));
    }

    public UserProfile UpsertPluginAccount(string platformId, string username)
    {
        using var scope = scopeFactory.CreateScope();
        using var context = scope.ServiceProvider.GetService<DataContext>()!;

        var user = context.Users
            .Include(i => i.Flairs)
            .Include(i => i.CompetetiveStatistics)
            .SingleOrDefault(i => i.PlatformId == platformId);

        if (user == null)
        {
            user = new User
            {
                Guid = Guid.NewGuid().ToString(),
                PlatformId = platformId,
                Username = username,
                AvatarUrl = $"https://cdn.scoresaber.com/avatars/{platformId}.jpg",
                Banned = false,
                Permissions = [],
                Created = DateTimeOffset.UtcNow,
                Updated = DateTimeOffset.UtcNow,
                LastPlayed = null
            };

            context.Users.Add(user);
        }
        else
        {
            user.PlatformId = platformId;
            user.Username = username;
        }
        
        context.SaveChanges();

        var stats = CreateOrGetCompetitiveStatistics(user, context);

        user = context.Users
            .Include(i => i.Flairs)
            .Include(i => i.CompetetiveStatistics)
            .Single(i =>
                i.CompetetiveStatistics.Any(j => j.Season == configHelper.Season) && i.PlatformId == platformId);

        return new UserProfile(user.Guid, user.Username, user.PlatformId, user.AvatarUrl, user.Banned, GetFlairsFromModel(user.Flairs), GetCompetitiveInfoFromModel(stats, context));
    }

    private CompetetiveStatistics CreateOrGetCompetitiveStatistics(User user, DataContext context)
    {
        var stats = context.CompetitiveStatistics.SingleOrDefault(i =>
            i.User.PlatformId == user.PlatformId && i.Season == configHelper.Season);

        if (stats != null) 
            return stats;
        
        stats = new CompetetiveStatistics()
        {
            Guid = Guid.NewGuid().ToString(),
            BestWinStreak = 0,
            Elo = 1000,
            Season = configHelper.Season,
            TotalGamesPlayed = 0,
            WinStreak = 0,
            Wins = 0,
            User = user
        };
            
        context.CompetitiveStatistics.Add(stats);

        context.SaveChanges();

        return stats;
    }

    public void IncrementWins(string platformId)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;

        try
        {
            var user = context.Users
                .Include(i => i.CompetetiveStatistics)
                .FirstOrDefault(i => i.PlatformId == platformId);

            if (user == null)
                return;
            
            var stats = user.CompetetiveStatistics.First(i => i.Season == configHelper.Season);

            stats.WinStreak++;

            stats.Wins++;
            
            if (stats.WinStreak > stats.BestWinStreak)
            {
                stats.BestWinStreak = stats.WinStreak;
            }

            context.SaveChanges();
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to increment wins for {platformId}");
            throw;
        }
    }

    public string? GetUserProfilePictureUrl(string platformId)
    {
        using var scope = scopeFactory.CreateScope();
        using var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var user = context.Users.SingleOrDefault(i => i.PlatformId == platformId);

        return user?.AvatarUrl;
    }

    public void AdjustElo(string platformId, int eloChange)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        try
        {
            var user = context.Users
                .Include(i => i.CompetetiveStatistics)
                .First(i => i.PlatformId == platformId);
            
            var stats = user.CompetetiveStatistics.First(i => i.Season == configHelper.Season);

            stats.Elo += eloChange;
            
            context.SaveChanges();
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to adjust elo for {platformId}");
            throw;
        }
    }

    public void ResetWinstreak(string platformId)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;

        try
        {
            var user = context.Users
                .Include(i => i.CompetetiveStatistics)
                .FirstOrDefault(i => i.PlatformId == platformId);

            if (user == null)
                return;
            
            var stats = user.CompetetiveStatistics.First(i => i.Season == configHelper.Season);

            stats.WinStreak = 0;
            context.SaveChanges();
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to reset winstreak for {platformId}");
            throw;
        }
    }

    public void IncrementTotalGames(string platformId)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;

        try
        {
            var user = context.Users
                .Include(i => i.CompetetiveStatistics)
                .FirstOrDefault(i => i.PlatformId == platformId);

            if (user == null)
                return;

            user.CompetetiveStatistics
                .First(i => i.Season == configHelper.Season)
                .TotalGamesPlayed++;

            context.SaveChanges();
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to increment total games for {platformId}");
            throw;
        }
    }
    
    public List<UserProfile> GetAllUserProfiles(int season = -1)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var users = context.Users.Include(p => p.CompetetiveStatistics)
            .Include(p => p.Flairs)
            .Where(i => !i.Banned)
            .Where(i => i.CompetetiveStatistics.Any(j => j.Season == season))
            .OrderByDescending(i => i.CompetetiveStatistics.First(j => j.Season == season).Elo)
            .ToArray();

        return users.Select(user => GetUserProfileFromModel(user, season, context)).ToList();
    }

    public bool SetFlairs(string platformId, string[] flairGuids)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var flairs = context.UserFlairs.Where(i => flairGuids.Contains(i.Guid)).ToArray();
        
        if (flairs.Length != flairGuids.Length)
            return false;
        
        var user = context.Users.FirstOrDefault(i => i.PlatformId == platformId);

        if (user == null)
            return false;

        user.Flairs = flairs.ToList();

        context.SaveChanges();
        return true;
    }

    public bool SetBannedStatus(string platformId, bool banned)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var user = context.Users.FirstOrDefault(i => i.PlatformId == platformId);

        if (user == null)
            return false;
        
        user.Banned = banned;

        context.SaveChanges();

        return true;
    }
    
    public UserProfileFull? GetFullUserProfileByPlatformId(string platformId)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var user = context.Users.Include(user => user.Flairs).FirstOrDefault(i => i.PlatformId == platformId);

        if (user == null)
            return null;

        return GetFullUserProfileFromModel(user, context);
    }

    public UserProfile? GetUserProfileByPlatformId(string platformId, int season = -1)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        if (season == -1)
            season = configHelper.Season;
        
        var user = context.Users.Include(p => p.CompetetiveStatistics).Include(user => user.Flairs).FirstOrDefault(i => i.PlatformId == platformId);

        if (user == null)
            return null;
        
        return GetUserProfileFromModel(user, season, context);
    }
    
    private static Flair[] GetFlairsFromModel(List<UserFlair> flair)
    {
        return flair.Select(i => new Flair(i.Guid, i.Name, i.Color, i.Permissions.ToArray())).ToArray();
    }

    private UserProfile GetUserProfileFromModel(User user, int season, DataContext context)
    {
        var competitiveStats = user.CompetetiveStatistics.FirstOrDefault(i => i.Season == season);
        
        return new UserProfile(
            user.Guid, 
            user.Username, 
            user.PlatformId,
            $"{configHelper.ApiUrl}/api/user/id/{user.PlatformId}/avatar", 
            user.Banned,
            GetFlairsFromModel(user.Flairs), 
            new CompetitiveInfo(
                competitiveStats.Season, 
                competitiveStats.Elo, 
                GetRankFromElo(competitiveStats.Elo, competitiveStats.Season, context),
                competitiveStats.Wins,
                competitiveStats.TotalGamesPlayed,
                competitiveStats.WinStreak,
                competitiveStats.BestWinStreak));
    }
    
    private static int GetRankFromElo(int elo, int season, DataContext context) => context.CompetitiveStatistics.Where(i => i.Season == season).Count(i => i.Elo > elo) + 1;

    private static CompetitiveInfo GetCompetitiveInfoFromModel(CompetetiveStatistics info,
        DataContext context) => new(
        info.Season,
        info.Elo,
        GetRankFromElo(info.Elo, info.Season, context),
        info.Wins,
        info.TotalGamesPlayed,
        info.WinStreak,
        info.BestWinStreak
        );
    
    private UserProfileFull GetFullUserProfileFromModel(User user, DataContext context) => new(
        user.Username,
        user.PlatformId,
        user.BeatKhanaId,
        $"{configHelper.ApiUrl}/api/user/id/{user.PlatformId}/avatar",
        GetFlairsFromModel(user.Flairs),
        user.Banned,
        user.LastPlayed,
        user.Created,
        user.Updated,
        user.CompetetiveStatistics.Select(i => GetCompetitiveInfoFromModel(i, context)).ToArray()
        );
}