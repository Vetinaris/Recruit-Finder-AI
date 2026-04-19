using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Entities;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Recruit_Finder_AI.Services;

namespace Recruit_Finder_AI.Controllers
{
    [Authorize]
    public class CvController : Controller
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public CvController(Recruit_Finder_AIContext context,
                            UserManager<ApplicationUser> userManager,
                            IHttpClientFactory httpClientFactory,
                            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> GetStatus(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs
                .Where(c => c.Id == id && c.UserId == userId)
                .Select(c => new { c.IsVerified })
                .FirstOrDefaultAsync();

            if (cv == null) return NotFound();
            return Json(cv);
        }

        [AllowAnonymous]
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateAiResult([FromBody] JsonElement result)
        {
            var apiKey = _configuration["AiService:ApiKey"];
            if (!Request.Headers.TryGetValue("X-AI-Key", out var extractedKey) || extractedKey != apiKey)
            {
                return Unauthorized("Invalid API Key");
            }

            try
            {
                int cvId = result.GetProperty("cvId").GetInt32();
                var cv = await _context.Cvs.FindAsync(cvId);

                if (cv != null)
                {
                    cv.IsVerified = true;
                    _context.Update(cv);

                    var notification = new Notification
                    {
                        UserId = cv.UserId,
                        Title = "AI Verification Complete",
                        Content = $"AI Verification for your CV is complete!",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false,
                        IsCompleted = true,
                        ActionUrl = Url.Action("Index", "Cv")
                    };
                    _context.Add(notification);
                    await _context.SaveChangesAsync();
                }
                return Ok();
            }
            catch { return BadRequest(); }
        }

        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var myCvs = await _context.Cvs
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(myCvs);
        }

        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Surname,DateOfBirth,Address,PhoneNumber,Email,ProfessionalExperience,Education,Portfolio,Languages,Skills,Interests")] Cv cv)
        {
            var userId = _userManager.GetUserId(User);
            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Title");

            if (ModelState.IsValid)
            {
                var cvCount = await _context.Cvs.CountAsync(c => c.UserId == userId);
                if (cvCount >= 5)
                {
                    TempData["ErrorMessage"] = "Limit of 5 CVs reached.";
                    return RedirectToAction(nameof(Index));
                }

                cv.UserId = userId;
                cv.CreatedAt = DateTime.UtcNow;
                cv.IsVerified = false;

                _context.Add(cv);
                await _context.SaveChangesAsync();

                _ = SendToAiVerification(cv);

                TempData["SuccessMessage"] = "CV created! AI analysis started.";
                return RedirectToAction(nameof(Index));
            }
            return View(cv);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (cv == null)
            {
                TempData["ErrorMessage"] = "CV not found.";
                return RedirectToAction(nameof(Index));
            }

            _ = SendToAiVerification(cv);

            TempData["SuccessMessage"] = "AI analysis triggered in the background!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);

            if (cv != null)
            {
                _context.Cvs.Remove(cv);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "CV deleted.";
            }
            return RedirectToAction(nameof(Index));
        }
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (cv == null) return NotFound();
            return View(cv);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var cv = await _context.Cvs.FirstOrDefaultAsync(m => m.Id == id && m.UserId == userId);
            if (cv == null) return NotFound();
            return View(cv);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Surname,DateOfBirth,Address,PhoneNumber,Email,ProfessionalExperience,Education,Portfolio,Languages,Skills,Interests")] Cv cv)
        {
            var userId = _userManager.GetUserId(User);
            var existingCv = await _context.Cvs.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

            if (existingCv == null) return NotFound();

            ModelState.Remove("User");
            ModelState.Remove("UserId");
            ModelState.Remove("Title");

            if (ModelState.IsValid)
            {
                cv.UserId = userId;
                cv.CreatedAt = existingCv.CreatedAt;
                cv.IsVerified = false;

                _context.Update(cv);
                await _context.SaveChangesAsync();

                _ = SendToAiVerification(cv);

                TempData["SuccessMessage"] = "CV updated and re-queued for analysis!";
                return RedirectToAction(nameof(Index));
            }
            return View(cv);
        }
        private async Task<bool> SendToAiVerification(Cv cv)
        {
            try
            {
                var aiUrl = _configuration["AiService:PythonServiceUrl"];
                var apiKey = _configuration["AiService:ApiKey"];

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);

                client.DefaultRequestHeaders.Add("X-AI-Key", apiKey);

                var jsonData = JsonSerializer.Serialize(new
                {
                    cvId = cv.Id,
                    fullName = $"{cv.Name} {cv.Surname}",
                    experience = cv.ProfessionalExperience,
                    education = cv.Education,
                    skills = cv.Skills,
                    languages = cv.Languages,
                    portfolio = cv.Portfolio
                });

                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(aiUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}