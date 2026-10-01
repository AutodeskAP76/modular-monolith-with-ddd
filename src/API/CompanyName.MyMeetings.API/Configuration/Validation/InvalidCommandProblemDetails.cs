using CompanyName.MyMeetings.BuildingBlocks.Application;
using Microsoft.AspNetCore.Mvc;

namespace CompanyName.MyMeetings.API.Configuration.Validation
{
    // HTTP 400 error body (RFC 7807 problem details) returned when a command fails validation.
    public class InvalidCommandProblemDetails : ProblemDetails
    {
        // Builds the response from the exception thrown by the command validation.
        public InvalidCommandProblemDetails(InvalidCommandException exception)
        {
            // Short summary shown to the API caller.
            Title = "Command validation error";

            // 400 Bad Request: the caller sent invalid data.
            Status = StatusCodes.Status400BadRequest;

            // URI identifying this kind of error (placeholder domain).
            Type = "https://somedomain/validation-error";

            // Copy the individual validation messages into the response.
            Errors = exception.Errors;
        }

        // The validation error messages returned to the caller.
        public List<string> Errors { get; }
    }
}