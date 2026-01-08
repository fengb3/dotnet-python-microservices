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

public class AlbumInfoMessageHandler(ILogger<AlbumInfoMessageHandler> logger) : IMessageHandler<AlbumInfo>
{
    public async Task HandleMessageAsync(AlbumInfo message) =>
        logger.LogInformation("Processed AlbumInfoMessage in Handler: AlbumId={AlbumId}, Title={Title}, Artist={Artist}",
            message.Id, message.Title, message.Artist);
}

public class ImageInfoMessageHandler(ILogger<ImageInfoMessageHandler> logger) : IMessageHandler<ImageInfo>
{
    public async Task HandleMessageAsync(ImageInfo message) =>
        logger.LogInformation("Processed ImageInfoMessage in Handler: AlbumId = {AlbumId}",
            message.AlbumId);
}