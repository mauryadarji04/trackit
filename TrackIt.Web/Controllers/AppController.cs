using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TrackIt.Core;
using TrackIt.Core.Dtos;

namespace TrackIt.Web.Controllers;

/// <summary>Shared helpers: who is signed in, and turning rule violations into friendly messages.</summary>
public abstract class AppController : Controller
{
    protected CurrentUser Me
    {
        get
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? throw new InvalidOperationException("Not signed in.");
            var role = User.IsInRole(Roles.Admin) ? Roles.Admin
                     : User.IsInRole(Roles.Developer) ? Roles.Developer
                     : Roles.Reporter;
            return new CurrentUser(id, role);
        }
    }

    /// <summary>Runs a service call; rule violations become a red banner instead of a crash.</summary>
    protected async Task<bool> TryAsync(Func<Task> action, string? successMessage = null)
    {
        try
        {
            await action();
            if (successMessage is not null) TempData["Success"] = successMessage;
            return true;
        }
        catch (Exception ex) when (ex is BusinessRuleException or ForbiddenException or NotFoundException)
        {
            TempData["Error"] = ex.Message;
            return false;
        }
    }

    protected static List<SelectListItem> ToItems<T>(IEnumerable<T> source, Func<T, string> value, Func<T, string> text) =>
        source.Select(s => new SelectListItem(text(s), value(s))).ToList();
}
