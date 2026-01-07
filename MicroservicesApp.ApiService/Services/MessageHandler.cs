using Microservices;
using StackExchange.Redis;

namespace MicroservicesApp.ApiService.Services;

public interface IMessageHandler<in T> where T : Google.Protobuf.IMessage<T>
{
    public Task HandleMessageAsync(T message);
}

public class ResultMessageHandler(ILogger<ResultMessageHandler> logger) : IMessageHandler<ResultMessage>
{
    public async Task HandleMessageAsync(ResultMessage message) =>
        logger.LogInformation("Processed ResultMessage in Handler: TaskId={TaskId}, Result={Result}",
                message.TaskId, message.Result);
}

public interface IMessageTypeProvider
{
    IEnumerable<string> GetMessageTypes();
}

public class MessageConsumer(
    IConnectionMultiplexer redis, 
    IMessageTypeProvider messageTypeProvider,
    IServiceProvider rootServiceProvider
    ) : BackgroundService
{
    private readonly string ConsumerGroup = "api-consumers";
    private readonly string ConsumerName  = "api-1";
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var          db       = redis.GetDatabase();
        List<Func<Task>> handlers = new();
        var          types    = messageTypeProvider.GetMessageTypes();
        foreach (var type in types)
        {
            // 2. read messages of that type from Redis stream
            switch (type)
            {
                case nameof(ResultMessage):
                    handlers.Add(() => ConsumeOne<ResultMessage>(
                        db,
                        "results",
                        ResultMessage.Parser.ParseFrom,
                        stoppingToken
                    ));
                    
                    break;
            }
        }
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                foreach (var handler in handlers)
                {
                    await handler();
                }
            }
            catch (Exception ex)
            {
                // Log and continue
            }
        }
    }

    private async Task ConsumeOne<T>(IDatabase db, string streamKey, Func<byte[], T> parser, CancellationToken ct) where T : Google.Protobuf.IMessage<T>
    {
        var results = await db.StreamReadGroupAsync(
            streamKey,
            ConsumerGroup,
            ConsumerName,
            ">",
            count: 1
        );
        if (results.Length == 0)
        {
            await Task.Delay(1000, ct);
            return;
        }
        foreach (var entry in results)
        {
            var messageData = entry.Values.First(v => v.Name == "data").Value;
            var resultMsg   = parser((byte[])messageData!);
            var scoped      = rootServiceProvider.CreateScope();
            var handler     = scoped.ServiceProvider.GetRequiredService<IMessageHandler<T>>();
            await handler.HandleMessageAsync(resultMsg);
        }
    }
}