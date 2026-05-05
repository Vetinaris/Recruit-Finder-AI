// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Recruit_Finder_AI.Areas.Identity.Pages.Account
{
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly Recruit_Finder_AIContext _context;
        private readonly SettingsService _settingsService;

        public ResetPasswordModel(UserManager<ApplicationUser> userManager, Recruit_Finder_AIContext context, SettingsService settingsService)
        {
            _userManager = userManager;
            _context = context;
            _settingsService = settingsService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            [Required]
            public string Code { get; set; }
        }

        public IActionResult OnGet(string code = null, string email = null)
        {
            if (code == null || email == null)
            {
                return RedirectToPage("./ForgotPassword");
            }

            Input = new InputModel
            {
                Code = code,
                Email = email
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _context.Users
                .Include(u => u.PasswordHistories)
                .FirstOrDefaultAsync(u => u.Email == Input.Email);

            if (user == null)
            {
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            var savedCode = await _userManager.GetAuthenticationTokenAsync(user, "ManualReset", "ResetCode");
            if (savedCode == null || savedCode != Input.Code.Trim())
            {
                ModelState.AddModelError(string.Empty, "Invalid code. Please try again.");
                return Page();
            }

            var settings = await _settingsService.GetAdminSettingsAsync();
            var passwordHasher = new PasswordHasher<ApplicationUser>();

            foreach (var history in user.PasswordHistories)
            {
                var verificationResult = passwordHasher.VerifyHashedPassword(user, history.PasswordHash, Input.Password);
                if (verificationResult == PasswordVerificationResult.Success)
                {
                    ModelState.AddModelError(string.Empty, $"You cannot reuse any of your last {settings.PasswordHistoryDepth} passwords.");
                    return Page();
                }
            }

            var internalToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, internalToken, Input.Password);

            if (result.Succeeded)
            {
                user.PasswordHistories.Add(new PasswordHistory
                {
                    PasswordHash = user.PasswordHash,
                    CreatedAt = DateTime.UtcNow
                });

                var historyToKeep = settings.PasswordHistoryDepth;
                var redundantHistory = user.PasswordHistories
                    .OrderByDescending(h => h.CreatedAt)
                    .Skip(historyToKeep)
                    .ToList();

                if (redundantHistory.Any())
                {
                    _context.PasswordHistories.RemoveRange(redundantHistory);
                }

                int expiryDays = settings?.PasswordExpirationDays ?? 90;
                user.PasswordExpiration = DateTime.UtcNow.AddDays(expiryDays);

                await _userManager.RemoveAuthenticationTokenAsync(user, "ManualReset", "ResetCode");

                await _context.SaveChangesAsync();

                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return Page();
        }
    }
}