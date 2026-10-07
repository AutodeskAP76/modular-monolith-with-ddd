using Autofac;
using Autofac.Core;
using CompanyName.MyMeetings.BuildingBlocks.Application.Events;
using CompanyName.MyMeetings.BuildingBlocks.Application.Outbox;
using CompanyName.MyMeetings.BuildingBlocks.Domain;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.Serialization;
using MediatR;
using Newtonsoft.Json;

namespace CompanyName.MyMeetings.BuildingBlocks.Infrastructure.DomainEventsDispatching
{
    // Collects the domain events raised by the entities, publishes them in-process and stores their notifications in the outbox.
    public class DomainEventsDispatcher : IDomainEventsDispatcher
    {
        private readonly IMediator _mediator;

        private readonly ILifetimeScope _scope;

        private readonly IOutbox _outbox;

        private readonly IDomainEventsAccessor _domainEventsProvider;

        private readonly IDomainNotificationsMapper _domainNotificationsMapper;

        // Receives the mediator, the DI scope, the outbox, the domain events accessor and the notifications mapper.
        public DomainEventsDispatcher(
            IMediator mediator,
            ILifetimeScope scope,
            IOutbox outbox,
            IDomainEventsAccessor domainEventsProvider,
            IDomainNotificationsMapper domainNotificationsMapper)
        {
            _mediator = mediator;
            _scope = scope;
            _outbox = outbox;
            _domainEventsProvider = domainEventsProvider;
            _domainNotificationsMapper = domainNotificationsMapper;
        }

        // Publishes pending domain events to their handlers and saves a serialized outbox message for each event that has a notification.
        public async Task DispatchEventsAsync()
        {
            // Gather the domain events raised by the tracked aggregates.
            var domainEvents = _domainEventsProvider.GetAllDomainEvents();

            // Step 1: for each event, build its matching integration notification (if one is registered).
            List<IDomainEventNotification<IDomainEvent>> domainEventNotifications = [];
            foreach (var domainEvent in domainEvents)
            {
                // Close the open generic IDomainEventNotification<> over the concrete event type.
                Type domainEvenNotificationType = typeof(IDomainEventNotification<>);
                var domainNotificationWithGenericType = domainEvenNotificationType.MakeGenericType(domainEvent.GetType());

                // Resolve optionally: not every domain event has a notification. The event and its id
                // are passed as named constructor parameters of the notification.
                var domainNotification = _scope.ResolveOptional(domainNotificationWithGenericType, new List<Parameter>
                {
                    new NamedParameter("domainEvent", domainEvent),
                    new NamedParameter("id", domainEvent.Id)
                });

                if (domainNotification != null)
                {
                    domainEventNotifications.Add(domainNotification as IDomainEventNotification<IDomainEvent>);
                }
            }

            // Clear the events before publishing so that events raised by handlers are not
            // dispatched twice, and handlers can raise new events for the next dispatch round.
            _domainEventsProvider.ClearAllDomainEvents();

            // Step 2: publish each domain event in-process to its handlers (same transaction).
            foreach (var domainEvent in domainEvents)
            {
                await _mediator.Publish(domainEvent);
            }

            // Step 3: persist the notifications to the outbox so they are delivered
            // asynchronously (e.g. to other modules) by the outbox processor.
            foreach (var domainEventNotification in domainEventNotifications)
            {
                // Stable type name used to deserialize the notification when the outbox is processed.
                var type = _domainNotificationsMapper.GetName(domainEventNotification.GetType());

                // Serialize all properties (including non-public ones) of the notification.
                var data = JsonConvert.SerializeObject(domainEventNotification, new JsonSerializerSettings
                {
                    ContractResolver = new AllPropertiesContractResolver()
                });

                var outboxMessage = new OutboxMessage(
                    domainEventNotification.Id,
                    domainEventNotification.DomainEvent.OccurredOn,
                    type,
                    data);

                _outbox.Add(outboxMessage);
            }
        }
    }
}