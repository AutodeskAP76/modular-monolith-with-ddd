namespace CompanyName.MyMeetings.BuildingBlocks.Domain
{
    // Exception thrown by the domain when a business rule is violated; carries the rule that was broken.
    public class BusinessRuleValidationException : Exception
    {
        // The business rule that failed.
        public IBusinessRule BrokenRule { get; }

        // Human-readable description of the violation (the rule's message).
        public string Details { get; }

        // Creates the exception from the broken rule and uses the rule's message as the exception message.
        public BusinessRuleValidationException(IBusinessRule brokenRule)
            : base(brokenRule.Message)
        {
            BrokenRule = brokenRule;
            this.Details = brokenRule.Message;
        }

        // Formats as "<rule type full name>: <rule message>", which is useful in logs.
        public override string ToString()
        {
            return $"{BrokenRule.GetType().FullName}: {BrokenRule.Message}";
        }
    }
}
