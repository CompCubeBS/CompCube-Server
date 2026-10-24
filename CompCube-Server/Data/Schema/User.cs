using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace CompCube_Server.Data.Schema;

[Index(nameof(PlatformId), IsUnique = true)]
public class User
{
    [Key]
    [MaxLength(36)]
    public required string Guid { get; set; }

    [MaxLength(60)]
    public string? BeatKhanaId { get; set; } = null;
    
    [Required]
    [MaxLength(50)]
    public required string PlatformId { get; set; }
    
    [Required]
    [MaxLength(50)]
    public required string Username { get; set; }

    [Required]
    [MaxLength(128)]
    public required string AvatarUrl { get; set; } = "https://cdn.scoresaber.com/avatars/oculus.png?v=1781213201";

    [Required]
    public bool Banned { get; set; } = false;

    public string? FlairGuid { get; set; } = null;

    public UserFlair? Flair { get; set; } = null;
    
    [Required]
    public List<Score> Scores { get; set; } = [];
    
    [Required]
    public List<CompetetiveStatistics> CompetetiveStatistics { get; set; } = [];
    
    [Required]
    public DateTime Created { get; set; }
    
    [Required]
    public DateTime Updated { get; set; }
}