using Microsoft.EntityFrameworkCore;
using DatabaseVersion.Api.Models;

namespace DatabaseVersion.Api.Data;

public class AppDbContext  : DbContext
{
    public AppDbContext (DbContextOptions <AppDbContext> options) : base(options)
    {
    }
        public DbSet<TodoItem> TodoItems => Set<TodoItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TodoItem>()
        .HasIndex(t => new {t.EntityId, t.Version})
        .IsUnique();
    }
    
}