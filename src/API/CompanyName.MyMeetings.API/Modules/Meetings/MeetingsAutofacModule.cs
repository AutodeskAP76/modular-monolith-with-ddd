using Autofac;
using CompanyName.MyMeetings.Modules.Meetings.Application.Contracts;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure;

namespace CompanyName.MyMeetings.API.Modules.Meetings
{
    // Autofac module that makes the Meetings module available to the API through its IMeetingsModule interface.
    public class MeetingsAutofacModule : Module
    {
        // Called by Autofac when this module is registered (see Startup.ConfigureContainer).
        protected override void Load(ContainerBuilder builder)
        {
            // Resolve IMeetingsModule to MeetingsModule, sharing one instance within each lifetime scope (e.g. per request).
            builder.RegisterType<MeetingsModule>()
                .As<IMeetingsModule>()
                .InstancePerLifetimeScope();
        }
    }
}