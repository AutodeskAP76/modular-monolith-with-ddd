using Autofac;
using CompanyName.MyMeetings.Modules.Payments.Application.Contracts;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure;

namespace CompanyName.MyMeetings.API.Modules.Payments
{
    // Autofac module that makes the Payments module available to the API through its IPaymentsModule interface.
    public class PaymentsAutofacModule : Module
    {
        // Called by Autofac when this module is registered (see Startup.ConfigureContainer).
        protected override void Load(ContainerBuilder builder)
        {
            // Resolve IPaymentsModule to PaymentsModule, sharing one instance within each lifetime scope (e.g. per request).
            builder.RegisterType<PaymentsModule>()
                .As<IPaymentsModule>()
                .InstancePerLifetimeScope();
        }
    }
}