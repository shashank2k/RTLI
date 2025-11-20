using System.Text.Json;
using Dapper;
using Npgsql;
using Microsoft.Extensions.Options;

namespace LogProcessor.Data;

public class LogRepository
{
private readonly string _cs;
private readonly ILogger<LogRepository> _logger;
private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
public LogRepository(IOptions<DbOptions> opts,ILogger<LogRepository> logger)
    {
        _cs = opts.Value.ConnectionString;
        _logger = logger;
    }

public async Task InsertAsync(LogIngestor.Models.LogDto dto, CancellationToken ct)
{
    _logger.LogInformation("Connecting db ");

    const string sql = @"INSERT INTO logs (ts, level, app, message, user_id, trace_id, context)
VALUES (@ts, @level, @app, @message, @user_id, @trace_id, @context::jsonb);";    
    await using var conn = new NpgsqlConnection(_cs);
     _logger.LogInformation("Excuting DB command");
        var contextJson = new
        {
            ts = dto.Timestamp,
            level = dto.Level,
            app = dto.App,
            message = dto.Message,
            user_id = dto.UserId,
            trace_id = dto.TraceId,
            context = dto.Context is null ? null : JsonSerializer.Serialize(dto.Context, JsonOpts)
        };
    _logger.LogInformation("Inserting log entry into database"+JsonSerializer.Serialize(contextJson, JsonOpts));
    var affected = await conn.ExecuteAsync(new CommandDefinition(
        sql,contextJson,
        cancellationToken: ct));
        _logger.LogInformation("Affected rows: "+affected);
        var data = await conn.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM logs;", cancellationToken: ct));
        _logger.LogInformation("Total rows in logs table: "+data);

}
}