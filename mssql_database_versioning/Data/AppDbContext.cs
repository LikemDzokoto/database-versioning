using Microsoft.EntityFrameworkCore;
using mssql_database_version.Models; 

namespace mssql_database_version.Data;


public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<TodoItem> TodoItems { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //map the user table to the lowercase "user" table in the database
        modelBuilder.Entity<User>().ToTable("user");


        //map  the todoitem and enable temporal table support 
        modelBuilder.Entity<TodoItem>(entity => 
        {
            entity.ToTable("todo_item", b => b.IsTemporal(t => 
            {
                t.UseHistoryTable("todo_item_history");
                t.HasPeriodStart("SysStart");
                t.HasPeriodEnd("SysEnd");
            })); 
        });
}
}