using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrackIt.Core;
using TrackIt.Core.Dtos;
using TrackIt.Core.Interfaces;
using TrackIt.Infrastructure.Data;

namespace TrackIt.Infrastructure.Identity;

/// <summary>Reads and manages users through ASP.NET Core Identity.</summary>
public class UserDirectory : IUserDirectory
{
    private readonly UserManager<ApplicationUser> _users;

    public UserDirectory(UserManager<ApplicationUser> users) => _users = users;

    public async Task<IReadOnlyList<UserSummary>> GetAllAsync()
    {
        var list = await _users.Users.AsNoTracking().OrderBy(u => u.FullName).ToListAsync();
        var result = new List<UserSummary>();
        foreach (var u in list) result.Add(await ToSummaryAsync(u));
        return result;
    }

    public async Task<IReadOnlyList<UserSummary>> GetDevelopersAsync()
    {
        var devs = await _users.GetUsersInRoleAsync(Roles.Developer);
        return devs.Where(u => u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new UserSummary
            {
                Id = u.Id, FullName = u.FullName, Email = u.Email, Role = Roles.Developer, IsActive = u.IsActive,
            })
            .ToList();
    }

    public async Task<UserSummary?> GetByIdAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        return user is null ? null : await ToSummaryAsync(user);
    }

    public async Task SetRoleAsync(string userId, string role)
    {
        if (!Roles.All.Contains(role)) throw new BusinessRuleException("Unknown role.");
        var user = await _users.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");

        var current = await _users.GetRolesAsync(user);
        if (current.Count > 0) await _users.RemoveFromRolesAsync(user, current);
        await _users.AddToRoleAsync(user, role);
    }

    public async Task SetActiveAsync(string userId, bool isActive)
    {
        var user = await _users.FindByIdAsync(userId) ?? throw new NotFoundException("User not found.");
        user.IsActive = isActive;
        await _users.UpdateAsync(user);
        // Changing the security stamp invalidates the user's login cookie, signing them out.
        if (!isActive) await _users.UpdateSecurityStampAsync(user);
    }

    private async Task<UserSummary> ToSummaryAsync(ApplicationUser u)
    {
        var roles = await _users.GetRolesAsync(u);
        return new UserSummary
        {
            Id = u.Id,
            FullName = u.FullName,
            Email = u.Email,
            Role = roles.FirstOrDefault() ?? string.Empty,
            IsActive = u.IsActive,
        };
    }
}
