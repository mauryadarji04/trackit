using Dapper;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrackIt.Core;
using TrackIt.Core.Dtos;
using TrackIt.Core.Interfaces;
using TrackIt.Infrastructure.Data;

namespace TrackIt.Web;

/// <summary>
/// Runs at startup. Always makes sure the three roles exist. When Seed:DemoData is true
/// (Development) and there are no users yet, it also creates demo users, projects and tickets,
/// going through the stored procedures so the trigger and SLA logic are exercised.
/// </summary>
public class DbSeeder
{
    private readonly RoleManager<IdentityRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IProjectRepository _projects;
    private readonly ITicketRepository _tickets;
    private readonly ISqlConnectionFactory _sql;
    private readonly IConfiguration _config;
    private readonly ILogger<DbSeeder> _logger;

    public DbSeeder(
        RoleManager<IdentityRole> roles,
        UserManager<ApplicationUser> users,
        IProjectRepository projects,
        ITicketRepository tickets,
        ISqlConnectionFactory sql,
        IConfiguration config,
        ILogger<DbSeeder> logger)
    {
        _roles = roles;
        _users = users;
        _projects = projects;
        _tickets = tickets;
        _sql = sql;
        _config = config;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        foreach (var role in Roles.All)
            if (!await _roles.RoleExistsAsync(role))
                await _roles.CreateAsync(new IdentityRole(role));

        if (!_config.GetValue<bool>("Seed:DemoData")) return;
        if (await _users.Users.AnyAsync()) return;

        var password = _config["Seed:DemoPassword"] ?? "TrackIt#2026";

        var admin = await CreateUserAsync("Asha Admin", "admin@trackit.local", Roles.Admin, password);
        var dev1 = await CreateUserAsync("Dev Patel", "dev1@trackit.local", Roles.Developer, password);
        var dev2 = await CreateUserAsync("Dana Developer", "dev2@trackit.local", Roles.Developer, password);
        var rep1 = await CreateUserAsync("Rohan Reporter", "reporter1@trackit.local", Roles.Reporter, password);
        var rep2 = await CreateUserAsync("Rita Reporter", "reporter2@trackit.local", Roles.Reporter, password);

        var projects = (await _projects.GetAllAsync(activeOnly: true)).ToList();
        if (projects.Count == 0)
        {
            foreach (var name in new[] { "Customer Portal", "Billing Service", "Mobile App" })
                await _projects.AddAsync(new Core.Entities.Project { Name = name, IsActive = true });
            projects = (await _projects.GetAllAsync(activeOnly: true)).ToList();
        }

        // title, description, project index, priority, reporter, assignee, final status, age in days
        var demo = new (string Title, string Desc, int Proj, string Pri, ApplicationUser Rep, ApplicationUser? Dev, string Final, int Age)[]
        {
            ("Login page returns 500 for SSO users", "Users signing in with SSO see a server error after the redirect.", 0, TicketPriority.Critical, rep1, dev1, TicketStatus.InProgress, 0),
            ("Invoice total off by one cent", "Rounding differs between the invoice PDF and the payment screen.", 1, TicketPriority.High, rep2, dev2, TicketStatus.Resolved, 5),
            ("Dark mode text unreadable on settings", "Grey labels on a dark background fail contrast checks.", 2, TicketPriority.Low, rep1, dev1, TicketStatus.Closed, 9),
            ("Password reset email never arrives", "Reset mails are not delivered to some providers.", 0, TicketPriority.High, rep1, dev2, TicketStatus.Open, 3),
            ("App crashes when rotating the device", "Crash on the order screen after rotating to landscape.", 2, TicketPriority.Medium, rep2, dev1, TicketStatus.Resolved, 6),
            ("Export to CSV drops the last row", "The final record is missing in the exported file.", 1, TicketPriority.Medium, rep2, null, TicketStatus.Open, 10),
            ("Typo on the pricing page", "'Recieve' should be 'Receive'.", 0, TicketPriority.Low, rep1, null, TicketStatus.Open, 1),
            ("Slow search on tickets over 10k rows", "Search takes more than eight seconds on large accounts.", 1, TicketPriority.High, rep1, dev2, TicketStatus.InProgress, 4),
        };

        foreach (var d in demo)
        {
            var project = projects[d.Proj % projects.Count];
            var id = await _tickets.CreateAsync(new CreateTicketRequest
            {
                Title = d.Title, Description = d.Desc, ProjectId = project.ProjectId, Priority = d.Pri,
            }, d.Rep.Id);

            if (d.Dev is not null)
                await _tickets.AssignAsync(id, d.Dev.Id, admin.Id);

            var path = new[] { TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed };
            foreach (var step in path)
            {
                if (d.Final == TicketStatus.Open || d.Dev is null) break;
                var actor = step == TicketStatus.Closed ? admin.Id : d.Dev.Id;
                await _tickets.UpdateStatusAsync(id, step, actor);
                if (step == d.Final) break;
            }

            if (d.Age > 0) await BackdateAsync(id, d.Age);
        }

        _logger.LogInformation("Demo data created. Sign in as admin@trackit.local with the password in Seed:DemoPassword.");
    }

    private async Task<ApplicationUser> CreateUserAsync(string fullName, string email, string role, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = fullName, IsActive = true,
        };
        var result = await _users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException("Could not seed user " + email + ": " +
                string.Join("; ", result.Errors.Select(e => e.Description)));

        await _users.AddToRoleAsync(user, role);
        return user;
    }

    /// <summary>Moves a seeded ticket back in time so the dashboard has varied ages. The audit trigger ignores this update.</summary>
    private async Task BackdateAsync(int ticketId, int days)
    {
        await using var conn = _sql.Create();
        await conn.ExecuteAsync(
            @"UPDATE dbo.Tickets
              SET CreatedAt = DATEADD(DAY, -@Days, CreatedAt),
                  DueAt     = DATEADD(DAY, -@Days, DueAt)
              WHERE TicketId = @TicketId;",
            new { TicketId = ticketId, Days = days });
    }
}
