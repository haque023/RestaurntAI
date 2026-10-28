namespace RestaurantAI.Infrastructure.Messaging.RabbitMQ;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672";

    /// <summary>Topic exchange that receives every platform event. Routing key = message type.</summary>
    public string Exchange { get; set; } = "restaurant.events";

    public string ClientName { get; set; } = "restaurant-ai";
}
