using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using System.ComponentModel.DataAnnotations;

public class RegisterConfirmationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<RegisterConfirmationModel> _logger;
    private readonly Recruit_Finder_AIContext _context;

    public RegisterConfirmationModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<RegisterConfirmationModel> logger,
        Recruit_Finder_AIContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _context = context;
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
            var savedCode = await _userManager.GetAuthenticationTokenAsync(user, "ManualConfirm", "EmailCode");

            if (string.IsNullOrEmpty(savedCode))
            {
                savedCode = await _context.UserTokens
                    .Where(t => t.UserId == user.Id && t.Name == "EmailCode" && t.LoginProvider == "ManualConfirm")
                    .Select(t => t.Value)
                    .FirstOrDefaultAsync();
            }

            var enteredCode = Input.Code?.Trim();

            _logger.LogInformation($">>> [CONFIRM] Weryfikacja: {Input.Email}. Baza: [{savedCode}], Wpisano: [{enteredCode}]");

            if (!string.IsNullOrEmpty(savedCode) && savedCode == enteredCode)
            {
                user.EmailConfirmed = true;
                await _userManager.ResetAccessFailedCountAsync(user);

                var updateResult = await _userManager.UpdateAsync(user);

                if (updateResult.Succeeded)
                {
                    await _userManager.RemoveAuthenticationTokenAsync(user, "ManualConfirm", "EmailCode");

                    _logger.LogInformation($">>> [CONFIRM] Sukces dla {user.Email}.");

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return RedirectToPage("/Index");
                }
            }
        }

        _logger.LogWarning($">>> [CONFIRM] Błędny kod dla {Input.Email}.");
        ModelState.AddModelError(string.Empty, "Invalid confirmation code. Please try again.");
        return Page();
    }
}