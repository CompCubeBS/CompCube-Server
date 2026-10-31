using CompCube_Models.Models.ClientData;
using CompCube_Models.Models.Map;
using CompCube_Server.Data.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CompCube_Server.Data;

public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
{
    public DbSet<Beatmap> Beatmaps => Set<Beatmap>();
    public DbSet<CompetetiveStatistics> CompetitiveStatistics => Set<CompetetiveStatistics>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<Pool> Pools => Set<Pool>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Score> Scores => Set<Score>();
    public DbSet<UserFlair> UserFlairs => Set<UserFlair>();
    public DbSet<AuthState> AuthStates => Set<AuthState>();
    public DbSet<PooledBeatmap> PooledBeatmaps => Set<PooledBeatmap>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(builder =>
        {
            builder.Log(RelationalEventId.PendingModelChangesWarning);
        });
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthState>(entity =>
        {
            entity.ToTable("oauth_states");
        });

        modelBuilder.Entity<PooledBeatmap>(entity =>
        {
            entity.ToTable("pooled_beatmaps");
        });
        
        modelBuilder.Entity<Beatmap>(entity =>
        {
            entity.ToTable("beatmaps");
        });
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");

            entity.Property(u => u.Permissions).HasDefaultValue(new List<string>());
        });

        modelBuilder.Entity<Pool>(entity =>
        { 
            entity.ToTable("pools");
        });

        modelBuilder.Entity<Score>(entity =>
        {
            entity.ToTable("scores");

            entity.HasOne(p => p.Beatmap)
                .WithMany(p => p.Scores)
                .HasForeignKey(p => p.BeatmapGuid)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(p => p.Owner)
                .WithMany(p => p.Scores)
                .HasForeignKey(p => p.OwnerGuid)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CompetetiveStatistics>(entity =>
        {
            entity.ToTable("competetive_statistics");
            
            entity.HasOne(p => p.User)
                .WithMany(p => p.CompetetiveStatistics)
                .HasForeignKey(p => p.UserGuid)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MatchResult>(entity =>
        {
            entity.ToTable("match_results");
            
            entity.HasMany(p => p.Scores)
                .WithOne(p => p.MatchResult)
                .HasForeignKey(p => p.MatchResultGuid)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(p => p.Winner)
                .WithMany()
                .HasForeignKey(p => p.WinnerGuid)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(p => p.Loser)
                .WithMany()
                .HasForeignKey(p => p.LoserGuid)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(p => p.FirstPicker)
                .WithMany()
                .HasForeignKey(p => p.FirstPickerGuid)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserFlair>(entity =>
        {
            entity.ToTable("user_flairs");
        });

        modelBuilder.Entity<User>().HasData(new User
        {
            Guid = "00000000-0000-0000-0000-000000000000",
            Username = "Béla",
            AvatarUrl = "https://cdn.beatkhana.com/view/d8d943968d33a36b00841583ec404c46.jpg",
            Banned = false,
            BeatKhanaId = "bd4e7d4a-dcf0-484c-a065-bbccc78a064b",
            PlatformId = "76561199003743737",
            Created = DateTimeOffset.UtcNow,
            Updated = DateTimeOffset.UtcNow,
            Permissions = ["role:admin", "role:dev", "role:moderator", "role:pooler"],
            Flairs = []
        }, new User
        {
            Guid = "00000000-0000-0000-0000-000000000001",
            Username = "ShyyLuna",
            AvatarUrl = "https://cdn.beatkhana.com/view/b4fce8548f05e9b07dc376cb06b70aed.webp",
            Banned = false,
            BeatKhanaId = "583e1518-9651-4b0a-a568-67c6fae73128",
            PlatformId = "76561199016011545",
            Created = DateTimeOffset.UtcNow,
            Updated = DateTimeOffset.UtcNow,
            Permissions = ["role:dev", "role:moderator"],
            Flairs = []
        });
    }
}