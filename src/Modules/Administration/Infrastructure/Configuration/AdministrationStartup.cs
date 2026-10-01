using Autofac;
using CompanyName.MyMeetings.BuildingBlocks.Application;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus;
using CompanyName.MyMeetings.Modules.Administration.Application.MeetingGroupProposals.AcceptMeetingGroupProposal;
using CompanyName.MyMeetings.Modules.Administration.Application.MeetingGroupProposals.RequestMeetingGroupProposalVerification;
using CompanyName.MyMeetings.Modules.Administration.Application.Members.CreateMember;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.Authentication;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.DataAccess;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.EventsBus;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.Logging;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.Mediation;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.Processing;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.Processing.InternalCommands;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.Processing.Outbox;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration.Quartz;
using Serilog;

namespace CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration
{
    // Entry point that boots the Administration module: builds its own DI container and starts its background processing.
    public class AdministrationStartup
    {
        // The Administration module's private Autofac container, separate from the API's container.
        private static IContainer _container;

        // Starts the module; called once by the API at startup (see Startup.InitializeModules).
        public static void Initialize(
            string connectionString,
            IExecutionContextAccessor executionContextAccessor,
            ILogger logger,
            IEventsBus eventsBus,
            long? internalProcessingPoolingInterval = null)
        {
            // Tag every log entry of this module with "Administration" so it can be told apart from other modules.
            var moduleLogger = logger.ForContext("Module", "Administration");

            // Register all of the module's dependencies in its DI container.
            ConfigureContainer(connectionString, executionContextAccessor, moduleLogger, eventsBus);

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
        private static void ConfigureContainer(
            string connectionString,
            IExecutionContextAccessor executionContextAccessor,
            ILogger logger,
            IEventsBus eventsBus)
        {
            var containerBuilder = new ContainerBuilder();

            // Logging: registers the module's logger.
            containerBuilder.RegisterModule(new LoggingModule(logger));

            // Data access: registers the database connection (using the connection string) and its logging.
            var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger);
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

            // Outbox: registers the outbox that saves notifications with the data change and publishes them afterwards.
            containerBuilder.RegisterModule(new OutboxModule(domainNotificationsMap));

            // Maps each internal command's name (stored in the database) to its C# type, so queued commands can be executed later.
            BiDictionary<string, Type> internalCommandsMap = new BiDictionary<string, Type>();
            internalCommandsMap.Add("CreateMember", typeof(CreateMemberCommand));
            internalCommandsMap.Add("RequestMeetingGroupProposalVerification", typeof(RequestMeetingGroupProposalVerificationCommand));

            // Internal commands: registers the queue of commands the module schedules for itself to run in the background.
            containerBuilder.RegisterModule(new InternalCommandsModule(internalCommandsMap));

            // Quartz: registers the background jobs run by the scheduler.
            containerBuilder.RegisterModule(new QuartzModule());

            // Reuses the API's current-user accessor instead of creating a new one.
            containerBuilder.RegisterInstance(executionContextAccessor);

            // Create the container from all registrations above.
            _container = containerBuilder.Build();

            // Share the container with the rest of the module (e.g. AdministrationModule resolves its handlers through it).
            AdministrationCompositionRoot.SetContainer(_container);
        }
    }
}