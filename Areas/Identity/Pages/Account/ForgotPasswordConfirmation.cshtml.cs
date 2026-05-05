using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

public class ForgotPasswordConfirmationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailService _emailService;
    private readonly ILogger<ForgotPasswordConfirmationModel> _logger;

    public ForgotPasswordConfirmationModel(
        UserManager<ApplicationUser> userManager,
        EmailService emailService,
        ILogger<ForgotPasswordConfirmationModel> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required][EmailAddress] public string Email { get; set; }
        [Required] public string Code { get; set; }
    }

    public void OnGet(string email)
    {
        Input = new InputModel { Email = email };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await _userManager.FindByEmailAsync(Input.Email);
        if (user != null)
        {
            var savedCode = await _userManager.GetAuthenticationTokenAsync(user, "ManualReset", "ResetCode");
            var enteredCode = Input.Code?.Trim();

            if (savedCode != null && savedCode == enteredCode)
            {
                return RedirectToPage("./ResetPassword", new { code = enteredCode, email = Input.Email });
            }
        }

        ModelState.AddModelError(string.Empty, "Invalid code. Please try again.");
        return Page();
    }

    public async Task<IActionResult> OnPostResendAsync()
    {
        if (string.IsNullOrEmpty(Input.Email))
        {
            return RedirectToPage("./ForgotPassword");
        }
        return RedirectToPage("./ForgotPassword", new { email = Input.Email });
    }
}