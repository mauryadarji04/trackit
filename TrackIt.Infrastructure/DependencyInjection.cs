using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrackIt.Core;
using TrackIt.Core.Interfaces;
using TrackIt.Core.Services;
using TrackIt.Infrastructure.Data;
using TrackIt.Infrastructure.Identity;
using TrackIt.Infrastructure.Integrations;
using TrackIt.Infrastructure.Repositories;

namespace TrackIt.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers every layer. Controllers only ever see the interfaces.</summary>
    public static IServiceCollection AddTrackIt(this IServiceCollection services, IConfiguration config)
    {
        var connectionString = config.GetConnectionString("TrackIt")
            ?? throw new InvalidOperationException("Connection string 'TrackIt' is missing.");

        // EF Core: Identity, projects, comments
        services.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

        services.AddIdentity<ApplicationUser, IdentityRole>(o =>
            {
                o.Password.RequiredLength = 8;
                o.Password.RequireNonAlphanumeric = false;
                o.User.RequireUniqueEmail = true;
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        // Dapper: stored-procedure workflows
        services.AddSingleton<ISqlConnectionFactory>(new SqlConnectionFactory(connectionString));

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        // Integrations
        services.Configure<SmtpOptions>(config.GetSection("Smtp"));
        services.Configure<WebhookOptions>(config.GetSection("Webhook"));
        services.AddScoped<IEmailService, SmtpEmailService>();
        services.AddHttpClient<IWebhookService, WebhookService>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IPdfReportGenerator, QuestPdfReportGenerator>();

        // Services (business rules)
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
