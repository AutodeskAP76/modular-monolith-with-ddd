using Autofac;
using CompanyName.MyMeetings.Modules.Administration.Application.Contracts;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure;

namespace CompanyName.MyMeetings.API.Modules.Administration
{
    // Autofac module that makes the Administration module available to the API through its IAdministrationModule interface.
    internal class AdministrationAutofacModule : Module
    {
        // Called by Autofac when this module is registered (see Startup.ConfigureContainer).
        protected override void Load(ContainerBuilder builder)
        {
            // Resolve IAdministrationModule to AdministrationModule, sharing one instance within each lifetime scope (e.g. per request).
            builder.RegisterType<AdministrationModule>()
                .As<IAdministrationModule>()
                .InstancePerLifetimeScope();
        }
    }
}