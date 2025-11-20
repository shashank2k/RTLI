using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LogIngestor.Messaging;

public class RabbitPublisher : IDisposable
{
private readonly RabbitOptions _opts;
private readonly IConnection _connection;
private readonly IModel _channel;
private readonly IBasicProperties _props;
public bool IsHealthy => _connection.IsOpen && _channel.IsOpen;

public RabbitPublisher(RabbitConnectionFactory factory, IOptions<RabbitOptions> opts)
{
    _opts = opts.Value;
    _connection = factory.Get();
    _channel = _connection.CreateModel();

    _channel.QueueDeclare(queue: _opts.Queue,
                          durable: true,
                          exclusive: false,
                          autoDelete: false,
                          arguments: null);

    if (_opts.PublisherConfirms)
    {
        _channel.ConfirmSelect();
    }

    _props = _channel.CreateBasicProperties();
    _props.DeliveryMode = 2; // persistent
    _props.ContentType = "application/json";
}

public Task PublishAsync(string json)
{
    var body = Encoding.UTF8.GetBytes(json);
    _channel.BasicPublish(exchange: "",
                          routingKey: _opts.Queue,
                          mandatory: true,
                          basicProperties: _props,
                          body: body);

    if (_opts.PublisherConfirms)
    {
        // wait synchronously for simplicity; switch to async confirms if using higher throughput
        if (!_channel.WaitForConfirms(TimeSpan.FromSeconds(5)))
            throw new IOException("RabbitMQ publish confirm timed out");
    }

    return Task.CompletedTask;
}

public void Dispose()
{
    try { _channel?.Close(); } catch { }
    try { _connection?.Close(); } catch { }
    _channel?.Dispose();
    _connection?.Dispose();
}
}