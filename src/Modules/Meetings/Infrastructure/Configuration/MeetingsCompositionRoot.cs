using Autofac;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration
{
    // Holds the Meetings module container so any part of the module can resolve its dependencies.
    internal static class MeetingsCompositionRoot
    {
        private static IContainer _container;

        // Stores the container built at module startup.
        internal static void SetContainer(IContainer container)
        {
            _container = container;
        }

        // Opens a new lifetime scope from the stored container, to be disposed by the caller.
        internal static ILifetimeScope BeginLifetimeScope()
        {
            return _container.BeginLifetimeScope();
        }
    }
}