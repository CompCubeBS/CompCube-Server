using CompCube_Models.Models.Map;
using CompCube_Server.Data.Schema;
using Microsoft.EntityFrameworkCore;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthState>(entity =>
        {
            entity.ToTable("oauth_states");
        });
        
        modelBuilder.Entity<Beatmap>(entity =>
        {
            entity.ToTable("beatmaps");
        });
        
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            
            entity.HasOne(p => p.Flair)
                .WithMany()
                .HasForeignKey(u => u.FlairGuid)
                .OnDelete(DeleteBehavior.SetNull);
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
    }
}