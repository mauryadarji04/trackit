using TrackIt.Core;
using TrackIt.Core.Dtos;
using TrackIt.Core.Services;
using Xunit;

namespace TrackIt.Tests;

public class TicketWorkflowTests
{
    [Theory]
    [InlineData("Open", "In Progress", true)]
    [InlineData("Reopened", "In Progress", true)]
    [InlineData("In Progress", "Resolved", true)]
    [InlineData("Resolved", "Closed", true)]
    [InlineData("Resolved", "Reopened", true)]
    [InlineData("Closed", "Reopened", true)]
    [InlineData("Open", "Closed", false)]
    [InlineData("Open", "Resolved", false)]
    [InlineData("In Progress", "Closed", false)]
    [InlineData("Closed", "In Progress", false)]
    [InlineData("Unknown", "Open", false)]
    public void IsValidTransition_MatchesTheStoredProcedureRules(string from, string to, bool expected)
    {
        Assert.Equal(expected, TicketWorkflow.IsValidTransition(from, to));
    }

    [Fact]
    public void AllowedNextStatuses_Admin_SeesEveryValidMove()
    {
        var next = TicketWorkflow.AllowedNextStatuses(TicketStatus.Resolved, Roles.Admin);

        Assert.Equal(new[] { TicketStatus.Closed, TicketStatus.Reopened }, next);
    }

    [Fact]
    public void AllowedNextStatuses_Developer_CannotCloseOrReopen()
    {
        Assert.Equal(new[] { TicketStatus.Resolved },
            TicketWorkflow.AllowedNextStatuses(TicketStatus.InProgress, Roles.Developer));
        Assert.Empty(TicketWorkflow.AllowedNextStatuses(TicketStatus.Resolved, Roles.Developer));
    }

    [Fact]
    public void AllowedNextStatuses_Reporter_GetsNothing()
    {
        Assert.Empty(TicketWorkflow.AllowedNextStatuses(TicketStatus.Open, Roles.Reporter));
    }
}

public class CommentThreadingTests
{
    private static CommentDto C(int id, int? parent, int minute) => new()
    {
        CommentId = id, ParentCommentId = parent, TicketId = 1, CommentText = $"c{id}",
        CreatedAt = new DateTime(2026, 1, 1, 10, minute, 0),
    };

    [Fact]
    public void Build_NestsRepliesUnderTheirParent()
    {
        var tree = CommentThreading.Build(new[] { C(1, null, 0), C(2, 1, 1), C(3, 2, 2), C(4, null, 3) });

        Assert.Equal(new[] { 1, 4 }, tree.Select(c => c.CommentId));
        Assert.Equal(2, tree[0].Replies.Single().CommentId);
        Assert.Equal(3, tree[0].Replies[0].Replies.Single().CommentId);
        Assert.Empty(tree[1].Replies);
    }

    [Fact]
    public void Build_SortsByTimeEvenWhenInputIsShuffled()
    {
        var tree = CommentThreading.Build(new[] { C(2, null, 5), C(1, null, 1) });

        Assert.Equal(new[] { 1, 2 }, tree.Select(c => c.CommentId));
    }

    [Fact]
    public void Build_TreatsOrphanedReplyAsTopLevel()
    {
        var tree = CommentThreading.Build(new[] { C(5, 99, 0) });

        Assert.Single(tree);
    }

    [Fact]
    public void Build_CalledTwice_DoesNotDuplicateReplies()
    {
        var input = new[] { C(1, null, 0), C(2, 1, 1) };

        CommentThreading.Build(input);
        var tree = CommentThreading.Build(input);

        Assert.Single(tree[0].Replies);
    }
}
