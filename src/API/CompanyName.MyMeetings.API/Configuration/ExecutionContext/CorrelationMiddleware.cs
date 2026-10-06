namespace CompanyName.MyMeetings.API.Configuration.ExecutionContext
{
    // Middleware that assigns a new correlation ID to every incoming HTTP request.
    internal class CorrelationMiddleware
    {
        internal const string CorrelationHeaderKey = "CorrelationId";
        private readonly RequestDelegate _next;

        // Stores the next delegate in the ASP.NET Core request pipeline.
        public CorrelationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // Generates a correlation ID, adds it to the request headers, then invokes the next middleware.
        public async Task Invoke(HttpContext context)
        {
            var correlationId = Guid.NewGuid();

            context.Request?.Headers.Append(CorrelationHeaderKey, correlationId.ToString());

            await _next.Invoke(context);
        }
    }
}