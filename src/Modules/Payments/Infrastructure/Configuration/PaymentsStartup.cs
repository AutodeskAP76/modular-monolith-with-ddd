using Autofac;
using CompanyName.MyMeetings.BuildingBlocks.Application;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.Emails;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.EventBus;
using CompanyName.MyMeetings.Modules.Payments.Application.MeetingFees.MarkMeetingFeeAsPaid;
using CompanyName.MyMeetings.Modules.Payments.Application.MeetingFees.MarkMeetingFeePaymentAsPaid;
using CompanyName.MyMeetings.Modules.Payments.Application.Subscriptions.CreateSubscription;
using CompanyName.MyMeetings.Modules.Payments.Application.Subscriptions.MarkSubscriptionPaymentAsPaid;
using CompanyName.MyMeetings.Modules.Payments.Application.Subscriptions.MarkSubscriptionRenewalPaymentAsPaid;
using CompanyName.MyMeetings.Modules.Payments.Application.Subscriptions.RenewSubscription;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.AggregateStore;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.Authentication;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.DataAccess;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.Email;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.EventsBus;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.Logging;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.Mediation;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.Processing;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.Processing.Outbox;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration.Quartz;
using ILogger = Serilog.ILogger;

namespace CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration
{
    // Entry point that boots the Payments module: builds its own DI container and starts its background processing.
    public class PaymentsStartup
    {
        // The Payments module's private Autofac container, separate from the API's container.
        private static IContainer _container;

        // Keeps the module's event projectors running; kept so Stop can shut them down.
        private static SubscriptionsManager _subscriptionsManager;

        // Starts the module; called once by the API at startup (see Startup.InitializeModules).
        public static void Initialize(
            string connectionString,
            IExecutionContextAccessor executionContextAccessor,
            ILogger logger,
            EmailsConfiguration emailsConfiguration,
            IEventsBus eventsBus,
            bool runQuartz = true,
            long? internalProcessingPoolingInterval = null)
        {
            // Tag every log entry of this module with "Payments" so it can be told apart from other modules.
            var moduleLogger = logger.ForContext("Module", "Payments");

            // Register all of the module's dependencies in its DI container.
            ConfigureCompositionRoot(connectionString, executionContextAccessor, moduleLogger, emailsConfiguration, eventsBus, runQuartz);

            // Start the Quartz scheduler for background jobs, unless disabled (runQuartz = false, e.g. in tests).
            if (runQuartz)
            {
                QuartzStartup.Initialize(moduleLogger, internalProcessingPoolingInterval);
            }

            // Subscribe the module's handlers to integration events coming from the event bus.
            EventsBusStartup.Initialize(moduleLogger);
        }

        // Shuts the module down by stopping its event projectors and its background job scheduler.
        public static void Stop()
        {
            _subscriptionsManager.Stop();
            QuartzStartup.StopQuartz();
        }

        // Builds the module's DI container: registers every Autofac module plus the shared services, then makes it available module-wide.
        private static void ConfigureCompositionRoot(
            string connectionString,
            IExecutionContextAccessor executionContextAccessor,
            ILogger logger,
            EmailsConfiguration emailsConfiguration,
            IEventsBus eventsBus,
            bool runQuartz = true)
        {
            var containerBuilder = new ContainerBuilder();

            // Logging: registers the module's logger.
            containerBuilder.RegisterModule(new LoggingModule(logger));

            // Data access: registers the database connection (using the connection string) and its logging.
            var loggerFactory = new Serilog.Extensions.Logging.SerilogLoggerFactory(logger);
            containerBuilder.RegisterModule(new DataAccessModule(connectionString, loggerFactory));

            // Processing: registers command/query handling decorators and background processing services.
            containerBuilder.RegisterModule(new ProcessingModule());

            // Email: registers the email sender, configured with the "from" address.
            containerBuilder.RegisterModule(new EmailModule(emailsConfiguration));

            // Event bus: registers the bus used to publish and receive integration events between modules.
            containerBuilder.RegisterModule(new EventsBusModule(eventsBus));

            // Mediator: registers MediatR, which routes commands, queries and notifications to their handlers.
            containerBuilder.RegisterModule(new MediatorModule());

            // Authentication: registers services that identify the current user inside the module.
            containerBuilder.RegisterModule(new AuthenticationModule());

            // Maps each domain notification's name (stored in the database) to its C# type, so the outbox can turn stored messages back into objects.
            BiDictionary<string, Type> domainNotificationsMap = new BiDictionary<string, Type>();
            domainNotificationsMap.Add("MeetingFeePaidNotification", typeof(MeetingFeePaidNotification));
            domainNotificationsMap.Add("MeetingFeePaymentPaidNotification", typeof(MeetingFeePaymentPaidNotification));
            domainNotificationsMap.Add("SubscriptionCreatedNotification", typeof(SubscriptionCreatedNotification));
            domainNotificationsMap.Add("SubscriptionPaymentPaidNotification", typeof(SubscriptionPaymentPaidNotification));
            domainNotificationsMap.Add("SubscriptionRenewalPaymentPaidNotification", typeof(SubscriptionRenewalPaymentPaidNotification));
            domainNotificationsMap.Add("SubscriptionRenewedNotification", typeof(SubscriptionRenewedNotification));

            // Outbox: registers the outbox that saves notifications with the data change and publishes them afterwards.
            containerBuilder.RegisterModule(new OutboxModule(domainNotificationsMap));

            // Quartz: registers the background jobs, only when the scheduler is enabled.
            if (runQuartz)
            {
                containerBuilder.RegisterModule(new QuartzModule());
            }

            // Reuses the API's current-user accessor instead of creating a new one.
            containerBuilder.RegisterInstance(executionContextAccessor);

            // Create the container from all registrations above.
            _container = containerBuilder.Build();

            // Share the container with the rest of the module (e.g. PaymentsModule resolves its handlers through it).
            PaymentsCompositionRoot.SetContainer(_container);

            // Start the projectors now that the container is ready.
            RunEventsProjectors();
        }

        // Starts the subscriptions manager, which keeps the module's read data in sync with the events it receives.
        private static void RunEventsProjectors()
        {
            // Get the manager from the container.
            _subscriptionsManager = _container.Resolve<SubscriptionsManager>();

            // Begin listening for events.
            _subscriptionsManager.Start();
        }
    }
}