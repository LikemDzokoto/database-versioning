namespace DatabaseVersion.Api.Models;

public record TodoCreateRequest(string Title, string? Description);

public record TodoUpdateRequest(string NewTitle, bool IsCompleted);
