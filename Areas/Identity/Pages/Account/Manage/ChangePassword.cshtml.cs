using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Recruit_Finder_AI.Areas.Identity.Pages.Account.Manage
{
    public class ChangePasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ILogger<ChangePasswordModel> _logger;
        private readonly Recruit_Finder_AIContext _context;
        private readonly SettingsService _settingsService;


        public ChangePasswordModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ILogger<ChangePasswordModel> logger,
            Recruit_Finder_AIContext context,
            SettingsService settingsService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public class InputModel
        {
            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "Current password")]
            public string OldPassword { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "New password")]
            public string NewPassword { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm new password")]
            [Compare("NewPassword", ErrorMessage = "The new password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            var hasPassword = await _userManager.HasPasswordAsync(user);
            if (!hasPassword)
            {
                return RedirectToPage("./SetPassword");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var settings = await _settingsService.GetAdminSettingsAsync();
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound("User not found.");

            if (Input.NewPassword.Length < settings.MinPasswordLength)
            {
                ModelState.AddModelError(string.Empty, $"New password must be at least {settings.MinPasswordLength} characters.");
                return Page();
            }

            var passwordHistory = await _context.PasswordHistories
                .Where(ph => ph.ApplicationUserId == user.Id)
                .OrderByDescending(ph => ph.CreatedAt)
                .Take(settings.PasswordHistoryDepth)
                .ToListAsync();

            foreach (var entry in passwordHistory)
            {
                var result = _userManager.PasswordHasher.VerifyHashedPassword(user, entry.PasswordHash, Input.NewPassword);
                if (result != PasswordVerificationResult.Failed)
                {
                    ModelState.AddModelError(string.Empty, $"You cannot reuse any of your last {settings.PasswordHistoryDepth} passwords.");
                    return Page();
                }
            }

            var changePasswordResult = await _userManager.ChangePasswordAsync(user, Input.OldPassword, Input.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                foreach (var error in changePasswordResult.Errors) ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }

            user.PasswordExpiration = DateTime.UtcNow.AddDays(settings.PasswordExpirationDays);

            _context.PasswordHistories.Add(new PasswordHistory
            {
                ApplicationUserId = user.Id,
                PasswordHash = user.PasswordHash,
                CreatedAt = DateTime.UtcNow
            });

            await _userManager.UpdateAsync(user);
            await _context.SaveChangesAsync();

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Your password has been changed.";
            return RedirectToPage();
        }
    }
}