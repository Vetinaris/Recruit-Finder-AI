using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System;
using System.ComponentModel.DataAnnotations;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditService _auditService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        AuditService auditService,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; }
    public IList<AuthenticationScheme> ExternalLogins { get; set; }
    public string ReturnUrl { get; set; }
    [TempData]
    public string ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }
        returnUrl ??= Url.Content("~/");
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            var user = await _userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                await _auditService.LogAsync(Input.Email, "LOGIN", "Login failed: User not found", false, null);
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            if (user.IsPermanentBan)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Login blocked: PERMANENT BAN", false, user.Id);
                ModelState.AddModelError(string.Empty, "Your account has been permanently banned.");
                return Page();
            }

            if (!await _userManager.IsEmailConfirmedAsync(user))
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Account deleted due to lack of confirmation during login attempt", false, user.Id);

                await _userManager.DeleteAsync(user);

                ModelState.AddModelError(string.Empty, "This account was not confirmed and has been removed. Please register again.");
                return Page();
            }

            if (user.PasswordExpiration.HasValue && user.PasswordExpiration.Value <= DateTime.UtcNow)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Login blocked: Password expired", false, user.Id);
                return RedirectToPage("./ForgotPassword", new { area = "Identity" });
            }

            var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "User logged in successfully", true, user.Id);

                var roles = await _userManager.GetRolesAsync(user);
                if (roles.Contains("ADMIN")) return RedirectToAction("Index", "Admin");
                if (roles.Contains("MODERATOR")) return RedirectToAction("Index", "Moderator");

                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Account locked out (Temporary)", false, user.Id);

                var lockoutDate = user.LockoutEnd;
                string dateMsg = lockoutDate.HasValue ? lockoutDate.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm") : "an unspecified time";

                string reasonText = !string.IsNullOrEmpty(user.BanReason)
                    ? $"Suspension reason: {user.BanReason}."
                    : "Account requires verification.";

                ModelState.AddModelError(string.Empty, $"Account suspended until {dateMsg}. {reasonText} Contact: +48 123 456 789.");
                return Page();
            }
            else
            {
                await _auditService.LogAsync(Input.Email, "LOGIN", "Invalid password attempt", false, user.Id);
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return Page();
            }
        }
        return Page();
    }
}