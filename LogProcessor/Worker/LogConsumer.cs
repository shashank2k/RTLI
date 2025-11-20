using System.Text;
using System.Text.Json;
using LogProcessor.Messaging;
using LogProcessor.Data;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;

namespace LogProcessor.Worker;

public class LogConsumer : BackgroundService
{
private readonly ILogger<LogConsumer> _logger;
private readonly RabbitConsumerFactory _factory;
private readonly LogRepository _repo;
private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

public LogConsumer(ILogger<LogConsumer> logger, RabbitConsumerFactory factory, LogRepository repo)
{
    _logger = logger;
    _factory = factory;
    _repo = repo;
}

protected override Task ExecuteAsync(CancellationToken stoppingToken)
{
    var channel = _factory.GetOrCreateChannel();

    var consumer = new EventingBasicConsumer(channel);
    consumer.Received += async (_, ea) =>
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.Span);
            if(json.Count() == 0) throw new InvalidDataException("No Data");
            var dto = JsonSerializer.Deserialize<LogIngestor.Models.LogDto>(json, _json);
            if (dto is null)
                throw new InvalidDataException("Invalid log JSON");
            _logger.LogInformation("Trying to insert in DB");
            await _repo.InsertAsync(dto, stoppingToken);

            channel.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message, routing to DLQ");
            try
            {
                // Send to DLQ with reason header
                var props = channel.CreateBasicProperties();
                props.Headers = new Dictionary<string, object?>
                {
                    ["reason"] = ex.GetType().Name,
                    ["error"] = Encoding.UTF8.GetBytes(ex.Message)
                };
                channel.BasicPublish(exchange: "", routingKey: "logs.dlq", basicProperties: props, body: ea.Body);
            }
            catch (Exception pubEx)
            {
                _logger.LogError(pubEx, "Failed to publish to DLQ");
            }
            finally
            {
                channel.BasicAck(ea.DeliveryTag, multiple: false); // avoid poison loop
            }
        }
    };

    channel.BasicConsume(queue: "logs.queue", autoAck: false, consumer: consumer);

    _logger.LogInformation("LogConsumer started and consuming from {Queue}", "logs.queue");
    return Task.CompletedTask;
}
}