using MediatR;

namespace CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus
{
    // Base class for events published on the events bus to communicate across module boundaries.
    public abstract class IntegrationEvent : INotification
    {
        // Unique identifier of the event.
        public Guid Id { get; }

        // Moment when the event occurred.
        public DateTime OccurredOn { get; }

        // Sets the identity and occurrence time shared by all integration events.
        protected IntegrationEvent(Guid id, DateTime occurredOn)
        {
            this.Id = id;
            this.OccurredOn = occurredOn;
        }
    }
}