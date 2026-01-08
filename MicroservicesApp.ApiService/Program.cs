using StackExchange.Redis;
using Microservices;
using Google.Protobuf;
using MicroservicesApp.ApiService.Services;
using SourceGenerators.Generated;

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

builder.Services.AddMessageHandlers();

builder.Services.AddSingleton<MessageConsumer>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapPost("/task", async (TaskRequest request, IConnectionMultiplexer redis, ILogger<Program> logger) =>
{
    var db = redis.GetDatabase();
    
    // Create task message
    var taskMsg = new ResultMessage
    {
        TaskId    = Guid.NewGuid().ToString(),
        Status    = "good",
        Result    = "this is result",
        Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds()
    };
    
    await db.SendMessageAsync(taskMsg);
    
    logger.LogInformation("Published task {TaskId} of type {TaskType}", taskMsg.TaskId, taskMsg.GetType().FullName);
    
    return Results.Ok(new { taskId = taskMsg.TaskId, message = "Task submitted successfully" });
})
.WithName("SubmitTask");

app.MapDefaultEndpoints();

app.Run();

record TaskRequest(string? TaskType, string? Data);

// Background service to consume results from Redis Stream
class ResultConsumerService(ILogger<ResultConsumerService> logger,IConnectionMultiplexer redis, IServiceProvider rootSp) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var msgConsumer = rootSp.GetRequiredService<MessageConsumer>();

        await msgConsumer.StartConsumeAllAsync(stoppingToken);
    }
}
