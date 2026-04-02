using Microsoft.EntityFrameworkCore;
using DatabaseVersion.Api.Models;

namespace DatabaseVersion.Api.Data;

public class AppDbContext  : DbContext
{
    public AppDbContext (DbContextOptions <AppDbContext> options) : base(options)
    {
    }
        public DbSet<TodoItem> TodoItems => Set<TodoItem>();
        public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {


        //user entity mapping 

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users");
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.DisplayName).HasColumnName("display_name");
            entity.Property(e => e.Email).HasColumnName("email");
            // entity.Property(e => e.CreatedAt).HasColumnName("created_at");

            

        });
        //todo item entity mapping 
        modelBuilder.Entity<TodoItem>(entity =>
        {
            entity.ToTable("todo_item");
            

        
        modelBuilder.Entity<TodoItem>()
            .HasIndex(t => new { t.EntityId, t.Version })
            .IsUnique()
            .HasDatabaseName("IX_todo_items_entity_id_version");

        entity.Property(e => e.Id).HasColumnName("id");
        entity.Property(e => e.EntityId).HasColumnName("entity_id");
        entity.Property(e => e.Version).HasColumnName("version");
        entity.Property(e => e.Title).HasColumnName("title");
        entity.Property(e => e.Description).HasColumnName("description");
        entity.Property(e => e.IsCompleted).HasColumnName("is_completed");
        entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        entity.Property(e => e.Userid).HasColumnName("user_id");

        entity.HasOne(t => t.User)
            .WithMany(u => u.TodoItems)
            .HasForeignKey(t => t.Userid)
            .OnDelete(DeleteBehavior.Restrict);


        entity.HasIndex(t =>  new { t.EntityId, t.Version}).IsUnique();
        });
    }
    
}