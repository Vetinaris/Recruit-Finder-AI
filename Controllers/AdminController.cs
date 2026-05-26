using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Entities;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using Recruit_Finder_AI.ViewModels;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private readonly NotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private readonly EmailService _emailService;

        public AdminController(UserManager<ApplicationUser> userManager,
                               RoleManager<IdentityRole> roleManager,
                               Recruit_Finder_AIContext context,
                               AuditService auditService,
                               SettingsService settingsService,
                               NotificationService notificationService,
            IConfiguration configuration, EmailService emailService)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _auditService = auditService;
            _settingsService = settingsService;
            _notificationService = notificationService;
            _configuration = configuration;
            _emailService = emailService;
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
                    PrimaryRole = primaryRole,
                    BanReason = user.BanReason,
                    IsPermanentBan = user.IsPermanentBan,
                    BanDescription = user.BanDescription
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
                    PrimaryRole = primaryRole,
                    BanReason = user.BanReason,
                    IsPermanentBan = user.IsPermanentBan,
                    BanDescription = user.BanDescription
                });
            }

            var viewModel = new AdminIndexViewModel { Users = usersWithRoles };
            return View(viewModel);
        }

        [HttpGet]
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

            DateTime? passwordExpirationLocal = user.PasswordExpiration?.ToLocalTime();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                UserName = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                LockoutEndDisplay = user.LockoutEnd,
                PasswordExpiration = passwordExpirationLocal,
                ForcePasswordReset = user.PasswordExpiration.HasValue && user.PasswordExpiration.Value <= DateTime.UtcNow,
                CompanyName = user.CompanyName,
                NIP = user.NIP,
                CompanyAddress = user.CompanyAddress,
                IsEmployer = user.IsEmployer,
                ProfilePicture = user.ProfilePicture,

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
            if (user == null) return RedirectToAction("Index");

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

            bool resetEmailSent = false;

            bool isCurrentlyExpiredInDb = user.PasswordExpiration.HasValue && user.PasswordExpiration.Value <= DateTime.UtcNow;

            if (model.ForcePasswordReset)
            {
                if (!isCurrentlyExpiredInDb)
                {
                    var code = new Random().Next(100000, 999999).ToString();
                    await _userManager.RemoveAuthenticationTokenAsync(user, "ManualReset", "ResetCode");
                    await _userManager.SetAuthenticationTokenAsync(user, "ManualReset", "ResetCode", code);
                    resetEmailSent = await _emailService.SendPasswordResetCodeAsync(user.Email, code);
                }

                user.PasswordExpiration = DateTime.UtcNow.AddDays(-1);
            }
            else
            {
                user.PasswordExpiration = model.PasswordExpiration?.ToUniversalTime();

                await _userManager.RemoveAuthenticationTokenAsync(user, "ManualReset", "ResetCode");
            }

            user.Email = model.Email;
            user.UserName = model.UserName;
            user.CompanyName = model.CompanyName;
            user.NIP = model.NIP;
            user.CompanyAddress = model.CompanyAddress;
            user.IsEmployer = model.IsEmployer;

            bool isCurrentlyLocked = await _userManager.IsLockedOutAsync(user);
            if (model.IsLocked && !isCurrentlyLocked)
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            else if (!model.IsLocked && isCurrentlyLocked)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            var selectedRoles = model.AvailableRoles?.Where(r => r.IsSelected).Select(r => r.RoleName).ToList() ?? new List<string>();
            await _userManager.RemoveFromRolesAsync(user, currentRoles.Except(selectedRoles));
            await _userManager.AddToRolesAsync(user, selectedRoles.Except(currentRoles));

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);

                string msg = "User updated successfully.";
                if (resetEmailSent) msg += " New reset code has been sent.";

                TempData["SuccessMessage"] = msg;
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
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

            var code = new Random().Next(100000, 999999).ToString();

            await _userManager.RemoveAuthenticationTokenAsync(user, "ManualReset", "ResetCode");
            await _userManager.SetAuthenticationTokenAsync(user, "ManualReset", "ResetCode", code);

            user.PasswordExpiration = DateTime.UtcNow.AddDays(-1);

            await _userManager.UpdateSecurityStampAsync(user);

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                bool emailSent = await _emailService.SendPasswordResetCodeAsync(user.Email, code);
                TempData["SuccessMessage"] = $"Reset forced for {user.UserName}." + (emailSent ? " Code sent." : " Email failed.");
            }
            else
            {
                TempData["ErrorMessage"] = "Database update failed.";
            }

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
            if (user == null) return NotFound();

            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            user.IsPermanentBan = false;
            user.BanReason = null;
            user.BanDescription = null;

            var result = await _userManager.UpdateAsync(user);
            await _auditService.LogActionAsync(User.Identity.Name, "ADMIN_UNLOCK_USER", $"Unlocked {user.UserName}", result.Succeeded, user.Id);

            if (result.Succeeded) TempData["SuccessMessage"] = $"User {user.UserName} unlocked.";
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
        public async Task<IActionResult> Settings()
        {
            var model = await _settingsService.GetAdminSettingsAsync();
            return View(model);
        }
        private readonly List<string> _banReasons = new List<string>
{
    "Violation of service terms",
    "Spam and unwanted content",
    "Attempted data theft (Phishing)",
    "Hate speech / Profanity",
    "Suspicious account activity",
    "Unpaid subscription (employer)",
    "Other (requires description)"
};

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PermaBan(string id, string reason, string? customReason)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null || user.Id == _userManager.GetUserId(User)) return BadRequest();

            user.IsPermanentBan = true;
            user.BanReason = (reason == "Other" && !string.IsNullOrWhiteSpace(customReason)) ? customReason : reason;
            user.BanDescription = customReason;

            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            var result = await _userManager.UpdateAsync(user);

            string auditDetails = $"Permanent Ban. Reason: {user.BanReason}. " +
                                 (!string.IsNullOrEmpty(customReason) ? $"Additional Notes: {customReason}" : "");

            await _auditService.LogActionAsync(User.Identity.Name, "ADMIN_PERMABAN", auditDetails, result.Succeeded, user.Id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TemporaryBan(string id, int months, string reason, string? customReason)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.BanReason = (reason == "Other" && !string.IsNullOrWhiteSpace(customReason)) ? customReason : reason;
            user.BanDescription = customReason;
            user.IsPermanentBan = false;

            var result = await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMonths(months));
            await _userManager.UpdateAsync(user);

            string auditDetails = $"Temp Ban ({months} mo). Reason: {user.BanReason}. " +
                                 (!string.IsNullOrEmpty(customReason) ? $"Additional Notes: {customReason}" : "");

            await _auditService.LogActionAsync(User.Identity.Name, "ADMIN_TEMP_BAN", auditDetails, true, user.Id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(AdminSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Validation failed. Please check your input.";
                return View(model);
            }

            var success = await _settingsService.SaveAdminSettingsAsync(model);

            await _auditService.LogActionAsync(User.Identity.Name, "ADMIN_SETTINGS_UPDATE", success ? "Success" : "Failed", success, _userManager.GetUserId(User));

            if (success)
            {
                TempData["SuccessMessage"] = "Global Security Policy has been updated successfully.";
            }
            else
            {
                TempData["ErrorMessage"] = "A database error occurred while saving settings.";
            }

            return RedirectToAction(nameof(Settings));
        }

        [HttpGet]
        public async Task<IActionResult> ExportAuditLogs()
        {
            var logs = await _context.AuditLogs.OrderByDescending(l => l.Timestamp).ToListAsync();
            var builder = new StringBuilder();
            builder.AppendLine("sep=;");
            builder.AppendLine("Timestamp;Admin;Action;Status;Details");

            foreach (var log in logs)
            {
                string safeDetails = log.Details?.Replace(";", "-").Replace("\r", "").Replace("\n", " ") ?? "";
                string status = log.IsSuccess ? "Success" : "Failed";

                builder.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss};{log.UserName};{log.Action};{status};{safeDetails}");
            }

            var csvData = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
            return File(csvData, "text/csv", $"AuditLogs_{DateTime.Now:yyyyMMdd}.csv");
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN")]
        public IActionResult Guidelines()
        {
            return View();
        }
    }
}