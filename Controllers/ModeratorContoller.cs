using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;

namespace Recruit_Finder_AI.Controllers
{
    [Authorize(Roles = "MODERATOR", AuthenticationSchemes = "Identity.Application")]
    public class ModeratorController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public ModeratorController(UserManager<ApplicationUser> userManager, NotificationService notificationService)
        {
            _userManager = userManager;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index()
        {
            var pendingCompanies = await _userManager.Users
                .Where(u => !u.IsEmployer && !string.IsNullOrEmpty(u.NIP))
                .ToListAsync();

            return View(pendingCompanies);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.IsEmployer = true;
            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                await _notificationService.SendAsync(
                    user.Id,
                    "Company Verified",
                    "Congratulations! Your company has been verified. You can now post job offers.",
                    null
                );
                TempData["SuccessMessage"] = $"Company  {user.CompanyName} has been approved.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Decline(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.NIP = null;
            await _userManager.UpdateAsync(user);

            await _notificationService.SendAsync(
                user.Id,
                "Verification Declined",
                "Your company verification was declined. Please check your data and NIP number.",
                null
            );

            TempData["StatusMessage"] = "Verification declined and user notified.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReportFakeCompany(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var admins = await _userManager.GetUsersInRoleAsync("ADMIN");
            foreach (var admin in admins)
            {
                await _notificationService.SendAsync(
                    admin.Id,
                    "SECURITY ALERT: Fake Company",
                    $"User {user.Email} is impersonating a non-existent company (NIP: {user.NIP}). Recommended: BAN.",
                    null
                );
            }

            await _notificationService.SendAsync(
                user.Id,
                "Account Suspended",
                "Your account has been reported for providing false business information.",
                null
            );

            TempData["ErrorMessage"] = "Fraudulent user reported to Admin.";
            return RedirectToAction(nameof(Index));
        }
    }
}