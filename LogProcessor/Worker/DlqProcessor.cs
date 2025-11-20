using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using LogProcessor.Messaging;

namespace LogProcessor.Worker;

public class DlqProcessor : BackgroundService
{
private readonly ILogger<DlqProcessor> _logger;
private readonly RabbitConsumerFactory _factory;// Tune these as needed
private const string MainQueue = "logs.queue";
private const string DlqQueue = "logs.dlq";
private const string RetryQueue = "logs.retry.5s";
private const string ParkingQueue = "logs.parking";
private const int MaxRetries = 5;

public DlqProcessor(ILogger<DlqProcessor> logger, RabbitConsumerFactory factory)
{
    _logger = logger;
    _factory = factory;
}

protected override Task ExecuteAsync(CancellationToken stoppingToken)
{
    var channel = _factory.GetOrCreateChannel();

    // Declare DLQ, retry, and parking queues.
    // Retry queue has a TTL and DLX back to the main queue.[1]
    channel.QueueDeclare(DlqQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
    channel.QueueDeclare(MainQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);
    channel.QueueDeclare(ParkingQueue, durable: true, exclusive: false, autoDelete: false, arguments: null);

    var retryArgs = new Dictionary<string, object>
    {
        ["x-message-ttl"] = 5000, // 5s delay
        ["x-dead-letter-exchange"] = "", // default exchange
        ["x-dead-letter-routing-key"] = MainQueue
    };
    channel.QueueDeclare(RetryQueue, durable: true, exclusive: false, autoDelete: false, arguments: retryArgs);

    channel.BasicQos(0, 100, false);

    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += (_, ea) =>
    {
        try
        {
            // Extract headers set by the main consumer when it DLQ’d the message.[1]
            var headers = ea.BasicProperties?.Headers ?? new Dictionary<string, object>();
            var reason = GetHeaderString(headers, "reason") ?? "unknown";
            var errorText = GetHeaderBytes(headers, "error") is { Length: > 0 } eb ? Encoding.UTF8.GetString(eb) : null;
            var retryCount = ParseRetryCount(headers);

            var body = ea.Body.ToArray();
            var preview = Preview(body);

            _logger.LogWarning("DLQ message: reason={Reason}, retries={Retries}, size={Size}, preview={Preview}", reason, retryCount, body.Length, preview);

            // Classify: transient vs permanent. You may refine this set.[1]
            var isTransient = reason.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
                              || reason.Contains("NpgsqlException", StringComparison.OrdinalIgnoreCase)
                              || reason.Contains("SocketException", StringComparison.OrdinalIgnoreCase);

            if (isTransient && retryCount < MaxRetries)
            {
                // Publish to retry queue with incremented retry-count; it will dead-letter back to main after TTL.[1]
                var props = channel.CreateBasicProperties();
                props.Persistent = true;
                props.Headers = CopyHeaders(headers);
                props.Headers["retry-count"] = Encoding.UTF8.GetBytes((retryCount + 1).ToString());

                channel.BasicPublish(exchange: "", routingKey: RetryQueue, mandatory: false, basicProperties: props, body: body);
                _logger.LogInformation("Requeued to {Queue} with retry-count={Count}", RetryQueue, retryCount + 1);
            }
            else
            {
                // Park message for offline analysis (permanent failure or retries exhausted).[1]
                var props = channel.CreateBasicProperties();
                props.Persistent = true;
                props.Headers = CopyHeaders(headers);
                props.Headers["final-reason"] = Encoding.UTF8.GetBytes(reason);
                if (errorText is not null)
                    props.Headers["final-error"] = Encoding.UTF8.GetBytes(errorText);

                channel.BasicPublish(exchange: "", routingKey: ParkingQueue, mandatory: false, basicProperties: props, body: body);
                _logger.LogWarning("Parked message to {Queue}", ParkingQueue);
            }

            // Ack the DLQ message to avoid loops.[1]
            channel.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process DLQ message; acking to avoid poison loops");
            channel.BasicAck(ea.DeliveryTag, multiple: false);
        }
    };

    channel.BasicConsume(queue: DlqQueue, autoAck: false, consumer: consumer);
    _logger.LogInformation("DlqProcessor consuming from {Queue}", DlqQueue);

    return Task.CompletedTask;
}

private static string? GetHeaderString(IDictionary<string, object> headers, string key)
{
    if (!headers.TryGetValue(key, out var val) || val is null) return null;
    if (val is byte[] b) return Encoding.UTF8.GetString(b);
    if (val is ReadOnlyMemory<byte> rom) return Encoding.UTF8.GetString(rom.ToArray());
    return val.ToString();
}

private static byte[]? GetHeaderBytes(IDictionary<string, object> headers, string key)
{
    if (!headers.TryGetValue(key, out var val) || val is null) return null;
    if (val is byte[] b) return b;
    if (val is ReadOnlyMemory<byte> rom) return rom.ToArray();
    var s = val.ToString();
    return s is null ? null : Encoding.UTF8.GetBytes(s);
}

private static int ParseRetryCount(IDictionary<string, object> headers)
{
    var s = GetHeaderString(headers, "retry-count");
    return int.TryParse(s, out var n) ? n : 0;
}

private static IDictionary<string, object> CopyHeaders(IDictionary<string, object> headers)
{
    var copy = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    foreach (var kv in headers)
    {
        if (kv.Value is ReadOnlyMemory<byte> rom) copy[kv.Key] = rom.ToArray();
        else copy[kv.Key] = kv.Value!;
    }
    return copy;
}

private static string Preview(byte[] body)
{
    if (body.Length == 0) return "<empty>";
    var json = Encoding.UTF8.GetString(body);
    return json.Length > 120 ? json.Substring(0, 120) + "..." : json;
}
}