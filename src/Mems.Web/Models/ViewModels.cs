/*
 * Project: Municipal Elections Management System (MEMS)
 * Course: COMP4976
 * Authors: Abdullah Rehan, Alex Fisher (project contributors)
 * Description: Defines validated forms and presentation data for public and admin views.
 */
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Mems.Web.Models;

/// <summary>Limits municipality form binding to editable fields and image operations.</summary>
public class MunicipalityForm
{
    public int MunicipalityId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(50)] public string Province { get; set; } = "British Columbia";
    [Required, DataType(DataType.Date), Display(Name = "Election date")] public DateOnly? ElectionDate { get; set; } = new(2026, 10, 17);
    [Required, StringLength(2000)] public string Description { get; set; } = "";
    [Display(Name = "Municipality logo")] public IFormFile? Image { get; set; }
    public string? CurrentImage { get; set; }
    public bool RemoveImage { get; set; }
}

/// <summary>Carries validated seat information and authorized municipality choices.</summary>
public class PositionForm
{
    public int PositionId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Range(1,100), Display(Name = "Seats available")] public int SeatsAvailable { get; set; } = 1;
    [Range(1,int.MaxValue), Display(Name = "Municipality")] public int MunicipalityId { get; set; }
    public List<SelectListItem> Municipalities { get; set; } = [];
}

/// <summary>Separates editable candidate fields from entity metadata and navigation properties.</summary>
public class CandidateForm
{
    public int CandidateId { get; set; }
    [Required, StringLength(100)] public string Name { get; set; } = "";
    [Required, StringLength(4000), Display(Name = "Biography / profile")] public string Profile { get; set; } = "";
    [Url, StringLength(300)] public string? Website { get; set; }
    [EmailAddress, StringLength(254)] public string? Email { get; set; }
    [Range(1,int.MaxValue), Display(Name = "Municipality")] public int MunicipalityId { get; set; }
    [Range(1,int.MaxValue), Display(Name = "Position")] public int PositionId { get; set; }
    [Display(Name = "Candidate photograph")] public IFormFile? Image { get; set; }
    public string? CurrentImage { get; set; }
    public bool RemoveImage { get; set; }
    public List<SelectListItem> Municipalities { get; set; } = [];
    public List<Position> Positions { get; set; } = [];
}

/// <summary>Combines directory results, active filters, pagination, and session selection state.</summary>
public class DirectoryViewModel
{
    public string? Search { get; set; }
    public int? MunicipalityId { get; set; }
    public int? PositionId { get; set; }
    public List<Municipality> Municipalities { get; set; } = [];
    public List<Position> Positions { get; set; } = [];
    public List<Candidate> Candidates { get; set; } = [];
    public HashSet<int> SelectedIds { get; set; } = [];
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

/// <summary>Carries administrator credentials and a return URL checked by the controller.</summary>
public class LoginForm
{
    [Required, Display(Name = "Username")] public string UserName { get; set; } = "";
    [Required, DataType(DataType.Password)] public string Password { get; set; } = "";
    [Display(Name = "Remember me")] public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}

/// <summary>Contains counts already restricted to the administrator's permitted data.</summary>
public record DashboardViewModel(int Municipalities, int Positions, int Candidates);

/// <summary>Validates a password change for the signed-in administrator.</summary>
public class ChangePasswordForm
{
    [Required, DataType(DataType.Password), Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "New Password")]
    public string NewPassword { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "Confirm New Password")]
    [Compare(nameof(NewPassword), ErrorMessage = "The new password and confirmation do not match.")]
    public string ConfirmNewPassword { get; set; } = "";
}
