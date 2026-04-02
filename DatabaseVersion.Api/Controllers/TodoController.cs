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


    [HttpPost("create-user")]
    public async Task<ActionResult<User>> CreateUser([FromBody] UserCreateRequest request)

    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = request.DisplayName,
            Email = request.Email
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return Ok(user);
    }


    [HttpGet("users")]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        return await _context.Users.ToListAsync();
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetCurrentTodos()
    {
        return await _context.TodoItems.ToListAsync();
    }


    [HttpGet("history/global")]
    public async Task<ActionResult<IEnumerable<TodoHistoryResponse>>> GetAllHistory(
        [FromQuery] DateTime? since,
        [FromQuery] Guid? entityId)
    {


        // If 'since' is null, we use DateTime.MinValue to get everything
        var filterTime = since ?? DateTime.MinValue;

        var sql = @"
    SELECT 
        h.version AS ""Version"", 
        h.title AS ""Title"", 
        h.description AS ""Description"", 
        h.is_completed AS ""IsCompleted"", 
        h.user_id AS ""UserId"", 
        u.display_name AS ""DisplayName"", 
        h.updated_at AS ""UpdatedAt"", 
        r.revision_time AS ""RevisionTime"", 
        r.comment AS ""RevisionComment""
    FROM table_version.public_todo_item_revision h
    JOIN table_version.revision r ON h._revision_created = r.id
    LEFT JOIN users u ON h.user_id = u.id
    WHERE r.revision_time >= {0}
    ORDER BY r.revision_time DESC";

        var history = await _context.Database
            .SqlQueryRaw<TodoHistoryResponse>(sql, filterTime)
            .ToListAsync();

        return Ok(history);
    }
    // [HttpGet("{entityId}/history")]
    // public async Task<ActionResult<IEnumerable<object>>> GetHistory(Guid entityId)
    // {
    //     var history = await _context.TodoItems
    //         .FromSqlRaw(@"
    //         SELECT DISTINCT ON (version) * FROM table_version.public_todo_item_revision 
    //         WHERE entity_id = {0} 
    //         ORDER BY version DESC, updated_at DESC", entityId)
    //         .AsNoTracking() 
    //         .ToListAsync();

    //     return Ok(history);
    // }


    [HttpGet("{entityId}/history")]
    public async Task<ActionResult<IEnumerable<object>>> GetHistory(Guid entityId)
    {
        var sql = @"
        SELECT 
            h.version, h.title, h.is_completed, h.description,
            r.revision_time as ChangeTime, r.comment as ChangeReason,
            u.display_name as Author
        FROM table_version.public_todo_item_revision h
        JOIN table_version.revision r ON h._revision_created = r.id
        JOIN public.users u ON h.user_id = u.id
        WHERE h.entity_id = {0}
        ORDER BY h.version DESC";

        var history = await _context.Database.SqlQueryRaw<dynamic>(sql, entityId).ToListAsync();


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
            Userid = request.UserId,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TodoItems.Add(todo);
        await _context.SaveChangesAsync();

        await _context.Entry(todo).Reference(t => t.User).LoadAsync();
        return Ok(todo);
    }

    [HttpPut("{entityId}")]
    public async Task<IActionResult> UpdateTodo(Guid entityId, [FromBody] TodoUpdateRequest request)
    {
        var todo = await _context.TodoItems
            .Include(t => t.User) // Include the User navigation property to access user details for auditing
            .Where(t => t.EntityId == entityId)
            .FirstOrDefaultAsync();

        if (todo == null) return NotFound();

        //audit the user making the changes 

        var revisionNote = $"Update by user {request.UserId}: {request.NewTitle}";
        await _context.Database.ExecuteSqlInterpolatedAsync($"SELECT table_version.ver_create_revision({revisionNote})");

        todo.Title = request.NewTitle;
        todo.IsCompleted = request.IsCompleted;
        todo.Userid = request.UserId; // Update the user ID to reflect who made the change
        todo.Version += 1;
        todo.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _context.Entry(todo).Reference(t => t.User).LoadAsync(); // Load the User details for the response
        return Ok(todo);
    }

}


public record UserCreateRequest(string DisplayName, string Email);


public record TodoHistoryResponse(
    int Version,
    string Title,
    string? Description,
    bool IsCompleted,
    Guid? UserId,
    string? DisplayName,
    DateTime UpdatedAt,
    DateTime RevisionTime,
    string? RevisionComment
);