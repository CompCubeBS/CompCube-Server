using System.ComponentModel.DataAnnotations;

namespace CompCube_Server.Data.Schema;

public class AuthState
{
    [Key]
    [MaxLength(64)]
    public required string StateHash { get; set; }
    
    [Required]
    public required string ReturnTo { get; set; }

    [Required] 
    public required ResponseModeType ResponseMode { get; set; } = ResponseModeType.Redirect;
    
    [Required]
    public required DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; } = null;
    
    [Required]
    public required DateTime CreatedAt { get; set; }

    public enum ResponseModeType
    {
        Redirect,
        Json
    }
}