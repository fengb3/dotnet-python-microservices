using StackExchange.Redis;
using Microservices;
using Google.Protobuf;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add hosted service for consuming results
builder.Services.AddHostedService<ResultConsumerService>();

builder.AddRedisClient(connectionName: "redis");

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

string[] summaries = ["Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"];

app.MapGet("/", () => "API service is running. Navigate to /weatherforecast to see sample data. Use POST /task to submit a task.");

app.MapPost("/task", async (TaskRequest request, IConnectionMultiplexer redis, ILogger<Program> logger) =>
{
    var db = redis.GetDatabase();
    
    // Create task message
    var taskMsg = new TaskMessage
    {
        TaskId = Guid.NewGuid().ToString(),
        TaskType = request.TaskType ?? "default",
        Data = request.Data ?? "",
        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
    };
    
    // Serialize to protobuf
    byte[] messageBytes = taskMsg.ToByteArray();
    
    // Publish to Redis Stream
    await db.StreamAddAsync("tasks", [
        new NameValueEntry("data", messageBytes)
    ]);
    
    logger.LogInformation("Published task {TaskId} of type {TaskType}", taskMsg.TaskId, taskMsg.TaskType);
    
    return Results.Ok(new { taskId = taskMsg.TaskId, message = "Task submitted successfully" });
})
.WithName("SubmitTask");


app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapDefaultEndpoints();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

record TaskRequest(string? TaskType, string? Data);

// Background service to consume results from Redis Stream
class ResultConsumerService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ResultConsumerService> _logger;
    private const string ResultStream = "results";
    private const string ConsumerGroup = "api-consumers";
    private const string ConsumerName = "api-1";

    public ResultConsumerService(IConnectionMultiplexer redis, ILogger<ResultConsumerService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _redis.GetDatabase();
        
        // Create consumer group (ignore if already exists)
        try
        {
            await db.StreamCreateConsumerGroupAsync(ResultStream, ConsumerGroup, "0", createStream: true);
            _logger.LogInformation("Created consumer group '{ConsumerGroup}'", ConsumerGroup);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            _logger.LogInformation("Consumer group '{ConsumerGroup}' already exists", ConsumerGroup);
        }
        
        _logger.LogInformation("Starting to consume results from stream '{ResultStream}'", ResultStream);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Read from stream
                var results = await db.StreamReadGroupAsync(
                    ResultStream,
                    ConsumerGroup,
                    ConsumerName,
                    ">",
                    count: 1
                );
                
                if (results.Length == 0)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }
                
                foreach (var entry in results)
                {
                    try
                    {
                        // Deserialize the result message
                        var messageData = entry.Values.First(v => v.Name == "data").Value;
                        var resultMsg = ResultMessage.Parser.ParseFrom((byte[])messageData!);
                        
                        // Log the result
                        _logger.LogInformation(
                            "Received result for task {TaskId}: Status={Status}, Result={Result}",
                            resultMsg.TaskId,
                            resultMsg.Status,
                            resultMsg.Result
                        );
                        
                        // Acknowledge the message
                        await db.StreamAcknowledgeAsync(ResultStream, ConsumerGroup, entry.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing result message {MessageId}", entry.Id);
                    }
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error in result consumer loop");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
