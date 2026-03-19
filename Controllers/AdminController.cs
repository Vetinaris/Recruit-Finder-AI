using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Entities;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Recruit_Finder_AI.ViewModels;

namespace Recruit_Finder_AI.Controllers
{
    [Authorize(Roles = "ADMIN")]
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly Recruit_Finder_AIContext _context;
        private readonly AuditService _auditService;
        private readonly SettingsService _settingsService;

        public AdminController(UserManager<ApplicationUser> userManager,
                               RoleManager<IdentityRole> roleManager,
                               Recruit_Finder_AIContext context,
                               AuditService auditService,
                               SettingsService settingsService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _auditService = auditService;
            _settingsService = settingsService;
        }

        private async Task<AdminIndexViewModel> LoadIndexModel(AdminIndexViewModel model)
        {
            var applicationUsers = await _userManager.Users.AsNoTracking().ToListAsync();
            var userTasks = applicationUsers.Select(async user =>
            {
                var roles = await _userManager.GetRolesAsync(user);
                string primaryRole = roles.FirstOrDefault() ?? "User";


                return new UserIndexRowViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    LockoutEnd = user.LockoutEnd,
                    PasswordExpiration = user.PasswordExpiration,
                    PrimaryRole = primaryRole
                };
            });

            var usersWithRoles = await Task.WhenAll(userTasks);
            model.Users = usersWithRoles.ToList();
            return model;
        }

        public async Task<IActionResult> Index()
        {
            var applicationUsers = await _userManager.Users.ToListAsync();
            var usersWithRoles = new List<UserIndexRowViewModel>();

            foreach (var user in applicationUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                string primaryRole = roles.FirstOrDefault() ?? "User";
                usersWithRoles.Add(new UserIndexRowViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email,
                    LockoutEnd = user.LockoutEnd,
                    PasswordExpiration = user.PasswordExpiration,
                    PrimaryRole = primaryRole
                });
            }

            var viewModel = new AdminIndexViewModel
            {
                Users = usersWithRoles
            };
            return View(viewModel);
        }

        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "No user to edit found.";
                return RedirectToAction("Index");
            }
            var userRoles = await _userManager.GetRolesAsync(user);
            var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            DateTime? passwordExpirationLocal = user.PasswordExpiration.HasValue
                ? user.PasswordExpiration.Value.ToLocalTime()
                : (DateTime?)null;

            var model = new EditUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                LockoutEndDisplay = user.LockoutEnd,
                PasswordExpiration = passwordExpirationLocal,
                ForcePasswordReset = user.PasswordExpiration.HasValue && user.PasswordExpiration.Value.ToUniversalTime() <= DateTime.UtcNow,
                AvailableRoles = allRoles.Select(role => new UserRoleViewModel
                {
                    RoleName = role,
                    IsSelected = userRoles.Contains(role)
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "No user to update found.";
                return RedirectToAction("Index");
            }

            if (!ModelState.IsValid)
            {
                var allRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
                model.AvailableRoles = allRoles.Select(role => new UserRoleViewModel
                {
                    RoleName = role,
                    IsSelected = model.AvailableRoles?.Any(ar => ar.RoleName == role && ar.IsSelected) ?? false
                }).ToList();
                return View(model);
            }

            user.Email = model.Email;
            user.UserName = model.UserName;

            bool isCurrentlyLocked = await _userManager.IsLockedOutAsync(user);
            if (model.IsLocked && !isCurrentlyLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }
            else if (!model.IsLocked && isCurrentlyLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            if (model.ForcePasswordReset)
            {
                user.PasswordExpiration = DateTime.UtcNow.AddDays(-1);
            }
            else
            {
                user.PasswordExpiration = model.PasswordExpiration?.ToUniversalTime();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var selectedRoleNames = model.AvailableRoles?
                .Where(r => r.IsSelected)
                .Select(r => r.RoleName)
                .ToList() ?? new List<string>();

            var rolesToRemove = currentRoles.Except(selectedRoleNames).ToList();
            var rolesToAdd = selectedRoleNames.Except(currentRoles).ToList();

            if (rolesToRemove.Any()) await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (rolesToAdd.Any()) await _userManager.AddToRolesAsync(user, rolesToAdd);

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);

                await _auditService.LogActionAsync(
                    User.Identity.Name,
                    "ADMIN_EDIT_USER",
                    $"Edited user {user.UserName}. Roles: {string.Join(", ", selectedRoleNames)}",
                    true,
                    user.Id
                );

                TempData["SuccessMessage"] = $"User {user.UserName} updated successfully.";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForcePasswordReset(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Index");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            user.PasswordExpiration = DateTime.UtcNow.AddDays(-1);
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {

                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (Exception)
                {

                }
            }

            await _auditService.LogActionAsync(
                User.Identity.Name,
                "ADMIN_FORCE_PASSWORD_RESET",
                result.Succeeded
                    ? $"Admin forced password reset for user {user.UserName}."
                    : $"Failed to force password reset for user {user.UserName}.",
                result.Succeeded,
                user.Id
            );

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded
                    ? $"User {user.UserName} will be required to change password. Reset link: {Url.Page("/Account/ResetPassword", null, new { code, email = user.Email }, Request.Scheme)}"
                    : $"Error setting password reset for {user.UserName}.";

            return RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Index");
            }

            var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            await _auditService.LogActionAsync(
                User.Identity.Name,
                "ADMIN_LOCK_USER",
                result.Succeeded ? $"Admin locked user {user.UserName}." : $"Failed to lock user {user.UserName}.",
                result.Succeeded,
                user.Id
            );

            if (result.Succeeded)
                TempData["SuccessMessage"] = $"User {user.UserName} has been locked.";
            else
                TempData["ErrorMessage"] = "Failed to lock user.";

            return RedirectToAction("Index");
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("Index");
            }

            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            var result = await _userManager.UpdateAsync(user);

            await _auditService.LogActionAsync(
                User.Identity.Name,
                "ADMIN_UNLOCK_USER",
                result.Succeeded
                    ? $"Admin unlocked user {user.UserName}."
                    : $"Failed to unlock user {user.UserName}.",
                result.Succeeded,
                user.Id
            );

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded
                    ? $"User {user.UserName} has been unlocked."
                    : $"Failed to unlock user {user.UserName}.";

            return RedirectToAction("Index");
        }

        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> AuditLogs()
        {
            var logs = await _context.AuditLogs
                             .OrderByDescending(l => l.Timestamp)
                             .ToListAsync();
            return View(logs);
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Settings()
        {
            var model = await _settingsService.GetAdminSettingsAsync();

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found. Could not delete.";
                return RedirectToAction("Index");
            }


            if (user.Id == _userManager.GetUserId(User))
            {
                TempData["ErrorMessage"] = "You cannot delete your own admin account.";
                return RedirectToAction("Index");
            }


            var result = await _userManager.DeleteAsync(user);


            await _auditService.LogActionAsync(
                User.Identity.Name,
                "ADMIN_DELETE_USER",
                result.Succeeded
                    ? $"Admin deleted user {user.UserName} (ID: {user.Id})."
                    : $"Failed to delete user {user.UserName}.",
                result.Succeeded,
                user.Id
            );


            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded
                    ? $"User {user.UserName} has been permanently deleted."
                    : $"Failed to delete user {user.UserName}. Error: {string.Join(", ", result.Errors.Select(e => e.Description))}";

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Settings(AdminSettingsViewModel model)
        {

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var success = await _settingsService.SaveAdminSettingsAsync(model);

            await _auditService.LogActionAsync(
                User.Identity.Name,
                "ADMIN_SETTINGS_UPDATE",
                success ? "System settings modified." : "System settings modification failed.",
                success,
                _userManager.GetUserId(User)
            );

            if (success)
            {
                TempData["SuccessMessage"] = "System settings updated successfully.";

                return RedirectToAction(nameof(Settings));
            }
            else
            {
                ModelState.AddModelError(string.Empty, "An error occurred while saving settings to the database.");
                return View(model);
            }
        }
    }
}