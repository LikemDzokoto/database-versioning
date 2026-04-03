using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace mssql_database_version.Models;

[Table("todo_item")]
public class TodoItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Column("entity_id")]
    public Guid EntityId { get; set; } = Guid.NewGuid();

    [Required]
    [Column("version")]
    public int Version { get; set; } = 1;

    [Required]
    [MaxLength(255)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("is_completed")]
    public bool IsCompleted { get; set; } = false;

    [Required]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("UserId")]
    public User? User { get; set; }
}