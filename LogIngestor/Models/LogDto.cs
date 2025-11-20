namespace LogIngestor.Models;

public record LogDto(
DateTimeOffset Timestamp,
string Level,
string Message,
string App,
string? UserId,
string? TraceId,
object? Context);