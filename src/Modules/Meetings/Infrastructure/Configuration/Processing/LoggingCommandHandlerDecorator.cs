using CompanyName.MyMeetings.BuildingBlocks.Application;
using CompanyName.MyMeetings.Modules.Meetings.Application.Configuration.Commands;
using CompanyName.MyMeetings.Modules.Meetings.Application.Contracts;
using Serilog;
using Serilog.Context;
using Serilog.Core;
using Serilog.Events;

namespace CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration.Processing
{
    // Decorator that logs the start, success or failure of every command.
    internal class LoggingCommandHandlerDecorator<T> : ICommandHandler<T>
        where T : ICommand
    {
        private readonly ILogger _logger;
        private readonly IExecutionContextAccessor _executionContextAccessor;
        private readonly ICommandHandler<T> _decorated;

        // Receives the logger, the execution context and the wrapped handler.
        public LoggingCommandHandlerDecorator(
            ILogger logger,
            IExecutionContextAccessor executionContextAccessor,
            ICommandHandler<T> decorated)
        {
            _logger = logger;
            _executionContextAccessor = executionContextAccessor;
            _decorated = decorated;
        }

        // Logs around the wrapped handler (skipped for recurring commands) and rethrows any exception.
        public async Task Handle(T command, CancellationToken cancellationToken)
        {
            if (command is IRecurringCommand)
            {
                await _decorated.Handle(command, cancellationToken);

                return;
            }

            using (
                LogContext.Push(
                    new RequestLogEnricher(_executionContextAccessor),
                    new CommandLogEnricher(command)))
            {
                try
                {
                    this._logger.Information(
                        "Executing command {Command}",
                        command.GetType().Name);

                    await _decorated.Handle(command, cancellationToken);

                    this._logger.Information("Command {Command} processed successful", command.GetType().Name);
                }
                catch (Exception exception)
                {
                    this._logger.Error(exception, "Command {Command} processing failed", command.GetType().Name);
                    throw;
                }
            }
        }

        // Adds the command id to the log entries written while the command runs.
        private class CommandLogEnricher : ILogEventEnricher
        {
            private readonly ICommand _command;

            // Receives the command to describe.
            public CommandLogEnricher(ICommand command)
            {
                _command = command;
            }

            // Sets the "Context" log property to "Command:<id>".
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                logEvent.AddOrUpdateProperty(new LogEventProperty(
                    "Context",
                    new ScalarValue($"Command:{_command.Id.ToString()}")));
            }
        }

        // Adds the correlation id of the current HTTP request to the log entries.
        private class RequestLogEnricher : ILogEventEnricher
        {
            private readonly IExecutionContextAccessor _executionContextAccessor;

            // Receives the execution context accessor.
            public RequestLogEnricher(IExecutionContextAccessor executionContextAccessor)
            {
                _executionContextAccessor = executionContextAccessor;
            }

            // Sets the "CorrelationId" log property when a request context exists.
            public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
            {
                if (_executionContextAccessor.IsAvailable)
                {
                    logEvent.AddOrUpdateProperty(new LogEventProperty(
                        "CorrelationId",
                        new ScalarValue(_executionContextAccessor.CorrelationId)));
                }
            }
        }
    }
}