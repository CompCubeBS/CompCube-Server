using System.ComponentModel.DataAnnotations;

namespace CompCube_Server.Data.Schema;

public class Pool
{
    [Key]
    public required int Id { get; set; }

    [Required]
    public List<Beatmap> Maps { get; set; } = [];

    [Required] public bool Active { get; set; } = false;
}