using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using Recruit_Finder_AI.Data;
using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegisterModel> _logger;
    private readonly AuditService _auditService;
    private readonly Recruit_Finder_AIContext _context;
    private readonly SettingsService _settingsService;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger,
        AuditService auditService,
        Recruit_Finder_AIContext context, SettingsService settingsService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _auditService = auditService;
        _context = context;
        _settingsService = settingsService;
    }

    [BindProperty]
    public InputModel Input { get; set; }

    public string ReturnUrl { get; set; }
    public IList<AuthenticationScheme> ExternalLogins { get; set; }

    public class InputModel
    {
        [Required][EmailAddress] public string Email { get; set; }
        [Required][StringLength(100, MinimumLength = 6)][DataType(DataType.Password)] public string Password { get; set; }
        [DataType(DataType.Password)][Compare("Password")] public string ConfirmPassword { get; set; }
        [Required] public bool AcceptTerms { get; set; }
    }

    public async Task OnGetAsync(string returnUrl = null)
    {
        ReturnUrl = returnUrl;
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
    }

    public async Task<IActionResult> OnPostAsync(string returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        var settings = await _settingsService.GetAdminSettingsAsync();

        if (!settings.EnableRegistration)
        {
            ModelState.AddModelError(string.Empty, "Public registration is currently disabled by the system administrator.");
            return Page();
        }

        if (!string.IsNullOrEmpty(Input.Password) && Input.Password.Length < settings.MinPasswordLength)
        {
            ModelState.AddModelError("Input.Password", $"The password must be at least {settings.MinPasswordLength} characters long.");
        }

        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                PasswordExpiration = DateTime.UtcNow.AddDays(settings.PasswordExpirationDays)
            };

            var result = await _userManager.CreateAsync(user, Input.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, "USER");

                _context.PasswordHistories.Add(new PasswordHistory
                {
                    ApplicationUserId = user.Id,
                    PasswordHash = user.PasswordHash,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                await _auditService.LogActionAsync(user.UserName, "REGISTER", "User registered via Web UI", true, user.Id);

                await _signInManager.SignInAsync(user, isPersistent: false);
                return LocalRedirect(returnUrl);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return Page();
    }

}
