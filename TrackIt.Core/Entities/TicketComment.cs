namespace TrackIt.Core.Entities;

public class TicketComment
{
    public int CommentId { get; set; }
    public int TicketId { get; set; }
    public string UserId { get; set; } = string.Empty;

    /// <summary>Null for a top-level comment; otherwise the comment this one replies to.</summary>
    public int? ParentCommentId { get; set; }

    public string CommentText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
