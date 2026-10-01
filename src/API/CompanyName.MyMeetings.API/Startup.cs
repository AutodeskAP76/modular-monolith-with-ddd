using Autofac;
using Autofac.Extensions.DependencyInjection;
using CompanyName.MyMeetings.API.Configuration.Authorization;
using CompanyName.MyMeetings.API.Configuration.ExecutionContext;
using CompanyName.MyMeetings.API.Configuration.Extensions;
using CompanyName.MyMeetings.API.Configuration.Validation;
using CompanyName.MyMeetings.API.Modules.Administration;
using CompanyName.MyMeetings.API.Modules.Meetings;
using CompanyName.MyMeetings.API.Modules.Payments;
using CompanyName.MyMeetings.API.Modules.UserAccess;
using CompanyName.MyMeetings.BuildingBlocks.Application;
using CompanyName.MyMeetings.BuildingBlocks.Domain;
using CompanyName.MyMeetings.BuildingBlocks.Infrastructure.Emails;
using CompanyName.MyMeetings.Modules.Administration.Infrastructure.Configuration;
using CompanyName.MyMeetings.Modules.Meetings.Infrastructure.Configuration;
using CompanyName.MyMeetings.Modules.Payments.Infrastructure.Configuration;
using CompanyName.MyMeetings.Modules.Registrations.Infrastructure.Configuration;
using CompanyName.MyMeetings.Modules.UserAccess.Infrastructure.Configuration;
using CompanyName.MyMeetings.Modules.UserAccess.Infrastructure.Configuration.Identity;
using Hellang.Middleware.ProblemDetails;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Server.HttpSys;
using Serilog;
using Serilog.Formatting.Compact;
using ILogger = Serilog.ILogger;

namespace CompanyName.MyMeetings.API
{
    // Composition root of the API: where the whole solution is wired together and started.
    // Called by the runtime (via UseStartup<Startup> in Program) in this order:
    //   1. Constructor           -> logger, configuration, endpoint authorization check.
    //   2. ConfigureServices     -> API-level services in the standard DI collection.
    //   3. ConfigureContainer    -> API-side Autofac modules (works because of AutofacServiceProviderFactory).
    //   4. Configure             -> HTTP pipeline; calls InitializeModules, which runs each module's own
    //                               *Startup.Initialize (Meetings, Administration, UserAccess, Payments, Registrations).
    // Each module builds its own isolated Autofac container in its *Startup.Initialize; this class only triggers it.
    public class Startup
    {
        private const string MeetingsConnectionString = "MeetingsConnectionString";
        private static ILogger _logger;
        private static ILogger _loggerForApi;
        private readonly IConfiguration _configuration;

        // Sets up logging, loads configuration and verifies that all endpoints have authorization defined.
        public Startup(IWebHostEnvironment env)
        {
            ConfigureLogger();

            // Load settings in order; later sources override earlier ones:
            // appsettings.json -> environment-specific file -> user secrets -> env variables prefixed "Meetings_".
            _configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json")
                .AddUserSecrets<Startup>()
                .AddEnvironmentVariables("Meetings_")
                .Build();

            _loggerForApi.Information("Connection string:" + _configuration.GetConnectionString(MeetingsConnectionString));

            // check that all controler actions are protected by a permission attribute or explicitly marked as open (NoPermissionRequired).
            AuthorizationChecker.CheckAllEndpoints();
        }

        // Registers API-level services (controllers, Swagger, auth, problem details) in the standard DI collection.
        public void ConfigureServices(IServiceCollection services)
        {
            // Enables MVC controllers so incoming HTTP requests can be routed to the API's endpoints.
            services.AddControllers();

            // Adds Swagger/OpenAPI so the API can be explored and tested from a browser UI.
            services.AddSwaggerDocumentation();

            // Configures the identity server (token issuing/validation) used to authenticate API callers.
            services.ConfigureIdentityService();

            // Gives any class access to the current HTTP request (needed to read the logged-in user and correlation id).
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // Exposes the current user/correlation info to the modules without them depending on ASP.NET directly.
            services.AddSingleton<IExecutionContextAccessor, ExecutionContextAccessor>();

            // Turns known domain exceptions into proper HTTP error responses (RFC 7807 "problem details").
            services.AddProblemDetails(x =>
            {
                // Invalid command (failed validation) -> error response listing the validation errors.
                x.Map<InvalidCommandException>(ex => new InvalidCommandProblemDetails(ex));

                // Broken business rule -> error response describing which rule was violated.
                x.Map<BusinessRuleValidationException>(ex => new BusinessRuleValidationExceptionProblemDetails(ex));
            });

            // Defines the "HasPermission" policy: requests must carry a Bearer token and pass the permission check.
            services.AddAuthorization(options =>
            {
                options.AddPolicy(HasPermissionAttribute.HasPermissionPolicyName, policyBuilder =>
                {
                    // The requirement that the handler below evaluates (does the user have the needed permission?).
                    policyBuilder.Requirements.Add(new HasPermissionAuthorizationRequirement());

                    // Authenticate using the JWT Bearer token from the Authorization header.
                    policyBuilder.AddAuthenticationSchemes("Bearer");
                });
            });

            // The logic that actually decides whether the user has the required permission; created once per request.
            services.AddScoped<IAuthorizationHandler, HasPermissionAuthorizationHandler>();
        }

        // Dependency Injection (DI) setup, using Autofac as the DI container (called because of AutofacServiceProviderFactory).
        // It tells the container which concrete class to inject whenever an interface is requested, by registering
        // one Autofac module per business module, e.g.
        // IMeetingsModule -> MeetingsModule, IAdministrationModule -> AdministrationModule,
        // IUserAccessModule -> UserAccessModule, IPaymentsModule -> PaymentsModule.
        public void ConfigureContainer(ContainerBuilder containerBuilder)
        {
            // Each call runs that module's Load method, which maps the module's interface to its implementing class
            // (e.g. controllers asking for IMeetingsModule receive a MeetingsModule).
            containerBuilder.RegisterModule(new MeetingsAutofacModule());
            containerBuilder.RegisterModule(new AdministrationAutofacModule());
            containerBuilder.RegisterModule(new UserAccessAutofacModule());
            containerBuilder.RegisterModule(new PaymentsAutofacModule());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IServiceProvider serviceProvider)
        {
            // Get the Autofac container, which the modules need in order to resolve their dependencies.
            var container = app.ApplicationServices.GetAutofacRoot();

            // Allow browser apps from any origin to call this API (open CORS policy; consider restricting in production).
            app.UseCors(builder =>
                builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());

            // Start every business module (database, messaging, background jobs) before requests arrive.
            InitializeModules(container);

            // Attaches a correlation id to each request so its log entries can be traced together.
            app.UseMiddleware<CorrelationMiddleware>();

            // Serves the Swagger UI and the OpenAPI JSON document.
            app.UseSwaggerDocumentation();

            // Plugs the identity server into the request pipeline (login/token endpoints).
            app.AddIdentityService();

            if (env.IsDevelopment())
            {
                // In development, convert unhandled exceptions to detailed problem-details responses.
                app.UseProblemDetails();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            // Redirect plain HTTP requests to HTTPS.
            app.UseHttpsRedirection();

            // Matches each request URL to a controller action.
            app.UseRouting();

            // app.UseAuthentication();
            // Enforces the authorization policies (e.g. HasPermission) on the matched endpoint.
            app.UseAuthorization();

            // Runs the matched controller action.
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }

        // Creates the Serilog logger (console + JSON file) shared by the API and the modules.
        private static void ConfigureLogger()
        {
            _logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] [{Module}] [{Context}] {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(new CompactJsonFormatter(), "logs/logs")
                .CreateLogger();

            _loggerForApi = _logger.ForContext("Module", "API");

            _loggerForApi.Information("Logger configured");
        }

        // Boots each module (Meetings, Administration, UserAccess, Payments, Registrations) with its own settings.
        private void InitializeModules(ILifetimeScope container)
        {
            // Shared by all modules so they can read the current user and correlation id.
            var httpContextAccessor = container.Resolve<IHttpContextAccessor>();
            var executionContextAccessor = new ExecutionContextAccessor(httpContextAccessor);

            // The "from" address used by every module when sending emails.
            var emailsConfiguration = new EmailsConfiguration(_configuration["EmailsConfiguration:FromEmail"]);

            // Each Initialize call builds the module's own container, connects it to the database and starts its background processing.
            // The trailing nulls are optional overrides (e.g. test doubles) left unset in production.
            MeetingsStartup.Initialize(
                _configuration.GetConnectionString(MeetingsConnectionString),
                executionContextAccessor,
                _logger,
                emailsConfiguration,
                null);

            AdministrationStartup.Initialize(
                _configuration.GetConnectionString(MeetingsConnectionString),
                executionContextAccessor,
                _logger,
                null);

            UserAccessStartup.Initialize(
                _configuration.GetConnectionString(MeetingsConnectionString),
                executionContextAccessor,
                _logger,
                emailsConfiguration,
                _configuration["Security:TextEncryptionKey"],
                null,
                null);

            PaymentsStartup.Initialize(
                _configuration.GetConnectionString(MeetingsConnectionString),
                executionContextAccessor,
                _logger,
                emailsConfiguration,
                null);

            RegistrationsStartup.Initialize(
                _configuration.GetConnectionString(MeetingsConnectionString),
                executionContextAccessor,
                _logger,
                emailsConfiguration,
                _configuration["Security:TextEncryptionKey"],
                null,
                null);
        }
    }
}