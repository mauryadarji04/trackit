using TrackIt.Core.Dtos;

namespace TrackIt.Core.Services;

public static class CommentThreading
{
    /// <summary>Turns a flat list (oldest first) into a tree: top-level comments with nested Replies.</summary>
    public static IReadOnlyList<CommentDto> Build(IEnumerable<CommentDto> flat)
    {
        var all = flat.OrderBy(c => c.CreatedAt).ThenBy(c => c.CommentId).ToList();
        foreach (var c in all) c.Replies = new List<CommentDto>();

        var byId = all.ToDictionary(c => c.CommentId);
        var roots = new List<CommentDto>();

        foreach (var c in all)
        {
            if (c.ParentCommentId is { } pid && byId.TryGetValue(pid, out var parent))
                parent.Replies.Add(c);
            else
                roots.Add(c);
        }

        return roots;
    }
}
