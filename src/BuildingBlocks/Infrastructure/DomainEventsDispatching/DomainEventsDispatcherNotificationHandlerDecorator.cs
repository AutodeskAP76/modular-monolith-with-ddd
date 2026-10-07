using MediatR;

namespace CompanyName.MyMeetings.BuildingBlocks.Infrastructure.DomainEventsDispatching
{
    // Decorator that dispatches the domain events raised while a notification handler was running.
    public class DomainEventsDispatcherNotificationHandlerDecorator<T> : INotificationHandler<T>
        where T : INotification
    {
        private readonly INotificationHandler<T> _decorated;
        private readonly IDomainEventsDispatcher _domainEventsDispatcher;

        // Receives the dispatcher and the notification handler being wrapped.
        public DomainEventsDispatcherNotificationHandlerDecorator(
            IDomainEventsDispatcher domainEventsDispatcher,
            INotificationHandler<T> decorated)
        {
            _domainEventsDispatcher = domainEventsDispatcher;
            _decorated = decorated;
        }

        // Runs the wrapped handler, then dispatches any domain events it raised.
        public async Task Handle(T notification, CancellationToken cancellationToken)
        {
            await this._decorated.Handle(notification, cancellationToken);

            await this._domainEventsDispatcher.DispatchEventsAsync();
        }
    }
}