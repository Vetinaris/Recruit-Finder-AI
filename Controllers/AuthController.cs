using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Recruit_Finder_AI.Areas.Identity.Pages.Account;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.DTO;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;

namespace DM_AI.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IConfiguration _configuration;
        private readonly AuditService _auditService;
        private readonly Recruit_Finder_AIContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly EmailService _emailService;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            AuditService auditService,
            IHttpClientFactory httpClientFactory,
            Recruit_Finder_AIContext context,
            EmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _configuration = configuration;
            _context = context;
            _auditService = auditService;
            _httpClientFactory = httpClientFactory;
            _emailService = emailService;
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] ApiRegisterDto model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = new ApplicationUser
            {
                UserName = model.Input.Username,
                Email = model.Input.Email,
                PasswordExpiration = DateTime.UtcNow.AddDays(30),
                EmailConfirmed = false
            };

            var result = await _userManager.CreateAsync(user, model.Input.Password);

            if (result.Succeeded)
            {
                _context.PasswordHistories.Add(new PasswordHistory
                {
                    ApplicationUserId = user.Id,
                    PasswordHash = user.PasswordHash,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                await _auditService.LogActionAsync(user.UserName, "REGISTER", "User registered via API, awaiting confirmation", true, user.Id);

                var random = new Random();
                string confirmationCode = random.Next(100000, 999999).ToString();

                await _userManager.SetAuthenticationTokenAsync(user, "ManualConfirm", "EmailCode", confirmationCode);
                await _emailService.SendEmailConfirmationCodeAsync(user.Email, confirmationCode);

                return Ok(new
                {
                    Message = "Registration successful. Check email.",
                    RequiresConfirmation = true
                });
            }
            return BadRequest(result.Errors);
        }

        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return BadRequest("Invalid request.");

            var savedCode = await _userManager.GetAuthenticationTokenAsync(user, "ManualConfirm", "EmailCode");

            if (savedCode != null && savedCode == model.Code)
            {
                await _userManager.RemoveAuthenticationTokenAsync(user, "ManualConfirm", "EmailCode");

                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);

                await _auditService.LogActionAsync(user.UserName, "CONFIRM_EMAIL", "Email confirmed", true, user.Id);
                return Ok(new { Message = "Email confirmed successfully." });
            }

            return BadRequest(new { Message = "Invalid or expired code." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiLoginModel model)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);


            var user = await _userManager.FindByEmailAsync(model.EmailOrUsername)
                       ?? await _userManager.FindByNameAsync(model.EmailOrUsername);

            if (user == null)
            {
                await _auditService.LogAsync(model.EmailOrUsername, "LOGIN", "Login failed: User not found", false, null);
                return Unauthorized(new { Message = "Invalid login credentials." });
            }

            var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Login failed: Account locked out", false, user.Id);
                return StatusCode(403, new { Message = "Your account is locked." });
            }

            if (!result.Succeeded)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Login failed: Invalid password", false, user.Id);
                return Unauthorized(new { Message = "Invalid login credentials." });
            }

            if (user.PasswordExpiration.HasValue && user.PasswordExpiration.Value <= DateTime.UtcNow)
            {
                await _auditService.LogAsync(user.UserName, "LOGIN", "Login blocked: Password expired", false, user.Id);
                return StatusCode(403, new
                {
                    Message = "Your password has expired.",
                    RequiresPasswordReset = true,
                    RedirectUrl = "/Identity/Account/ForgotPassword"
                });
            }

            await _auditService.LogAsync(user.UserName, "LOGIN", "User logged in successfully via API", true, user.Id);

            var token = GenerateJwtToken(user);
            await _signInManager.SignInAsync(user, isPersistent: model.RememberMe);

            return Ok(new { Token = token, RedirectUrl = "/" });
        }
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            if (string.IsNullOrEmpty(model.Email)) return BadRequest("Email is required.");

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var random = new Random();
                string simpleCode = random.Next(100000, 999999).ToString();

                await _userManager.SetAuthenticationTokenAsync(user, "ManualReset", "ResetCode", simpleCode);

                Console.WriteLine($"[DEBUG] Wysyłam do Pythona kod: {simpleCode}");

                bool success = await _emailService.SendPasswordResetCodeAsync(user.Email, simpleCode);

                if (success)
                {
                    await _auditService.LogAsync(user.UserName, "ForgotPassword", "Reset code sent", true, user.Id);
                }
            }

            return Ok(new { Message = "If your email is in our system, you will receive a reset code." });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return BadRequest("Invalid request.");

            var savedCode = await _userManager.GetAuthenticationTokenAsync(user, "ManualReset", "ResetCode");

            if (savedCode != null && savedCode == model.Code)
            {
                await _userManager.RemoveAuthenticationTokenAsync(user, "ManualReset", "ResetCode");

                var internalToken = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, internalToken, model.NewPassword);

                if (result.Succeeded)
                {
                    user.PasswordExpiration = DateTime.UtcNow.AddDays(30);
                    await _userManager.UpdateAsync(user);
                    return Ok(new { Message = "Password has been reset successfully." });
                }
                return BadRequest(result.Errors);
            }

            return BadRequest(new { Message = "Invalid or expired code." });
        }
        private string GenerateJwtToken(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
            };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"])
            );

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(
                    int.Parse(_configuration["Jwt:ExpiresInHours"] ?? "1")
                ),
                SigningCredentials = creds,
                Issuer = _configuration["Jwt:Issuer"],
                Audience = _configuration["Jwt:Audience"]
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }
    }
}
