namespace CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus
{
    // Singleton in-memory event bus that routes integration events to the handlers subscribed for their type.
    public sealed class InMemoryEventBus
    {
        // Empty static constructor so the singleton is initialized lazily (no beforefieldinit).
        static InMemoryEventBus()
        {
        }

        // Private to enforce the singleton; starts with an empty handlers registry.
        private InMemoryEventBus()
        {
            _handlersDictionary = new Dictionary<string, List<IIntegrationEventHandler>>();
        }

        // The single, process-wide instance shared by all modules.
        public static InMemoryEventBus Instance { get; } = new InMemoryEventBus();

        // Handlers registered per event type, keyed by the event type's full name.
        private readonly IDictionary<string, List<IIntegrationEventHandler>> _handlersDictionary;

        // Registers the handler under the full name of event type T, creating the list on first subscription.
        public void Subscribe<T>(IIntegrationEventHandler<T> handler)
            where T : IntegrationEvent
        {
            var eventType = typeof(T).FullName;
            if (eventType != null)
            {
                if (_handlersDictionary.ContainsKey(eventType))
                {
                    var handlers = _handlersDictionary[eventType];
                    handlers.Add(handler);
                }
                else
                {
                    _handlersDictionary.Add(eventType, [handler]);
                }
            }
        }

        // Invokes, one after another, every handler subscribed to the event's runtime type.
        public async Task Publish<T>(T @event)
            where T : IntegrationEvent
        {
            var eventType = @event.GetType().FullName;

            if (eventType == null)
            {
                return;
            }

            // Note: throws KeyNotFoundException if no handler was ever subscribed for this event type.
            List<IIntegrationEventHandler> integrationEventHandlers = _handlersDictionary[eventType];

            foreach (var integrationEventHandler in integrationEventHandlers)
            {
                if (integrationEventHandler is IIntegrationEventHandler<T> handler)
                {
                    await handler.Handle(@event);
                }
            }
        }
    }
}