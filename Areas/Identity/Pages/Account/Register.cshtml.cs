using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using Recruit_Finder_AI.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

public class RegisterModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<RegisterModel> _logger;
    private readonly AuditService _auditService;
    private readonly Recruit_Finder_AIContext _context;
    private readonly SettingsService _settingsService;
    private readonly EmailService _emailService;

    public RegisterModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger,
        AuditService auditService,
        Recruit_Finder_AIContext context,
        SettingsService settingsService,
        EmailService emailService)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _auditService = auditService;
        _context = context;
        _settingsService = settingsService;
        _emailService = emailService;
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
            ModelState.AddModelError(string.Empty, "Registration is disabled.");
            return Page();
        }

        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                PasswordExpiration = DateTime.UtcNow.AddDays(settings.PasswordExpirationDays),
                EmailConfirmed = false
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

                _logger.LogInformation($">>> [REGISTER] Konto utworzone dla {user.Email}. Przekierowanie do wysyłki kodu.");

                return RedirectToPage("./SendCode", new { email = user.Email });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
        return Page();
    }
}