namespace CompanyName.MyMeetings.BuildingBlocks.Application
{
    // Exception thrown when a command fails validation before being handled; carries the validation error messages.
    public class InvalidCommandException : Exception
    {
        // The validation error messages that made the command invalid.
        public List<string> Errors { get; }

        // Creates the exception with the list of validation errors.
        public InvalidCommandException(List<string> errors)
        {
            this.Errors = errors;
        }
    }
}
