using Microsoft.Extensions.Logging.Abstractions;
using TrackIt.Core;
using TrackIt.Core.Dtos;
using TrackIt.Core.Services;
using Xunit;

namespace TrackIt.Tests;

public class TicketServiceTests
{
    private readonly FakeTicketRepository _tickets = new();
    private readonly FakeCommentRepository _comments = new();
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeEmailService _email = new();
    private readonly FakeWebhookService _webhook = new();
    private readonly TicketService _service;

    private static readonly CurrentUser Admin = new("admin", Roles.Admin);
    private static readonly CurrentUser Dev = new("dev1", Roles.Developer);
    private static readonly CurrentUser OtherDev = new("dev2", Roles.Developer);
    private static readonly CurrentUser Reporter = new("rep1", Roles.Reporter);
    private static readonly CurrentUser OtherReporter = new("rep2", Roles.Reporter);

    public TicketServiceTests()
    {
        _service = new TicketService(_tickets, _comments, _projects, _email, _webhook,
            NullLogger<TicketService>.Instance);
    }

    private static CreateTicketRequest ValidRequest(string priority = TicketPriority.Medium) => new()
    {
        Title = "  Crash on save  ", Description = "Steps to reproduce", ProjectId = 1, Priority = priority,
    };

    // ---------- Create ----------

    [Fact]
    public async Task Create_TrimsTitleAndStoresReporter()
    {
        var id = await _service.CreateTicketAsync(ValidRequest(), Reporter);

        Assert.Equal("Crash on save", _tickets.Tickets[id].Title);
        Assert.Equal("rep1", _tickets.Tickets[id].ReporterId);
    }

    [Fact]
    public async Task Create_ByDeveloper_IsForbidden()
    {
        await Assert.ThrowsAsync<ForbiddenException>(() => _service.CreateTicketAsync(ValidRequest(), Dev));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Create_WithBlankTitle_IsRejected(string title)
    {
        var request = ValidRequest();
        request.Title = title;

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateTicketAsync(request, Reporter));
    }

    [Fact]
    public async Task Create_WithUnknownPriority_IsRejected()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.CreateTicketAsync(ValidRequest("Urgent"), Reporter));
    }

    [Fact]
    public async Task Create_InInactiveProject_IsRejected()
    {
        var request = ValidRequest();
        request.ProjectId = 2;

        await Assert.ThrowsAsync<BusinessRuleException>(() => _service.CreateTicketAsync(request, Reporter));
    }

    [Fact]
    public async Task Create_Critical_PostsWebhook()
    {
        var id = await _service.CreateTicketAsync(ValidRequest(TicketPriority.Critical), Reporter);

        Assert.Equal(new[] { id }, _webhook.Notified);
    }

    [Fact]
    public async Task Create_NonCritical_DoesNotPostWebhook()
    {
        await _service.CreateTicketAsync(ValidRequest(TicketPriority.High), Reporter);

        Assert.Empty(_webhook.Notified);
    }

    // ---------- Assign ----------

    [Fact]
    public async Task Assign_ByNonAdmin_IsForbidden()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");

        await Assert.ThrowsAsync<ForbiddenException>(() => _service.AssignAsync(1, "dev1", Dev));
        Assert.Empty(_tickets.AssignCalls);
    }

    [Fact]
    public async Task Assign_ByAdmin_CallsRepositoryAndEmailsAssignee()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");

        await _service.AssignAsync(1, "dev1", Admin);

        Assert.Single(_tickets.AssignCalls);
        Assert.Contains(_email.Sent, m => m.To == "dev1@example.com");
    }

    [Fact]
    public async Task Assign_WhenEmailFails_StillSucceeds()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");
        _email.ShouldFail = true;

        await _service.AssignAsync(1, "dev1", Admin);

        Assert.Single(_tickets.AssignCalls);
    }

    // ---------- Status changes ----------

    [Fact]
    public async Task ChangeStatus_AssignedDeveloper_CanStartWork()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1", "dev1");

        await _service.ChangeStatusAsync(1, TicketStatus.InProgress, Dev);

        Assert.Equal(TicketStatus.InProgress, _tickets.Tickets[1].Status);
    }

    [Fact]
    public async Task ChangeStatus_DeveloperNotAssigned_IsForbidden()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1", "dev1");

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ChangeStatusAsync(1, TicketStatus.InProgress, OtherDev));
    }

    [Fact]
    public async Task ChangeStatus_DeveloperCannotClose()
    {
        _tickets.Add(1, TicketStatus.Resolved, "rep1", "dev1");

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ChangeStatusAsync(1, TicketStatus.Closed, Dev));
    }

    [Fact]
    public async Task ChangeStatus_ReporterCannotChangeStatus()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1", "dev1");

        await Assert.ThrowsAsync<ForbiddenException>(
            () => _service.ChangeStatusAsync(1, TicketStatus.InProgress, Reporter));
    }

    [Fact]
    public async Task ChangeStatus_CannotSkipSteps()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1", "dev1");

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.ChangeStatusAsync(1, TicketStatus.Closed, Admin));
        Assert.Empty(_tickets.StatusCalls);
    }

    [Fact]
    public async Task ChangeStatus_MissingTicket_IsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.ChangeStatusAsync(99, TicketStatus.InProgress, Admin));
    }

    [Fact]
    public async Task ChangeStatus_Closing_EmailsReporterAndAssignee()
    {
        _tickets.Add(1, TicketStatus.Resolved, "rep1", "dev1");

        await _service.ChangeStatusAsync(1, TicketStatus.Closed, Admin);

        Assert.Equal(2, _email.Sent.Count);
        Assert.Contains(_email.Sent, m => m.To == "rep1@example.com");
        Assert.Contains(_email.Sent, m => m.To == "dev1@example.com");
    }

    [Fact]
    public async Task ChangeStatus_Reopen_AllowedFromClosedForAdmin()
    {
        _tickets.Add(1, TicketStatus.Closed, "rep1", "dev1");

        await _service.ChangeStatusAsync(1, TicketStatus.Reopened, Admin);

        Assert.Equal(TicketStatus.Reopened, _tickets.Tickets[1].Status);
    }

    // ---------- Visibility ----------

    [Fact]
    public async Task Search_ReporterIsLimitedToOwnTickets_EvenIfFilterSaysOtherwise()
    {
        var filter = new TicketFilter { ReporterId = "someone-else" };

        await _service.SearchAsync(filter, Reporter);

        Assert.Equal("rep1", _tickets.LastFilter!.ReporterId);
    }

    [Fact]
    public async Task Search_DeveloperIsLimitedToAssignedTickets()
    {
        await _service.SearchAsync(new TicketFilter(), Dev);

        Assert.Equal("dev1", _tickets.LastFilter!.AssigneeId);
    }

    [Fact]
    public async Task Search_AdminKeepsRequestedFilters()
    {
        await _service.SearchAsync(new TicketFilter { AssigneeId = "dev2", Status = "Open" }, Admin);

        Assert.Equal("dev2", _tickets.LastFilter!.AssigneeId);
        Assert.Equal("Open", _tickets.LastFilter.Status);
    }

    [Fact]
    public async Task Search_ClampsPageSize()
    {
        await _service.SearchAsync(new TicketFilter { Page = 0, PageSize = 5000 }, Admin);

        Assert.Equal(1, _tickets.LastFilter!.Page);
        Assert.Equal(10, _tickets.LastFilter.PageSize);
    }

    [Fact]
    public async Task GetTicket_ReporterCannotSeeSomeoneElsesTicket()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetTicketAsync(1, OtherReporter));
    }

    [Fact]
    public async Task GetTicket_ReporterSeesOwnTicket()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");

        var ticket = await _service.GetTicketAsync(1, Reporter);

        Assert.Equal(1, ticket.TicketId);
    }

    // ---------- Comments ----------

    [Fact]
    public async Task AddComment_Blank_IsRejected()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddCommentAsync(1, "   ", null, Reporter));
    }

    [Fact]
    public async Task AddComment_StoresTrimmedTextForUser()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");

        await _service.AddCommentAsync(1, "  Still failing  ", null, Reporter);

        var added = Assert.Single(_comments.Added);
        Assert.Equal("Still failing", added.CommentText);
        Assert.Equal("rep1", added.UserId);
    }

    [Fact]
    public async Task AddComment_ReplyToCommentOnAnotherTicket_IsRejected()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");
        _tickets.Add(2, TicketStatus.Open, "rep1");
        _comments.Comments.Add(new CommentDto { CommentId = 7, TicketId = 2, CommentText = "elsewhere" });

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => _service.AddCommentAsync(1, "reply", 7, Reporter));
    }

    [Fact]
    public async Task AddComment_OnTicketUserCannotSee_IsNotFound()
    {
        _tickets.Add(1, TicketStatus.Open, "rep1");

        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AddCommentAsync(1, "hello", null, OtherReporter));
    }
}
