using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CompCube_Models.Models.ClientData;

namespace CompCube_Server.Data.Schema;

public class UserFlair
{
    [Key]
    public required string Guid { get; set; }
    
    [Required]
    public required string Name { get; set; }
    
    [Required]
    public required string Color { get; set; }

    [Required] public required List<string> Permissions { get; set; } = [];

    public List<User> Users { get; set; } = [];
}