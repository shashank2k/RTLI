using System;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace LogIngestor.Messaging;

public class RabbitConnectionFactory
{
private readonly RabbitOptions _opts;
private readonly Lazy<IConnection> _conn;
public RabbitConnectionFactory(IOptions<RabbitOptions> opts)
{
    _opts = opts.Value;
    _conn = new Lazy<IConnection>(() =>
    {
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
        return factory.CreateConnection("log-ingestor");
    }, isThreadSafe: true);
}

public IConnection Get() => _conn.Value;
}