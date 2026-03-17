using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DatabaseVersion.Api.Data;
using DatabaseVersion.Api.Models;

namespace DatabaseVersion.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodoController : ControllerBase
{
    private readonly AppDbContext _context;

    public TodoController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetCurrentTodos()
    {
        return await _context.TodoItems.ToListAsync();
    }

    [HttpGet("{entityId}/history")]
    public async Task<ActionResult<IEnumerable<object>>> GetHistory(Guid entityId)
    {
        var history = await _context.TodoItems
            .FromSqlRaw(@"
            SELECT DISTINCT ON (version) * FROM table_version.public_todo_item_revision 
            WHERE entity_id = {0} 
            ORDER BY version DESC, updated_at DESC", entityId)
            .AsNoTracking() 
            .ToListAsync();

        return Ok(history);
    }

    [HttpPost]
    public async Task<ActionResult<TodoItem>> CreateTodo([FromBody] TodoCreateRequest request)
    {

        await _context.Database.ExecuteSqlRawAsync("SELECT table_version.ver_create_revision('Initial creation')");

        var todo = new TodoItem
        {
            EntityId = Guid.NewGuid(),
            Version = 1,
            Title = request.Title,
            Description = request.Description,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TodoItems.Add(todo);
        await _context.SaveChangesAsync();
        return Ok(todo);
    }

    [HttpPut("{entityId}")]
    public async Task<IActionResult> UpdateTodo(Guid entityId, [FromBody] TodoUpdateRequest request)
    {
        var todo = await _context.TodoItems
            .Where(t => t.EntityId == entityId)
            .FirstOrDefaultAsync();

        if (todo == null) return NotFound();

        // Use Interpolated version to handle the dynamic string safely without '@' syntax issues
        var revisionNote = $"Update via API for {request.NewTitle}";
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT table_version.ver_create_revision({revisionNote})");

        todo.Title = request.NewTitle;
        todo.IsCompleted = request.IsCompleted;
        todo.Version += 1;
        todo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(todo);
    }

}