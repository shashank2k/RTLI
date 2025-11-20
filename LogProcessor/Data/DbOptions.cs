namespace LogProcessor.Data;
public class DbOptions
{
public string ConnectionString { get; set; } = "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=logs";
}