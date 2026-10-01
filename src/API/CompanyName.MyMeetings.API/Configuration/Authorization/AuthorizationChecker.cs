using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace CompanyName.MyMeetings.API.Configuration.Authorization
{
    // Verifies at startup that every controller action is protected by a permission attribute.
    public static class AuthorizationChecker
    {
        // Throws if any public controller action lacks HasPermission or NoPermissionRequired.
        public static void CheckAllEndpoints()
        {
            // Find every controller class in the API assembly.
            var assembly = typeof(Startup).Assembly;
            var allControllerTypes = assembly.GetTypes().Where(x => x.IsSubclassOf(typeof(ControllerBase)));

            List<string> notProtectedActionMethods = [];
            foreach (var controllerType in allControllerTypes)
            {
                // A permission attribute on the controller covers all its actions, so skip it.
                var controllerHasPermissionAttribute = controllerType.GetCustomAttribute<HasPermissionAttribute>();
                if (controllerHasPermissionAttribute != null)
                {
                    continue;
                }

                // Action methods = public methods declared on the controller itself (not inherited).
                var actionMethods = controllerType.GetMethods()
                    .Where(x => x.IsPublic && x.DeclaringType == controllerType)
                    .ToList();

                foreach (var publicMethod in actionMethods)
                {
                    // Each action must either require a permission or be explicitly marked as open.
                    var hasPermissionAttribute = publicMethod.GetCustomAttribute<HasPermissionAttribute>();
                    if (hasPermissionAttribute == null)
                    {
                        var noPermissionRequired = publicMethod.GetCustomAttribute<NoPermissionRequiredAttribute>();

                        if (noPermissionRequired == null)
                        {
                            // Neither attribute present: record it as unprotected.
                            notProtectedActionMethods.Add($"{controllerType.Name}.{publicMethod.Name}");
                        }
                    }
                }
            }

            // Fail fast: report all unprotected actions at once so the app won't start misconfigured.
            if (notProtectedActionMethods.Any())
            {
                var errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("Invalid authorization configuration: ");

                foreach (var notProtectedActionMethod in notProtectedActionMethods)
                {
                    errorBuilder.AppendLine($"Method {notProtectedActionMethod} is not protected. ");
                }

                throw new ApplicationException(errorBuilder.ToString());
            }
        }
    }
}