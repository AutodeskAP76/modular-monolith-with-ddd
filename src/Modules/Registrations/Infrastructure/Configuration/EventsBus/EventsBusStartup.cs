using Autofac;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus;
using Serilog;

namespace CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.EventsBus
{
    // Subscribes the Registrations module to integration events (currently none are subscribed).
    public static class EventsBusStartup
    {
        // Module entry point: called from RegistrationsStartup to set up the event subscriptions.
        public static void Initialize(
            ILogger logger)
        {
            SubscribeToIntegrationEvents(logger);
        }

        // Resolves the module's event bus; the only subscription is commented out, so nothing is subscribed.
        private static void SubscribeToIntegrationEvents(ILogger logger)
        {
            var eventBus = RegistrationsCompositionRoot.BeginLifetimeScope().Resolve<IEventsBus>();

            //// SubscribeToIntegrationEvent<MemberCreatedIntegrationEvent>(eventBus, logger);
        }

        // Logs and registers a generic handler for one integration event type on the event bus.
        private static void SubscribeToIntegrationEvent<T>(IEventsBus eventBus, ILogger logger)
            where T : IntegrationEvent
        {
            logger.Information("Subscribe to {@IntegrationEvent}", typeof(T).FullName);
            eventBus.Subscribe(
                new IntegrationEventGenericHandler<T>());
        }
    }
}