namespace TrackIt.Core;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Developer = "Developer";
    public const string Reporter = "Reporter";

    public static readonly string[] All = { Admin, Developer, Reporter };
}

public static class TicketStatus
{
    public const string Open = "Open";
    public const string InProgress = "In Progress";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
    public const string Reopened = "Reopened";

    public static readonly string[] All = { Open, InProgress, Resolved, Closed, Reopened };
}

public static class TicketPriority
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";

    public static readonly string[] All = { Critical, High, Medium, Low };
}
