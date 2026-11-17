using System.ComponentModel.DataAnnotations;
using CompCube_Models.Models.Map;

namespace CompCube_Server.Data.Schema;

public class Beatmap
{
    [Key]
    [MaxLength(36)]
    public required string Guid { get; set; }
    
    [Required]
    [MaxLength(64)]
    public string Hash { get; set; }
    
    [MaxLength(128)]
    public string? SongTitle { get; set; }
    
    [MaxLength(64)]
    public string? SongAuthor { get; set; }
    
    [MaxLength(64)]
    public string? SongCoverUrl { get; set; }
    
    [MaxLength(64)]
    public string? MapperName { get; set; }
    
    [Required]
    public int MaxScore { get; set; }
    
    [Required]
    public CCBeatmapData.Category Category { get; set; }
    
    [Required]
    public CCBeatmapData.DifficultyType Difficulty { get; set; }
    
    [Required]
    public int DurationSeconds { get; set; }
    
    [Required]
    public List<Pool> Pools { get; set; } = [];
    
    [Required]
    public List<Score> Scores { get; set; } = [];
}