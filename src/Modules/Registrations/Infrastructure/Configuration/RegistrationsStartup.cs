using Autofac;
using CompanyName.MyMeetings.BuildingBlocks.Application;
using CompanyName.MyMeetings.BuildingBlocks.Application.Emails;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.Emails;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus;
using CompanyName.MyMeetings.Modules.Registrations.Application.UserRegistrations.ConfirmUserRegistration;
using CompanyName.MyMeetings.Modules.Registrations.Application.UserRegistrations.RegisterNewUser;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.DataAccess;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.Domain;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.Email;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.EventsBus;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.Logging;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.Mediation;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.Processing;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.Processing.Outbox;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.Quartz;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration.UserAccess;
using Serilog;

namespace CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration
{
    // Entry point that boots the Registrations module: builds its own DI container and starts its background processing.
    public class RegistrationsStartup
    {
        // The Registrations module's private Autofac container, separate from the API's container.
        private static IContainer _container;

        // Starts the module; called once by the API at startup (see Startup.InitializeModules).
        public static void Initialize(
            string connectionString,
            IExecutionContextAccessor executionContextAccessor,
            ILogger logger,
            EmailsConfiguration emailsConfiguration,
            string textEncryptionKey,
            IEmailSender emailSender,
            IEventsBus eventsBus,
            long? internalProcessingPoolingInterval = null)
        {
            // Tag every log entry of this module with "Registrations" so it can be told apart from other modules.
            var moduleLogger = logger.ForContext("Module", "Registrations");

            // Register all of the module's dependencies in its DI container.
            ConfigureCompositionRoot(
                connectionString,
                executionContextAccessor,
                logger,
                emailsConfiguration,
                textEncryptionKey,
                emailSender,
                eventsBus);

            // Start the Quartz scheduler that runs the module's background jobs (e.g. outbox, internal commands).
            QuartzStartup.Initialize(moduleLogger, internalProcessingPoolingInterval);

            // Subscribe the module's handlers to integration events coming from the event bus.
            EventsBusStartup.Initialize(moduleLogger);
        }

        // Builds the module's DI container: registers every Autofac module plus the shared services, then makes it available module-wide.
        private static void ConfigureCompositionRoot(
            string connectionString,
            IExecutionContextAccessor executionContextAccessor,
            ILogger logger,
            EmailsConfiguration emailsConfiguration,
            string textEncryptionKey,
            IEmailSender emailSender,
            IEventsBus eventsBus)
        {
            var containerBuilder = new ContainerBuilder();

            // Logging: registers the module's logger.
            containerBuilder.RegisterModule(new LoggingModule(logger.ForContext("Module", "Registrations")));

            // Data access: registers the database connection (using the connection string) and its logging.
            var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger);
            containerBuilder.RegisterModule(new DataAccessModule(connectionString, loggerFactory));

            // Processing: registers command/query handling decorators and background processing services.
            containerBuilder.RegisterModule(new ProcessingModule());

            // Event bus: registers the bus used to publish and receive integration events between modules.
            containerBuilder.RegisterModule(new EventsBusModule(eventsBus));

            // Mediator: registers MediatR, which routes commands, queries and notifications to their handlers.
            containerBuilder.RegisterModule(new MediatorModule());

            // UserAccess: registers the Registrations module's gateway to the UserAccess module (e.g. to create users on registration).
            containerBuilder.RegisterModule(new UserAccessAutofacModule());

            // Maps each domain notification's name (stored in the database) to its C# type, so the outbox can turn stored messages back into objects.
            var domainNotificationsMap = new BiDictionary<string, Type>();
            domainNotificationsMap.Add("NewUserRegisteredNotification", typeof(NewUserRegisteredNotification));
            domainNotificationsMap.Add("UserRegistrationConfirmedNotification", typeof(UserRegistrationConfirmedNotification));

            // Outbox: registers the outbox that saves notifications with the data change and publishes them afterwards.
            containerBuilder.RegisterModule(new OutboxModule(domainNotificationsMap));

            // Quartz: registers the background jobs run by the scheduler.
            containerBuilder.RegisterModule(new QuartzModule());

            // Domain: registers domain services used by the module's business logic.
            containerBuilder.RegisterModule(new DomainModule());

            // Email: registers the email sender, configured with the "from" address.
            containerBuilder.RegisterModule(new EmailModule(emailsConfiguration, emailSender));

            //// containerBuilder.RegisterModule(new SecurityModule(textEncryptionKey));

            // Reuses the API's current-user accessor instead of creating a new one.
            containerBuilder.RegisterInstance(executionContextAccessor);

            // Create the container from all registrations above.
            _container = containerBuilder.Build();

            // Share the container with the rest of the module (e.g. RegistrationsModule resolves its handlers through it).
            RegistrationsCompositionRoot.SetContainer(_container);
        }
    }
}