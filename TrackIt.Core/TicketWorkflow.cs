namespace TrackIt.Core;

/// <summary>
/// The status rules, mirrored from usp_UpdateTicketStatus so the UI can show only valid
/// buttons and the service can check roles before calling the database.
/// The database remains the final authority.
/// </summary>
public static class TicketWorkflow
{
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [TicketStatus.Open]       = new[] { TicketStatus.InProgress },
        [TicketStatus.Reopened]   = new[] { TicketStatus.InProgress },
        [TicketStatus.InProgress] = new[] { TicketStatus.Resolved },
        [TicketStatus.Resolved]   = new[] { TicketStatus.Closed, TicketStatus.Reopened },
        [TicketStatus.Closed]     = new[] { TicketStatus.Reopened },
    };

    public static bool IsValidTransition(string from, string to) =>
        Transitions.TryGetValue(from, out var next) && next.Contains(to);

    /// <summary>Statuses this role may move a ticket to from its current status.</summary>
    public static IReadOnlyList<string> AllowedNextStatuses(string current, string role)
    {
        if (!Transitions.TryGetValue(current, out var next)) return Array.Empty<string>();

        return next.Where(to => IsRoleAllowed(role, to)).ToList();
    }

    /// <summary>Admin may do everything, Developer works the ticket (not close/reopen), Reporter nothing.</summary>
    public static bool IsRoleAllowed(string role, string target) => role switch
    {
        Roles.Admin     => true,
        Roles.Developer => target is TicketStatus.InProgress or TicketStatus.Resolved,
        _               => false,
    };
}
