using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TrackIt.Core.Entities;

namespace TrackIt.Infrastructure.Data;

/// <summary>Our user: the standard Identity user plus a display name and an active flag.</summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// EF Core is used for Identity, projects and comments (simple CRUD).
/// The tables are created by Database/01_Tables.sql, NOT by EF migrations.
/// </summary>
public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Project>(e =>
        {
            e.ToTable("Projects");
            e.HasKey(p => p.ProjectId);
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Description).HasMaxLength(500);
        });

        builder.Entity<TicketComment>(e =>
        {
            e.ToTable("TicketComments");
            e.HasKey(c => c.CommentId);
            e.Property(c => c.CommentText).HasMaxLength(2000).IsRequired();
            e.Property(c => c.CreatedAt).HasColumnType("datetime2(0)").HasDefaultValueSql("SYSUTCDATETIME()");
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(150);
        });
    }
}
