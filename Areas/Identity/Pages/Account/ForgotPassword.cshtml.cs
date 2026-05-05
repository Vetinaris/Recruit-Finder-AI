using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using Microsoft.Extensions.Configuration;

namespace Recruit_Finder_AI.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly AuditService _auditService;

        public ForgotPasswordModel(
            UserManager<ApplicationUser> userManager,
            EmailService emailService,
            IConfiguration configuration,
            AuditService auditService)
        {
            _userManager = userManager;
            _emailService = emailService;
            _configuration = configuration;
            _auditService = auditService;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public void OnGet(string email = null)
        {
            if (!string.IsNullOrEmpty(email))
            {
                Input = new InputModel { Email = email };
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user != null)
                {
                    var random = new Random();
                    string shortCode = random.Next(100000, 999999).ToString();

                    await _userManager.SetAuthenticationTokenAsync(user, "ManualReset", "ResetCode", shortCode);
                    bool success = await _emailService.SendPasswordResetCodeAsync(Input.Email, shortCode);

                    if (success)
                    {
                        await _auditService.LogAsync(user.UserName, "FORGOT_PASSWORD_PAGE", "Code sent", true, user.Id);
                        TempData["StatusMessage"] = "A new code has been sent.";
                    }
                }

                return RedirectToPage("./ForgotPasswordConfirmation", new { email = Input.Email });
            }

            return Page();
        }
    }
}