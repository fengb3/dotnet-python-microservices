using StackExchange.Redis;

namespace MicroservicesApp.ApiService.Services;

public partial class MessageConsumer(ILogger<MessageConsumer> logger, IServiceProvider rootServiceProvider, IConnectionMultiplexer redis)
{
    public async Task StartConsumeAsync<T>(CancellationToken ct) where T : Google.Protobuf.IMessage<T>
    {
        var parser = rootServiceProvider.GetRequiredService<Func<byte[], T>>();
        // var redis  = rootServiceProvider.GetRequiredService<IConnectionMultiplexer>();
        var db     = redis.GetDatabase();
        await db.EnsureInitialized<T>(logger);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var x = await db.ReadMessageAsync<T>(parser);
                
                if (x == null)
                    continue;
                
                using var scope   = rootServiceProvider.CreateScope();
                var       handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<T>>();
                await handler.HandleMessageAsync(x);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Error in result consumer loop");
                await Task.Delay(5000, ct);
            }
        }
    }
}