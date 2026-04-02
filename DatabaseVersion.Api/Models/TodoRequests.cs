namespace DatabaseVersion.Api.Models;

// public record TodoCreateRequest(string Title, string? Description , Guid UserId);

// public record TodoUpdateRequest(string NewTitle, bool IsCompleted , Guid UserId);


public class TodoCreateRequest
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required Guid UserId { get; set; }
}

public class TodoUpdateRequest
{
    public required string NewTitle { get; set; }
    public bool IsCompleted { get; set; }
    public required Guid UserId { get; set; }
}
