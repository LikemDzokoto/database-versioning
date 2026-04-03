using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;


namespace DatabaseVersion.Api.Models; 


[Table("users")]
public class User
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }
    
    [Column("display_name")]
    public required string DisplayName { get; set; }
    
    [Column("email")]
    public required string Email { get; set; }
    
    //Naviation proprty of the EF collection 
    public ICollection<TodoItem> TodoItems { get; set; } = new List<TodoItem>();
    
    // [Column("created_at")]
    // public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}