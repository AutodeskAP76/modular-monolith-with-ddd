using CompanyName.MyMeetings.Modules.UserAccess.Infrastructure.IdentityServer;
using IdentityServer4.AccessTokenValidation;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyName.MyMeetings.Modules.UserAccess.Infrastructure.Configuration.Identity;

// Extension methods that set up IdentityServer4, which handles login and issues/validates the JWT tokens used by the API.
public static class IdentityConfiguration
{
    // Registers IdentityServer (token issuing) and the API's token validation in the DI container.
    public static IServiceCollection ConfigureIdentityService(this IServiceCollection services)
    {
        // Adds the IdentityServer token service, configured entirely in memory (no database), fine for a demo app.
        services.AddIdentityServer()

            // The kinds of user info (claims) a client may request, e.g. user id and profile.
            .AddInMemoryIdentityResources(IdentityServerConfig.GetIdentityResources())

            // The permission scopes clients can ask for when calling the API.
            .AddInMemoryApiScopes(IdentityServerConfig.GetApiScopes())

            // The protected APIs that tokens are issued for.
            .AddInMemoryApiResources(IdentityServerConfig.GetApis())

            // The client applications allowed to request tokens.
            .AddInMemoryClients(IdentityServerConfig.GetClients())

            // Stores issued grants (e.g. refresh tokens) in memory; they are lost on restart.
            .AddInMemoryPersistedGrants()

            // Custom logic that puts the application's user claims into the issued tokens.
            .AddProfileService<ProfileService>()

            // Temporary key used to sign tokens; development only, replace with a real certificate in production.
            .AddDeveloperSigningCredential();

        // Checks username/password against the app's own users when a client logs in with the password flow.
        services.AddTransient<IResourceOwnerPasswordValidator, ResourceOwnerPasswordValidator>();

        // Makes the API validate incoming Bearer tokens using IdentityServer as the default authentication scheme.
        services.AddAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme)
            .AddIdentityServerAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme, x =>
            {
                // Address of the identity server that issued the tokens (here, the API itself on localhost).
                x.Authority = "http://localhost:5000";

                // Only accept tokens issued for this API.
                x.ApiName = "myMeetingsAPI";

                // Allows plain HTTP when fetching token metadata; acceptable locally, not in production.
                x.RequireHttpsMetadata = false;
            });

        return services;
    }

    // Adds the IdentityServer endpoints (e.g. /connect/token) to the HTTP pipeline.
    public static IApplicationBuilder AddIdentityService(this IApplicationBuilder app)
    {
        // Enables the IdentityServer middleware so clients can log in and request tokens.
        app.UseIdentityServer();
        return app;
    }
}
