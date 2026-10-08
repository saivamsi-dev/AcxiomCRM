using System.ComponentModel.DataAnnotations;

namespace AcxiomCRM.ViewModels;

public sealed class UserListViewModel
{
    public string? SearchTerm { get; init; }
    public IReadOnlyList<UserListItemViewModel> Users { get; init; } = [];
}

public sealed class UserListItemViewModel
{
    public string Id { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string UserName { get; init; } = string.Empty;
    public string Roles { get; init; } = string.Empty;
    public string AccountStatus { get; init; } = string.Empty;
}

public sealed class UserCreateViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public string SelectedRole { get; set; } = string.Empty;

    public IReadOnlyList<string> AvailableRoles { get; set; } = [];
}

public sealed class UserEditViewModel
{
    [Required]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Role")]
    public string SelectedRole { get; set; } = string.Empty;

    [Display(Name = "Account locked")]
    public bool IsLocked { get; set; }

    public IReadOnlyList<string> AvailableRoles { get; set; } = [];
}