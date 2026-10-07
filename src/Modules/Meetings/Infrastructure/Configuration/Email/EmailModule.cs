using Autofac;
using CompanyName.MyMeetings.BuildingBlocks.Application.Emails;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.Emails;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Email
{
    // Autofac module that registers the email sending service.
    internal class EmailModule : Module
    {
        private readonly EmailsConfiguration _configuration;

        // Receives the emails configuration (the "from" address).
        public EmailModule(EmailsConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Registers EmailSender as the IEmailSender implementation, using the emails configuration.
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<EmailSender>()
                .As<IEmailSender>()
                .WithParameter("configuration", _configuration)
                .InstancePerLifetimeScope();
        }
    }
}