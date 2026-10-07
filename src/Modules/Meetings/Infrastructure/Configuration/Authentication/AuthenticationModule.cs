using Autofac;
using CompanyName.MyMeetings.Modules.Meetings.Application.Members;
using CompanyName.MyMeetings.Modules.Meetings.Domain.Members;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Authentication
{
    // Autofac module that registers the services identifying the current member.
    internal class AuthenticationModule : Autofac.Module
    {
        // Registers MemberContext as the IMemberContext implementation.
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<MemberContext>()
                .As<IMemberContext>()
                .InstancePerLifetimeScope();
        }
    }
}