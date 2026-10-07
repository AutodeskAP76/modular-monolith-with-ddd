using Autofac;
using Serilog;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Logging
{
    // Autofac module that makes the Meetings module logger available through dependency injection.
    internal class LoggingModule : Autofac.Module
    {
        private readonly ILogger _logger;

        // Receives the module logger to register.
        internal LoggingModule(ILogger logger)
        {
            _logger = logger;
        }

        // Registers the logger instance as a single shared ILogger.
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterInstance(_logger)
                .As<ILogger>()
                .SingleInstance();
        }
    }
}