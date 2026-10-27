namespace DocQuery.Api.Models;

// Simple request/response shapes used by the controllers.

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(string Token, string Email);

public record DocumentDto(int Id, string FileName, int PageCount, DateTime UploadedAt);

public record AskRequest(string Question, int? DocumentId);

// One piece of text found by vector search, plus where it came from.
public record SourceChunk(int DocumentId, string FileName, int PageNumber, string Content, double Distance);

// A piece of a page before it is saved.
public record TextChunk(int PageNumber, string Content);
