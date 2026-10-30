using System.ComponentModel.DataAnnotations;
using CompCube_Models.Models.Map;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;

namespace CompCube_Server.Data.Schema;

[Index(nameof(ThreadId), IsUnique = true)]
public class PooledBeatmap
{
    [Required]
    [MaxLength(36)]
    [Key]
    public required string Guid { get; set; }
    
    [Required]
    [MaxLength(64)]
    public required string Hash { get; set; }
    
    [Required]
    public required VotingMap.Category Category { get; set; }
    
    [Required]
    public required VotingMap.DifficultyType Difficulty { get; set; }
    
    [Required]
    public required ulong PoolerId { get; set; }
    
    [Required]
    public required ulong ThreadId { get; set; }

    [Required] 
    public required AcceptanceStatus Status { get; set; } = AcceptanceStatus.InProgress;
    
    [Required]
    public required DateTime DateAdded { get; set; } = DateTime.UtcNow;

    public DateTime? DateVerdictDecided { get; set; } = null;


    public enum AcceptanceStatus
    {
        Accepted,
        Denied,
        InProgress
    }
}