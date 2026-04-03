using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace mssql_database_version.Models;


[Table("user")]

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();


    [Required]
    [MaxLength(255)]
    [Column("display_name")]
    public string DisplayName { get; set; } = string.Empty;


    [Required]
    [MaxLength(255)]
    [EmailAddress]
    [Column("email")]
    public string Email { get; set; } = string.Empty;

     public ICollection<TodoItem> TodoItems { get; set; } = new List<TodoItem>();
}