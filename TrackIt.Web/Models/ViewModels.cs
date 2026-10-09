using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using TrackIt.Core;
using TrackIt.Core.Dtos;

namespace TrackIt.Web.Models;

public class LoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required, StringLength(150), Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 8), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class CreateTicketViewModel
{
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Choose a project."), Display(Name = "Project")]
    public int ProjectId { get; set; }

    [Required]
    public string Priority { get; set; } = TicketPriority.Medium;

    public List<SelectListItem> Projects { get; set; } = new();
}

public class TicketListViewModel
{
    public TicketFilter Filter { get; set; } = new();
    public PagedResult<TicketListItem> Result { get; set; } = new();
    public List<SelectListItem> Projects { get; set; } = new();
    public List<SelectListItem> Developers { get; set; } = new();
    public bool IsAdmin { get; set; }
    public bool CanCreate { get; set; }
}

public class TicketDetailsViewModel
{
    public TicketDetail Ticket { get; set; } = new();
    public IReadOnlyList<CommentDto> Comments { get; set; } = Array.Empty<CommentDto>();
    public IReadOnlyList<AuditEntryDto> Audit { get; set; } = Array.Empty<AuditEntryDto>();
    public IReadOnlyList<string> NextStatuses { get; set; } = Array.Empty<string>();
    public List<SelectListItem> Developers { get; set; } = new();
    public bool CanAssign { get; set; }
}

public class ProjectFormViewModel
{
    public int ProjectId { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ReportViewModel
{
    [DataType(DataType.Date), Display(Name = "Week starting")]
    public DateTime WeekStart { get; set; }
}
