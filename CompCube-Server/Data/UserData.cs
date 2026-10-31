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
    public UserStatistics Debug => new("debug", 
        "0", 
        null, 
        "https://cdn.scoresaber.com/avatars/oculus.png?v=1781213201",
        [],
        false,
        0,
        1000,
        0,
        0,
        0,
        0
        );

    public UserInfo[] GetUserInfoByPermission(string permissionFilter)
    {
        using var scope = scopeFactory.CreateScope();
        
        using var dataContext = scope.ServiceProvider.GetService<DataContext>()!;

        return dataContext.Users.Include(i => i.Flairs).Where(i => i.Flairs.Any(j => j.Permissions.Contains(permissionFilter)) || i.Permissions.Contains(permissionFilter)).Select(i => GetUserInfoFromUserModel(i)).ToArray();
    }

    public List<UserStatistics> GetLeaderboardRange(int start, int range, int season = -1)
    {
        if (season == -1)
            season = configHelper.Season;

        range = Math.Min(range, 10);

        var users = GetAllUserStatistics(season).Where(i => i.Rank >= start).ToArray();
        Array.Resize(ref users, Math.Min(users.Length, range));

        return users.ToList();
    }

    public List<UserStatistics>? GetAroundUser(string platformId, int season = -1)
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
            .OrderBy(p => p.CompetetiveStatistics.First(i => i.Season == season).Elo).ToArray();
        
        var index = Array.FindIndex(users, u => u.PlatformId == platformId);

        if (index == -1)
            return null;

        var startIndex = index - 5;
        
        var count = Math.Min(10, users.Length - startIndex);
        
        return users.Skip(startIndex).Take(count).Select(user =>
        {
            var stats = user.CompetetiveStatistics.First(k => k.Season == season);

            return new UserStatistics(user.Username, user.PlatformId, user.BeatKhanaId, user.AvatarUrl,
                GetFlairsFromModel(user.Flairs), user.Banned, GetRankFromElo(stats.Elo, season, context), stats.Elo, stats.Wins,
                stats.TotalGamesPlayed, stats.WinStreak, stats.BestWinStreak);
        }).ToList();
    }

    public UserInfo UpsertFromBeatKhanaToken(ClaimsPrincipal claims, string linkingUrl)
    {
        var platformId = claims.Claims.First(c => c.Type == "platformId").Value;
        var guid = claims.Claims.First(c => c.Type == "guid").Value;
        var globalName = claims.Claims.First(c => c.Type == "global_name").Value;
        var username = claims.Claims.First(c => c.Type == "username").Value;
        var avatarUrl = claims.Claims.First(c => c.Type == "avatarUrl").Value;
        
        if (claims.Claims.FirstOrDefault(i => i.Type == "guid" && i.Value != "") != null)
            return UpsertFromBeatKhana(platformId!, guid, globalName == "" ? username : globalName, avatarUrl, linkingUrl);

        if (platformId == "")
            throw new Exception("Token has no usable identity!");
        
        return UpsertPluginAccount(platformId, username);
    }

    public UserInfo UpsertFromBeatKhana(string platformId, string beatKhanaGuid, string userName, string avatarUrl, string linkingUrl)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetService<DataContext>()!;

        try
        {
            var user = context.Users.FirstOrDefault(p => p.PlatformId == platformId);

            if (user == null)
            {
                user = new User()
                {
                    Guid = Guid.NewGuid().ToString(),
                    PlatformId = platformId,
                    Username = userName,
                    AvatarUrl = avatarUrl,
                    BeatKhanaId = beatKhanaGuid,
                    Updated = DateTime.UtcNow,
                    Created = DateTime.UtcNow,
                    Permissions = []
                };
                
                context.Users.Add(user);
                
                CreateNewCompetitiveStatisticsIfNotExists(user, context);

                context.SaveChanges();

                return GetUserInfoFromUserModel(user)!;
            }

            user.Username = userName;
            user.AvatarUrl = avatarUrl;
            user.BeatKhanaId = beatKhanaGuid;
            
            CreateNewCompetitiveStatisticsIfNotExists(user, context);

            context.SaveChanges();

            return GetUserInfoFromUserModel(user)!;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to upsert user from BeatKhana");
            throw;
        }
    }

    public UserInfo UpsertPluginAccount(string platformId, string username)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var user = context.Users.Include(p => p.CompetetiveStatistics).FirstOrDefault(p => p.PlatformId == platformId);

        using var transaction = context.Database.BeginTransaction();

        try
        {
            if (user == null)
            {
                context.Users.Add(new User()
                {
                    Guid = Guid.NewGuid().ToString(),
                    PlatformId = platformId,
                    Username = username,
                    AvatarUrl = "https://cdn.scoresaber.com/avatars/oculus.png?v=1781213201",
                    Banned = false,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow,
                    Permissions = []
                });

                user = context.Users.Include(p => p.CompetetiveStatistics).First(p => p.PlatformId == platformId);

                CreateNewCompetitiveStatisticsIfNotExists(user, context);

                context.SaveChanges();

                return GetUserStatisticsByPlatformId(platformId)!;
            }

            user.AvatarUrl = "https://cdn.scoresaber.com/avatars/oculus.png?v=1781213201";
            user.Username = username;
            
            CreateNewCompetitiveStatisticsIfNotExists(user, context);

            context.SaveChanges();

            return GetUserStatisticsByPlatformId(platformId)!;
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to create user info for {platformId}");
            throw;
        }
    }

    private void CreateNewCompetitiveStatisticsIfNotExists(User user, DataContext context)
    {
        if (user.CompetetiveStatistics.Any(i => i.Season == configHelper.Season))
            return;

        context.CompetitiveStatistics.Add(new CompetetiveStatistics()
        {
            Guid = Guid.NewGuid().ToString(),
            BestWinStreak = 0,
            Elo = 1000,
            Season = configHelper.Season,
            TotalGamesPlayed = 0,
            WinStreak = 0,
            Wins = 0,
            User = user
        });
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

    public void AdjustElo(string platformId, int eloChange)
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
    
    public List<UserStatistics> GetAllUserStatistics(int season = -1)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var users = context.Users.Include(p => p.CompetetiveStatistics)
            .Include(p => p.Flairs)
            .Where(i => !i.Banned)
            .Where(i => i.CompetetiveStatistics.Any(j => j.Season == season))
            .ToArray();

        return users.Select(user =>
        {
            var stats = user.CompetetiveStatistics.First(k => k.Season == season);

            return new UserStatistics(user.Username, user.PlatformId, user.BeatKhanaId, user.AvatarUrl,
                GetFlairsFromModel(user.Flairs), user.Banned, GetRankFromElo(stats.Elo, season, context), stats.Elo, stats.Wins,
                stats.TotalGamesPlayed, stats.WinStreak, stats.BestWinStreak);
        }).ToList();
    }
    
    public UserInfo? GetUserInfoByPlatformId(string platformId)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        var user = context.Users.Include(user => user.Flairs).FirstOrDefault(i => i.PlatformId == platformId);

        if (user == null)
            return null;

        return GetUserInfoFromUserModel(user);
    }

    public UserStatistics? GetUserStatisticsByPlatformId(string platformId, int season = -1)
    {
        using var scope = scopeFactory.CreateScope();
        
        var context = scope.ServiceProvider.GetService<DataContext>()!;
        
        if (season == -1)
            season = configHelper.Season;
        
        var user = context.Users.Include(p => p.CompetetiveStatistics).Include(user => user.Flairs).FirstOrDefault(i => i.PlatformId == platformId);

        if (user == null)
            return null;
        
        var stats = user.CompetetiveStatistics.FirstOrDefault(i => i.Season == season);

        if (stats == null)
            return null;

        var rank = GetRankFromElo(stats.Elo, season, context);
        
        return new UserStatistics(user.Username, user.PlatformId, user.BeatKhanaId, user.AvatarUrl, GetFlairsFromModel(user.Flairs), user.Banned, rank, stats.Elo, stats.Wins, stats.TotalGamesPlayed, stats.WinStreak, stats.BestWinStreak);
    }

    private int GetRankFromElo(int elo, int season, DataContext context)
    {
        if (season == -1)
            season = configHelper.Season;
        
        return context.CompetitiveStatistics.Where(i => i.Season == season).Count(i => i.Elo > elo) + 1;
    }

    private static Flair[] GetFlairsFromModel(List<UserFlair> flair)
    {
        return flair.Select(i => new Flair(i.Name, i.Color, i.Permissions.ToArray())).ToArray();
    }

    private static UserInfo GetUserInfoFromUserModel(User user) => new(user.Username, user.PlatformId, user.BeatKhanaId, user.AvatarUrl, GetFlairsFromModel(user.Flairs), user.Banned);
}