using CompanyName.MyMeetings.BuildingBlocks.Domain;
using Microsoft.AspNetCore.Mvc;

namespace CompanyName.MyMeetings.API.Configuration.Validation
{
    // HTTP 409 error body (RFC 7807 problem details) returned when a business rule is violated.
    public class BusinessRuleValidationExceptionProblemDetails : ProblemDetails
    {
        // Builds the response from the exception thrown by the domain.
        public BusinessRuleValidationExceptionProblemDetails(BusinessRuleValidationException exception)
        {
            // Short summary shown to the API caller.
            Title = "Business rule broken";

            // 409 Conflict: the request is valid but conflicts with the current state or a business rule.
            Status = StatusCodes.Status409Conflict;

            // Explains which rule was broken (the rule's message).
            Detail = exception.Message;

            // URI identifying this kind of error (placeholder domain).
            Type = "https://somedomain/business-rule-validation-error";
        }
    }
}