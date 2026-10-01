using CompanyName.MyMeetings.BuildingBlocks.Application;
using CompanyName.MyMeetings.Modules.UserAccess.Application.Authorization.GetUserPermissions;
using CompanyName.MyMeetings.Modules.UserAccess.Application.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace CompanyName.MyMeetings.API.Configuration.Authorization
{
    // Decides whether the current user holds the permission named in a [HasPermission] attribute on a controller or action.
    internal class HasPermissionAuthorizationHandler : AttributeAuthorizationHandler<
        HasPermissionAuthorizationRequirement, HasPermissionAttribute>
    {
        private readonly IExecutionContextAccessor _executionContextAccessor;
        private readonly IUserAccessModule _userAccessModule;

        // Receives the current-user accessor and the UserAccess module used to look up permissions.
        public HasPermissionAuthorizationHandler(
            IExecutionContextAccessor executionContextAccessor,
            IUserAccessModule userAccessModule)
        {
            _executionContextAccessor = executionContextAccessor;
            _userAccessModule = userAccessModule;
        }

        // Called by ASP.NET for each request to a protected endpoint; marks the authorization as failed or succeeded.
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            HasPermissionAuthorizationRequirement requirement,
            HasPermissionAttribute attribute)
        {
            // Ask the UserAccess module for all permissions of the logged-in user.
            var permissions = await _userAccessModule.ExecuteQueryAsync(new GetUserPermissionsQuery(_executionContextAccessor.UserId));

            // The user lacks the permission required by the attribute: deny access.
            if (!await AuthorizeAsync(attribute.Name, permissions))
            {
                context.Fail();
                return;
            }

            // The user has the permission: grant access.
            context.Succeed(requirement);
        }

        // Checks whether the user's permissions include the required one (by code).
        private Task<bool> AuthorizeAsync(string permission, List<UserPermissionDto> permissions)
        {
            // WARNING: in non-Debug builds (e.g. Release) the check is skipped and every user is allowed.
#if !DEBUG
            return Task.FromResult(true);
#endif
            // Debug builds only: the real check, true if any permission code matches the required one.
#pragma warning disable CS0162 // Unreachable code detected
            return Task.FromResult(permissions.Any(x => x.Code == permission));
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
}