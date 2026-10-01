using System.Reflection;
using Microsoft.OpenApi.Models;

namespace CompanyName.MyMeetings.API.Configuration.Extensions
{
    // Extension methods that set up Swagger/OpenAPI, the auto-generated API documentation and test UI.
    internal static class SwaggerExtensions
    {
        // Registers the Swagger generator (title, XML docs, Bearer auth) in the DI container.
        internal static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            // Configures how the OpenAPI document is generated.
            services.AddSwaggerGen(options =>
            {
                // Defines the "v1" document and the title/description shown at the top of the Swagger UI.
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MyMeetings API",
                    Version = "v1",
                    Description = "MyMeetings API for modular monolith .NET application."
                });

                // Uses the full type name (with namespace) as schema id, so same-named classes from different modules don't clash.
                options.CustomSchemaIds(t => t.ToString());

                // Locate the XML documentation file generated at build time (named after this assembly).
                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                var commentsFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var commentsFile = Path.Combine(baseDirectory, commentsFileName);

                // Shows the /// code comments as endpoint descriptions in the Swagger UI.
                options.IncludeXmlComments(commentsFile);

                // Declares that the API accepts a JWT in the "Authorization" header, enabling the "Authorize" button in the UI.
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description =
                        "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey
                });

                // Applies the "Bearer" definition above to all endpoints, so Swagger UI sends the token with each request.
                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });

            return services;
        }

        // Adds the Swagger endpoints to the HTTP pipeline: the JSON document and the browsable UI.
        internal static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
        {
            // Serves the generated OpenAPI document as JSON at /swagger/v1/swagger.json.
            app.UseSwagger();

            // Serves the interactive Swagger UI page, pointing it at the JSON document above.
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyMeetings API"); });

            return app;
        }
    }
}
