namespace LogProcessor.Messaging;
public class RabbitOptions
{
public string HostName { get; set; } = "localhost";
public int Port { get; set; } = 5672;
public string UserName { get; set; } = "guest";
public string Password { get; set; } = "guest";
public string Queue { get; set; } = "logs.queue";
public string Dlq { get; set; } = "logs.dlq";
public ushort Prefetch { get; set; } = 200;
}