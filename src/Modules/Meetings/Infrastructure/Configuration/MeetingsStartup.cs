using Autofac;
using CompanyName.MyMeetings.BuildingBlocks.Application;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.Emails;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus;
using CompanyName.MyMeetings.Modules.Meetings.Application.MeetingComments;
using CompanyName.MyMeetings.Modules.Meetings.Application.MeetingGroupProposals;
using CompanyName.MyMeetings.Modules.Meetings.Application.MeetingGroupProposals.AcceptMeetingGroupProposal;
using CompanyName.MyMeetings.Modules.Meetings.Application.MeetingGroups;
using CompanyName.MyMeetings.Modules.Meetings.Application.Meetings.SendMeetingAttendeeAddedEmail;
using CompanyName.MyMeetings.Modules.Meetings.Application.Members.CreateMember;
using CompanyName.MyMeetings.Modules.Meetings.Application.MemberSubscriptions;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Authentication;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.DataAccess;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Email;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.EventsBus;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Logging;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Mediation;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Processing;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Processing.Outbox;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Quartz;
using Serilog.Extensions.Logging;
using ILogger = Serilog.ILogger;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration
{
    // Entry point that boots the Meetings module: builds its own DI container and starts its background processing.
    public class MeetingsStartup
    {
        // The Meetings module's private Autofac container, separate from the API's container.
        private static IContainer _container;

        // Starts the module; called once by the API at startup (see Startup.InitializeModules).
        public static void Initialize(
            string connectionString,
            IExecutionContextAccessor executionContextAccessor,
            ILogger logger,
            EmailsConfiguration emailsConfiguration,
            IEventsBus eventsBus,
            long? internalProcessingPoolingInterval = null)
        {
            // Tag every log entry of this module with "Meetings" so it can be told apart from other modules.
            var moduleLogger = logger.ForContext("Module", "Meetings");

            // Register all of the module's dependencies in its DI container.
            ConfigureCompositionRoot(
                connectionString,
                executionContextAccessor,
                moduleLogger,
                emailsConfiguration,
                eventsBus);

            // Start the Quartz scheduler that runs the module's background jobs (e.g. outbox, internal commands).
            QuartzStartup.Initialize(moduleLogger, internalProcessingPoolingInterval);

            // Subscribe the module's handlers to integration events coming from the event bus.
            EventsBusStartup.Initialize(moduleLogger);
        }

        // Shuts the module down by stopping its background job scheduler.
        public static void Stop()
        {
            QuartzStartup.StopQuartz();
        }

        // Builds the module's DI container: registers every Autofac module plus the shared services, then makes it available module-wide.
        private static void ConfigureCompositionRoot(
            string connectionString,
            IExecutionContextAccessor executionContextAccessor,
            ILogger logger,
            EmailsConfiguration emailsConfiguration,
            IEventsBus eventsBus)
        {
            var containerBuilder = new ContainerBuilder();

            // Logging: registers the module's logger.
            containerBuilder.RegisterModule(new LoggingModule(logger.ForContext("Module", "Meetings")));

            // Data access: registers the database connection (using the connection string) and its logging.
            var loggerFactory = new SerilogLoggerFactory(logger);
            containerBuilder.RegisterModule(new DataAccessModule(connectionString, loggerFactory));

            // Processing: registers command/query handling decorators and background processing services.
            containerBuilder.RegisterModule(new ProcessingModule());

            // Event bus: registers the bus used to publish and receive integration events between modules.
            containerBuilder.RegisterModule(new EventsBusModule(eventsBus));

            // Mediator: registers MediatR, which routes commands, queries and notifications to their handlers.
            containerBuilder.RegisterModule(new MediatorModule());

            // Authentication: registers services that identify the current user inside the module.
            containerBuilder.RegisterModule(new AuthenticationModule());

            // Maps each domain notification's name (stored in the database) to its C# type, so the outbox can turn stored messages back into objects.
            var domainNotificationsMap = new BiDictionary<string, Type>();
            domainNotificationsMap.Add("MeetingGroupProposalAcceptedNotification", typeof(MeetingGroupProposalAcceptedNotification));
            domainNotificationsMap.Add("MeetingGroupProposedNotification", typeof(MeetingGroupProposedNotification));
            domainNotificationsMap.Add("MeetingGroupCreatedNotification", typeof(MeetingGroupCreatedNotification));
            domainNotificationsMap.Add("MeetingAttendeeAddedNotification", typeof(MeetingAttendeeAddedNotification));
            domainNotificationsMap.Add("MemberCreatedNotification", typeof(MemberCreatedNotification));
            domainNotificationsMap.Add("MemberSubscriptionExpirationDateChangedNotification", typeof(MemberSubscriptionExpirationDateChangedNotification));
            domainNotificationsMap.Add("MeetingCommentLikedNotification", typeof(MeetingCommentLikedNotification));
            domainNotificationsMap.Add("MeetingCommentUnlikedNotification", typeof(MeetingCommentUnlikedNotification));

            // Outbox: registers the outbox that saves notifications with the data change and publishes them afterwards.
            containerBuilder.RegisterModule(new OutboxModule(domainNotificationsMap));

            // Email: registers the email sender, configured with the "from" address.
            containerBuilder.RegisterModule(new EmailModule(emailsConfiguration));

            // Quartz: registers the background jobs run by the scheduler.
            containerBuilder.RegisterModule(new QuartzModule());

            // Reuses the API's current-user accessor instead of creating a new one.
            containerBuilder.RegisterInstance(executionContextAccessor);

            // Create the container from all registrations above.
            _container = containerBuilder.Build();

            // Share the container with the rest of the module (e.g. MeetingsModule resolves its handlers through it).
            MeetingsCompositionRoot.SetContainer(_container);
        }
    }
}