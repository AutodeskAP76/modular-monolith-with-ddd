using Serilog;

namespace CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus
{
    // Events bus client that delegates to the process-wide in-memory event bus, used to exchange integration events between modules.
    public class InMemoryEventBusClient : IEventsBus
    {
        private readonly ILogger _logger;

        // Receives the logger used to trace published events.
        public InMemoryEventBusClient(ILogger logger)
        {
            _logger = logger;
        }

        // Nothing to release: the in-memory bus is a shared singleton that this client does not own.
        public void Dispose()
        {
        }

        // Logs the event and publishes it to all handlers subscribed on the in-memory bus.
        public async Task Publish<T>(T @event)
            where T : IntegrationEvent
        {
            _logger.Information("Publishing {Event}", @event.GetType().FullName);
            await InMemoryEventBus.Instance.Publish(@event);
        }

        // Registers the handler on the in-memory bus for events of type T.
        public void Subscribe<T>(IIntegrationEventHandler<T> handler)
            where T : IntegrationEvent
        {
            InMemoryEventBus.Instance.Subscribe(handler);
        }

        // No-op: in-memory handlers are invoked directly on publish, so there is no consumer loop to start.
        public void StartConsuming()
        {
        }
    }
}