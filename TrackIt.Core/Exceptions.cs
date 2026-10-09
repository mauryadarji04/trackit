namespace TrackIt.Core;

/// <summary>A rule was broken (bad transition, wrong assignee role...). Message is safe to show to the user.</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>The ticket or record does not exist, or the user may not see it.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>The signed-in user's role does not allow this action.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
