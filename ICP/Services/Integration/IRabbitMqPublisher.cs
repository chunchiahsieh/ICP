namespace ICP.Services.Integration;

public interface IRabbitMqPublisher
{
    Task PublishAsync(string routingKey, string messageId, string payloadJson, CancellationToken cancellationToken = default);
}
