using Microsoft.EntityFrameworkCore;
using TrackIt.Core.Entities;
using TrackIt.Core.Interfaces;
using TrackIt.Infrastructure.Data;

namespace TrackIt.Infrastructure.Repositories;

/// <summary>Simple CRUD, so EF Core is the right tool here.</summary>
public class ProjectRepository : IProjectRepository
{
    private readonly AppDbContext _db;

    public ProjectRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Project>> GetAllAsync(bool activeOnly) =>
        await _db.Projects.AsNoTracking()
            .Where(p => !activeOnly || p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();

    public Task<Project?> GetByIdAsync(int projectId) =>
        _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.ProjectId == projectId);

    public async Task AddAsync(Project project)
    {
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Project project)
    {
        _db.Projects.Update(project);
        await _db.SaveChangesAsync();
    }
}
