using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;


namespace DatabaseVersion.Api.Models;

[Table("todo_items")]
public class TodoItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("entity_id")]
    public Guid EntityId { get; set; }
    
    [Column("version")]
    public int Version { get; set; }
    
    [Column("title")]
    public required string Title { get; set; }
    [Column("description")]
    public string? Description { get; set; }

    
    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    

}