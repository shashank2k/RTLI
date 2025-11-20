using LogProcessor;
using LogProcessor.Data;
using LogProcessor.Messaging;
using LogProcessor.Worker;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
.MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
.Enrich.WithProperty("Application", "LogProcessor")
.WriteTo.Console()
.CreateLogger();
try
{
Log.Information("Starting LogProcessor");
var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSerilog();

// Options
builder.Services.Configure<RabbitOptions>(builder.Configuration.GetSection("RabbitMq"));
builder.Services.Configure<DbOptions>(builder.Configuration.GetSection("Postgres"));

// Infrastructure
builder.Services.AddSingleton<RabbitConsumerFactory>();
builder.Services.AddSingleton<LogRepository>();

// Worker
builder.Services.AddHostedService<LogConsumer>();
builder.Services.AddHostedService<DlqProcessor>();

var host = builder.Build();

await host.RunAsync();
}
catch (Exception ex)
{
Log.Fatal(ex, "Processor failed");
}
finally
{
await Log.CloseAndFlushAsync();
}



// var builder = Host.CreateApplicationBuilder(args);
// builder.Services.AddHostedService<Worker>();

// var host = builder.Build();
// host.Run();
