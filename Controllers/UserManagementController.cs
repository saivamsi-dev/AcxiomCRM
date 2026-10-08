using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Policy = "AdminOnly")]
public class UserManagementController : Controller
{
    private static readonly string[] CrmRoles = ["Admin", "Manager", "SalesExecutive"];

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<UserManagementController> _logger;

    public UserManagementController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<UserManagementController> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm)
    {
        var usersQuery = _userManager.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchPattern = $"%{searchTerm.Trim()}%";
            usersQuery = usersQuery.Where(user =>
                EF.Functions.ILike(user.FullName, searchPattern) ||
                (user.Email != null && EF.Functions.ILike(user.Email, searchPattern)) ||
                (user.UserName != null && EF.Functions.ILike(user.UserName, searchPattern)));
        }

        var users = await usersQuery
            .OrderBy(user => user.FullName)
            .ToListAsync();

        var rows = new List<UserListItemViewModel>(users.Count);
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var isLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
            rows.Add(new UserListItemViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                Roles = roles.Count == 0 ? "No role" : string.Join(", ", roles),
                AccountStatus = isLocked ? "Locked" : "Active"
            });
        }

        return View(new UserListViewModel
        {
            SearchTerm = searchTerm,
            Users = rows
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new UserCreateViewModel
        {
            AvailableRoles = await GetAvailableRolesAsync()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        model.AvailableRoles = await GetAvailableRolesAsync();
        if (!await IsAvailableCrmRoleAsync(model.SelectedRole))
        {
            ModelState.AddModelError(nameof(model.SelectedRole), "Select an existing CRM role.");
        }

        if (ModelState.IsValid)
        {
            var email = model.Email.Trim();
            var normalizedEmail = _userManager.NormalizeEmail(email);
            if (await _userManager.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail))
            {
                ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalized = model.Email.Trim();
        var user = new ApplicationUser
        {
            UserName = normalized,
            Email = normalized,
            FullName = model.FullName.Trim(),
            LockoutEnabled = true
        };

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var createResult = await _userManager.CreateAsync(user, model.Password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(createResult);
                return View(model);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, model.SelectedRole);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(roleResult);
                return View(model);
            }

            await transaction.CommitAsync();
        }
        catch (Exception exception) when (exception is DbUpdateException or Npgsql.PostgresException)
        {
            await transaction.RollbackAsync();
            _logger.LogError(exception, "Failed to create user {Email}.", normalized);
            ModelState.AddModelError(string.Empty, "The user could not be created. Please verify the details and try again.");
            return View(model);
        }

        TempData["SuccessMessage"] = "User created successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var model = new UserEditViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            SelectedRole = (await _userManager.GetRolesAsync(user)).FirstOrDefault(IsCrmRole) ?? string.Empty,
            IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            AvailableRoles = await GetAvailableRolesAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, UserEditViewModel model)
    {
        model.AvailableRoles = await GetAvailableRolesAsync();
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!await IsAvailableCrmRoleAsync(model.SelectedRole))
        {
            ModelState.AddModelError(nameof(model.SelectedRole), "Select an existing CRM role.");
        }

        if (ModelState.IsValid)
        {
            var normalizedEmail = _userManager.NormalizeEmail(model.Email.Trim());
            if (await _userManager.Users.AnyAsync(other =>
                other.Id != user.Id && other.NormalizedEmail == normalizedEmail))
            {
                ModelState.AddModelError(nameof(model.Email), "A user with this email already exists.");
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var isCurrentlyAdmin = currentRoles.Contains("Admin", StringComparer.Ordinal);
        var removesLastAdmin = isCurrentlyAdmin && model.SelectedRole != "Admin";
        var locksSelfAsLastAdmin = isCurrentlyAdmin &&
            user.Id == _userManager.GetUserId(User) &&
            model.IsLocked;

        if (removesLastAdmin || locksSelfAsLastAdmin)
        {
            var admins = await _userManager.GetUsersInRoleAsync("Admin");
            if (admins.Count <= 1)
            {
                ModelState.AddModelError(nameof(model.SelectedRole),
                    "The last Admin account cannot be demoted or locked. Assign another Admin first.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            user.FullName = model.FullName.Trim();
            user.Email = model.Email.Trim();
            user.UserName = user.Email;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(updateResult);
                return View(model);
            }

            var crmRolesToRemove = currentRoles
                .Where(IsCrmRole)
                .Where(role => role != model.SelectedRole)
                .ToArray();
            if (crmRolesToRemove.Length > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, crmRolesToRemove);
                if (!removeResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddIdentityErrors(removeResult);
                    return View(model);
                }
            }

            if (!currentRoles.Contains(model.SelectedRole, StringComparer.Ordinal))
            {
                var addResult = await _userManager.AddToRoleAsync(user, model.SelectedRole);
                if (!addResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    AddIdentityErrors(addResult);
                    return View(model);
                }
            }

            var lockoutEnabledResult = await _userManager.SetLockoutEnabledAsync(user, true);
            if (!lockoutEnabledResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(lockoutEnabledResult);
                return View(model);
            }

            var lockoutResult = await _userManager.SetLockoutEndDateAsync(
                user,
                model.IsLocked ? DateTimeOffset.MaxValue : null);
            if (!lockoutResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(lockoutResult);
                return View(model);
            }

            if (!model.IsLocked)
            {
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            await transaction.CommitAsync();
        }
        catch (Exception exception) when (exception is DbUpdateException or Npgsql.PostgresException)
        {
            await transaction.RollbackAsync();
            _logger.LogError(exception, "Failed to update user {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty, "The user could not be updated. Please verify the details and try again.");
            return View(model);
        }

        TempData["SuccessMessage"] = "User updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<List<string>> GetAvailableRolesAsync()
    {
        return await _roleManager.Roles
            .Where(role => role.Name != null && CrmRoles.Contains(role.Name))
            .OrderBy(role => role.Name)
            .Select(role => role.Name!)
            .ToListAsync();
    }

    private async Task<bool> IsAvailableCrmRoleAsync(string? roleName)
    {
        return !string.IsNullOrWhiteSpace(roleName) &&
            IsCrmRole(roleName) &&
            await _roleManager.RoleExistsAsync(roleName);
    }

    private static bool IsCrmRole(string roleName)
    {
        return CrmRoles.Contains(roleName, StringComparer.Ordinal);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}