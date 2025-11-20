using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LogProcessor.Messaging;

public class RabbitConsumerFactory : IDisposable
{
private readonly RabbitOptions _opts;
private readonly IConnection _conn;
private RabbitMQ.Client.IModel? _channel;

public RabbitConsumerFactory(IOptions<RabbitOptions> opts)
{
    _opts = opts.Value;
    var factory = new ConnectionFactory
    {
        HostName = _opts.HostName,
        Port = _opts.Port,
        UserName = _opts.UserName,
        Password = _opts.Password,
        AutomaticRecoveryEnabled = true,
        NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
        TopologyRecoveryEnabled = true
    };
    _conn = factory.CreateConnection("log-processor");
}

public RabbitMQ.Client.IModel GetOrCreateChannel()
{
    if (_channel is { IsOpen: true }) return _channel;
    _channel?.Dispose();
    _channel = _conn.CreateModel();

    // Declare main and DLQ
    _channel.QueueDeclare(_opts.Queue, durable: true, exclusive: false, autoDelete: false, arguments: null);
    _channel.QueueDeclare(_opts.Dlq, durable: true, exclusive: false, autoDelete: false, arguments: null);

    // Prefetch for parallelism/backpressure
    _channel.BasicQos(0, _opts.Prefetch, global: false);

    return _channel;
}

public void Dispose()
{
    try { _channel?.Close(); } catch { }
    try { _conn.Close(); } catch { }
    _channel?.Dispose();
    _conn.Dispose();
}
}