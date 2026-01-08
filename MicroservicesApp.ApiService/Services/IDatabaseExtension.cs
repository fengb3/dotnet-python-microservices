using Google.Protobuf;
using StackExchange.Redis;

namespace MicroservicesApp.ApiService.Services;

public static class IDatabaseExtension
{
    private static readonly string ConsumerGroup = Environment.GetEnvironmentVariable("CONSUMER_GROUP") ?? "api-consumers";
    private static readonly string ConsumerName  = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "default-consumer";

    public static async Task EnsureInitialized<T>(this IDatabase db, ILogger logger)
    {
        var keyName = typeof(T).FullName;
        try
        {
            await db.StreamCreateConsumerGroupAsync( keyName, ConsumerGroup, "0", createStream: true);
            logger.LogInformation("Created consumer group '{ConsumerGroup}'", ConsumerGroup);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            logger.LogInformation("Consumer group '{ConsumerGroup}' already exists", ConsumerGroup);
        }
        
        logger.LogInformation("Starting to consume results from stream '{ResultStream}'", keyName);
    }

    public static Task<RedisValue> SendMessageAsync<T>(this IDatabase db, T data) where T : Google.Protobuf.IMessage<T>
    {
        var entry = new[]
        {
            new NameValueEntry("data", data.ToByteArray()),
        };
        return db.StreamAddAsync(typeof(T).FullName, entry);
    }

    public static async Task<T?> ReadMessageAsync<T>(this IDatabase db, Func<byte[], T> parser) where T : Google.Protobuf.IMessage<T>
    {
        var key = typeof(T).FullName;
        var results = await db.StreamReadGroupAsync(
            key,
            ConsumerGroup,
            ConsumerName,
            ">",
            count: 1
        );
        if (results.Length == 0)
        {
            await Task.Delay(1000);
            return default!;
        }
        foreach (var entry in results)
        {
            var messageData = entry.Values.First(v => v.Name == "data").Value;
            var resultMsg   = parser((byte[])messageData!);
            await db.StreamAcknowledgeAsync(key, ConsumerGroup, entry.Id);
            return resultMsg;
        }
        return default!;
    }
}