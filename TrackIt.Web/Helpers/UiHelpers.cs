using TrackIt.Core;

namespace TrackIt.Web.Helpers;

/// <summary>Maps statuses and priorities to Bootstrap badge colours.</summary>
public static class UiHelpers
{
    public static string StatusClass(string status) => status switch
    {
        TicketStatus.Open => "text-bg-primary",
        TicketStatus.InProgress => "text-bg-warning",
        TicketStatus.Resolved => "text-bg-success",
        TicketStatus.Closed => "text-bg-secondary",
        TicketStatus.Reopened => "text-bg-danger",
        _ => "text-bg-light",
    };

    public static string PriorityClass(string priority) => priority switch
    {
        TicketPriority.Critical => "text-bg-danger",
        TicketPriority.High => "text-bg-warning",
        TicketPriority.Medium => "text-bg-info",
        _ => "text-bg-light border",
    };

    /// <summary>Shows UTC database times in the viewer's server-local format.</summary>
    public static string Stamp(DateTime? value) =>
        value is null ? "-" : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToLocalTime().ToString("dd MMM yyyy HH:mm");
}
