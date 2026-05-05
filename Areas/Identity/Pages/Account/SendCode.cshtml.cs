using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System.Security.Cryptography;

public class SendCodeModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmailService _emailService;
    private readonly ILogger<SendCodeModel> _logger;

    public SendCodeModel(UserManager<ApplicationUser> userManager, EmailService emailService, ILogger<SendCodeModel> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    [BindProperty]
    public string Email { get; set; }

    public void OnGet(string email)
    {
        Email = email;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(Email)) return Page();

        var user = await _userManager.FindByEmailAsync(Email);
        if (user != null)
        {
            string confirmationCode = RandomNumberGenerator.GetInt32(100000, 999999).ToString();

            await _userManager.SetAuthenticationTokenAsync(user, "ManualConfirm", "EmailCode", confirmationCode);

            await _emailService.SendEmailConfirmationCodeAsync(user.Email, confirmationCode);

            _logger.LogInformation($">>> [SEND_CODE] Kod wysłany dla {user.Email}: {confirmationCode}");

            return RedirectToPage("./RegisterConfirmation", new { email = user.Email });
        }

        ModelState.AddModelError(string.Empty, "Użytkownik nie został znaleziony.");
        return Page();
    }
}