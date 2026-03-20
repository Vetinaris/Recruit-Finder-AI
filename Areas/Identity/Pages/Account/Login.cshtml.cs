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

            if (user.PasswordExpiration.HasValue && user.PasswordExpiration.Value <= DateTime.UtcNow)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Login blocked: Password expired", false, user.Id);
                return RedirectToPage("./ForgotPassword", new { area = "Identity" });
            }

            var result = await _signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "User logged in successfully via Web UI", true, user.Id);
                _logger.LogInformation("User logged in.");

                var roles = await _userManager.GetRolesAsync(user);

                if (roles.Contains("ADMIN"))
                {
                    return RedirectToAction("Index", "Admin");
                }

                if (roles.Contains("MODERATOR"))
                {
                    return RedirectToAction("Index", "Admin");
                }

                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Account locked out", false, user.Id);
                return RedirectToPage("./Lockout");
            }
            else
            {
                await _auditService.LogAsync(Input.Email, "LOGIN", "Invalid password attempt", false, user.Id);
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
        }
        return Page();
    }
}
