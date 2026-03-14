using System.ComponentModel.DataAnnotations.Schema;

namespace DatabaseVersion.Api.Models;

public class TodoItem
{
    public int Id { get; set; }
    public Guid EntityId { get; set; }
    
    public int Version { get; set; }
    
    public required string Title { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
    
    public bool IsCurrent { get; set; }
}