using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Helpers;

namespace RaceDay.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Please enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address."), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please choose a password."), DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Your password must be at least 8 characters long.")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Participant";

    [Display(Name = "Organiser sign-up code")]
    public string? OrganiserCode { get; set; }
}

public class HomeViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Search { get; set; }
    public string? EventType { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public string? Error { get; set; }
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<CategoryDto> Categories { get; set; } = new();
    public HashSet<int> EnrolledCategoryIds { get; set; } = new();
    public bool IsOwner { get; set; }
}

public class ProfileViewModel
{
    [Required(ErrorMessage = "Please enter your full name."), StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address."), EmailAddress(ErrorMessage = "Please enter a valid email address."), StringLength(150)]
    public string Email { get; set; } = string.Empty;

    public string? ProfilePictureUrl { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class EventFormViewModel
{
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter the event name."), StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(4000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Please choose the event date and time.")]
    [Display(Name = "Date and time")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Please enter the event location."), StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999.99", ErrorMessage = "Distance must be between 0.01 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal Distance { get; set; }

    [Required(ErrorMessage = "Please choose an event type.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Display(Name = "Banner image")]
    public IFormFile? BannerFile { get; set; }

    public string? ExistingBannerUrl { get; set; }
}

public class CategoryFormViewModel
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Please enter a category name."), StringLength(100)]
    [Display(Name = "Category name")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999.99", ErrorMessage = "Distance must be between 0.01 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal Distance { get; set; }

    [Range(typeof(decimal), "0", "99999.99", ErrorMessage = "The entry fee must be between 0 and 99999.99.")]
    [Display(Name = "Entry fee (R)")]
    public decimal EntryFee { get; set; }
}

public class CategoriesViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<CategoryDto> Categories { get; set; } = new();
    public CategoryFormViewModel NewCategory { get; set; } = new();
}

public class EnrolmentsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<EnrolmentDto> Enrolments { get; set; } = new();
}

public class ResultRowViewModel
{
    public EnrolmentDto Enrolment { get; set; } = null!;
    public ResultDto? Result { get; set; }
}

public class ResultsViewModel
{
    public EventDto Event { get; set; } = null!;
    public List<ResultRowViewModel> Rows { get; set; } = new();
}

public class ResultFormViewModel
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }
    public int? ResultId { get; set; }
    public string? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = "Finished";
}

public class OrganiserDashboardViewModel
{
    public List<OrganiserEventDto> Events { get; set; } = new();
    public string? Error { get; set; }

    public int TotalEvents => Events.Count;
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
    public List<OrganiserEventDto> Upcoming => Events.Where(e => UiHelpers.IsUpcoming(e.EventDate)).OrderBy(e => e.EventDate).ToList();
    public List<OrganiserEventDto> Past => Events.Where(e => !UiHelpers.IsUpcoming(e.EventDate)).OrderByDescending(e => e.EventDate).ToList();
    public OrganiserEventDto? NextEvent => Upcoming.FirstOrDefault();
}
