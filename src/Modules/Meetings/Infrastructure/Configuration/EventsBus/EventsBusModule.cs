using Autofac;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.EventsBus
{
    // Autofac module that registers the event bus used to exchange integration events with other modules.
    internal class EventsBusModule : Autofac.Module
    {
        private readonly IEventsBus _eventsBus;

        // Receives the event bus supplied by the host, or null to fall back to the in-memory one.
        public EventsBusModule(IEventsBus eventsBus)
        {
            _eventsBus = eventsBus;
        }

        // Registers the supplied bus, or the in-memory bus client when none was supplied.
        protected override void Load(ContainerBuilder builder)
        {
            if (_eventsBus != null)
            {
                builder.RegisterInstance(_eventsBus).SingleInstance();
            }
            else
            {
                builder.RegisterType<InMemoryEventBusClient>()
                .As<IEventsBus>()
                .SingleInstance();
            }
        }
    }
}