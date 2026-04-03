using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using mssql_database_version.Data;
using mssql_database_version.Models;

namespace mssql_database_version.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TodoController : ControllerBase
{
    private readonly AppDbContext _context;

    public TodoController(AppDbContext context)
    {
        _context = context;
    }

    // GET: api/todo
    // Using this as the primary GET to include user data
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodos()
    {
        return await _context.TodoItems.Include(t => t.User).ToListAsync();
    }

    // POST: api/todo
    [HttpPost]
    public async Task<ActionResult<TodoItem>> PostTodo(TodoItem todo)
    {
        todo.UpdatedAt = DateTime.UtcNow;
        todo.Version = 1;

        _context.TodoItems.Add(todo);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTodos), new { id = todo.Id }, todo);
    }

    // POST: api/todo/user
    // Explicit route added to fix the 405 Method Not Allowed error
    [HttpPost("user")]
    public async Task<ActionResult<User>> PostUser(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok(user);
    }

    // PUT: api/todo/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutTodo(int id, TodoItem todo)
    {
        if (id != todo.Id) return BadRequest();

        var existingItem = await _context.TodoItems.FindAsync(id);
        if (existingItem == null) return NotFound();

        existingItem.Title = todo.Title;
        existingItem.Description = todo.Description;
        existingItem.IsCompleted = todo.IsCompleted;
        existingItem.Version += 1;
        existingItem.UpdatedAt = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.TodoItems.Any(e => e.Id == id)) return NotFound();
            throw;
        }

        return Ok(existingItem);
    }

    // DELETE: api/todo/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTodo(int id)
    {
        var todo = await _context.TodoItems.FindAsync(id);
        if (todo == null) return NotFound();

        _context.TodoItems.Remove(todo);
        await _context.SaveChangesAsync();

        return NoContent();
    }


    // GET: api/todo/history/5
    [HttpGet("history/{id}")]
    public async Task<ActionResult<IEnumerable<object>>> GetTodoHistory(int id)
    {
        var history = await (from todo in _context.TodoItems.TemporalAll()
                             join user in _context.Users on todo.UserId equals user.Id
                             where todo.Id == id
                             select new
                             {
                                 todo.Id,
                                 todo.Title,
                                 todo.Description,
                                 todo.Version,
                                 todo.IsCompleted,
                                 todo.UpdatedAt,
                                 ChangedBy = new
                                 {
                                     DisplayName = user.DisplayName ?? "Unknown User",
                                     Email = user.Email ?? "No Email"
                                 },
                                 ValidFrom = EF.Property<DateTime>(todo, "SysStart"),
                                 ValidTo = EF.Property<DateTime>(todo, "SysEnd")
                             })
                             .OrderBy(t => t.ValidFrom)
                             .ToListAsync();

        if (history == null || !history.Any()) return NotFound();

        return Ok(history);
    }

    [HttpGet("history/{id}/range")]
    public async Task<ActionResult<IEnumerable<object>>> GetTodoHistoryByRange(
    int id,
    [FromQuery] DateTime from,
    [FromQuery] DateTime to)
    {
        // Method syntax is more robust for Temporal queries
        var history = await _context.TodoItems
            .TemporalBetween(from, to)
            .Where(t => t.Id == id)
            .Join(_context.Users,
                todo => todo.UserId,
                user => user.Id,
                (todo, user) => new { todo, user }) // Join Todo with User
            .Select(x => new
            {
                x.todo.Id,
                x.todo.Title,
                x.todo.Description,
                x.todo.Version,
                x.todo.IsCompleted,
                x.todo.UpdatedAt,
                ChangedBy = new
                {
                    DisplayName = x.user.DisplayName ?? "Unknown User",
                    Email = x.user.Email ?? "No Email"
                },
                // Metadata for your Thesis Audit Trail
                ValidFrom = EF.Property<DateTime>(x.todo, "SysStart"),
                ValidTo = EF.Property<DateTime>(x.todo, "SysEnd")
            })
            .OrderBy(t => t.ValidFrom)
            .ToListAsync();

        if (history == null || !history.Any())
            return NotFound($"No snapshots found for ID {id} between {from} and {to}");

        return Ok(history);
    }
}