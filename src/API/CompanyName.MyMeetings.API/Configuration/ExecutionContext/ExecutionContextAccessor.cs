using CompanyName.MyMeetings.BuildingBlocks.Application;

namespace CompanyName.MyMeetings.API.Configuration.ExecutionContext
{
    // Gives the modules access to the current user and correlation id from the HTTP request, without depending on ASP.NET.
    public class ExecutionContextAccessor : IExecutionContextAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Receives the ASP.NET accessor used to read the current HTTP request.
        public ExecutionContextAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // Id of the logged-in user, taken from the "sub" claim of the token; throws if there is no user.
        public Guid UserId
        {
            get
            {
                // Check that a request with a "sub" (subject = user id) claim exists.
                if (_httpContextAccessor
                    .HttpContext?
                    .User?
                    .Claims?
                    .SingleOrDefault(x => x.Type == "sub")?
                    .Value != null)
                {
                    // Convert the claim value to a Guid and return it as the user id.
                    return Guid.Parse(_httpContextAccessor.HttpContext.User.Claims.Single(
                        x => x.Type == "sub").Value);
                }

                throw new ApplicationException("User context is not available");
            }
        }

        // Id that ties together all log entries and operations of one request, read from the correlation request header.
        public Guid CorrelationId
        {
            get
            {
                // Check that an HTTP request exists and carries the correlation header (set by CorrelationMiddleware).
                if (IsAvailable && _httpContextAccessor.HttpContext.Request.Headers.Keys.Any(
                    x => x == CorrelationMiddleware.CorrelationHeaderKey))
                {
                    // Convert the header value to a Guid and return it.
                    return Guid.Parse(
                        _httpContextAccessor.HttpContext.Request.Headers[CorrelationMiddleware.CorrelationHeaderKey]);
                }

                throw new ApplicationException("Http context and correlation id is not available");
            }
        }

        // True when there is a current HTTP request; false e.g. in background jobs running outside a request.
        public bool IsAvailable => _httpContextAccessor.HttpContext != null;
    }
}