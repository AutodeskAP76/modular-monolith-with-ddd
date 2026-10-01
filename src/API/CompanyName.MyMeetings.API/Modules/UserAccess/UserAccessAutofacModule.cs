using Autofac;
using CompanyName.MyMeetings.Modules.UserAccess.Application.Contracts;
using CompanyName.MyMeetings.Modules.UserAccess.Infrastructure;

namespace CompanyName.MyMeetings.API.Modules.UserAccess
{
    // Autofac module that makes the UserAccess module available to the API through its IUserAccessModule interface.
    public class UserAccessAutofacModule : Module
    {
        // Called by Autofac when this module is registered (see Startup.ConfigureContainer).
        protected override void Load(ContainerBuilder builder)
        {
            // Resolve IUserAccessModule to UserAccessModule, sharing one instance within each lifetime scope (e.g. per request).
            builder.RegisterType<UserAccessModule>()
                .As<IUserAccessModule>()
                .InstancePerLifetimeScope();
        }
    }
}