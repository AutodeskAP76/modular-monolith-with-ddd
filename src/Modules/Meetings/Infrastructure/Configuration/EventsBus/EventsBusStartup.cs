using Autofac;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus;
using CompanyName.MyMeetings.Modules.Administration.IntegrationEvents.MeetingGroupProposals;
using CompanyName.MyMeetings.Modules.Payments.IntegrationEvents;
using CompanyName.MyMeetings.Modules.Registrations.IntegrationEvents;
using Serilog;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.EventsBus
{
    // Subscribes the Meetings module to the integration events it consumes from other modules.
    public static class EventsBusStartup
    {
        // Module entry point: called from MeetingsStartup to set up the event subscriptions.
        public static void Initialize(
            ILogger logger)
        {
            SubscribeToIntegrationEvents(logger);
        }

        // Resolves the module's event bus and subscribes to each integration event the module handles.
        private static void SubscribeToIntegrationEvents(ILogger logger)
        {
            var eventBus = MeetingsCompositionRoot.BeginLifetimeScope().Resolve<IEventsBus>();

            SubscribeToIntegrationEvent<MeetingGroupProposalAcceptedIntegrationEvent>(eventBus, logger);
            SubscribeToIntegrationEvent<SubscriptionExpirationDateChangedIntegrationEvent>(eventBus, logger);
            SubscribeToIntegrationEvent<NewUserRegisteredIntegrationEvent>(eventBus, logger);
            SubscribeToIntegrationEvent<MeetingFeePaidIntegrationEvent>(eventBus, logger);
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