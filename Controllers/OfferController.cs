using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Recruit_Finder_AI.Controllers
{
    public class OfferController : Controller
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;

        public OfferController(
            Recruit_Finder_AIContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IActionResult> List(
            string category,
            string? subcategory,
            string? searchString,
            string? jobType,
            string? salaryType,
            int? minSalary,
            string sortOrder,
            string language,
            bool showInactive = false)
        {
            var allOffersInCategory = await _context.JobOffers
                .Where(o => o.Category == category)
                .ToListAsync();

            var languages = allOffersInCategory
                .Where(o => !string.IsNullOrEmpty(o.RequiredLanguages))
                .SelectMany(o => o.RequiredLanguages.Split(','))
                .Select(l => l.Trim())
                .Distinct()
                .OrderBy(l => l)
                .ToList();

            var subcategories = allOffersInCategory
                .Where(o => !string.IsNullOrEmpty(o.Subcategory))
                .Select(o => o.Subcategory)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            ViewBag.CategoryName = category;
            ViewBag.Subcategories = subcategories;
            ViewBag.CurrentSubcategory = subcategory;
            ViewBag.CurrentSearch = searchString;

            ViewBag.CurrentJobType = jobType;
            ViewBag.CurrentSalaryType = salaryType;
            ViewBag.CurrentMinSalary = minSalary;
            ViewBag.CurrentSort = sortOrder;
            ViewBag.Languages = languages;
            ViewBag.CurrentLanguage = language;
            ViewBag.ShowInactive = showInactive;

            var query = _context.JobOffers.Include(o => o.User).Where(o => o.Category == category);

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.ToLower();
                query = query.Where(o => o.Title.ToLower().Contains(searchString) ||
                                         o.Company.ToLower().Contains(searchString));
            }

            if (!string.IsNullOrEmpty(subcategory))
            {
                query = query.Where(o => o.Subcategory == subcategory);
            }

            if (!string.IsNullOrEmpty(jobType))
                query = query.Where(o => o.JobType == jobType);

            if (!string.IsNullOrEmpty(language))
                query = query.Where(o => o.RequiredLanguages.Contains(language));

            if (!string.IsNullOrEmpty(salaryType))
                query = query.Where(o => o.SalaryType == salaryType);

            if (minSalary.HasValue)
            {
                query = query.Where(o =>
                    (o.MinimumSalary >= minSalary.Value) ||
                    (o.SalaryType == "negotiable") ||
                    (o.SalaryType == "none")
                );
            }

            query = sortOrder switch
            {
                "salary_desc" => query.OrderByDescending(o => o.MinimumSalary),
                "salary_asc" => query.OrderBy(o => o.MinimumSalary),
                "title" => query.OrderBy(o => o.Title),
                _ => query.OrderByDescending(o => o.CreatedAt)
            };

            bool isStaff = User.IsInRole("MODERATOR") || User.IsInRole("ADMIN");

            if (!isStaff || !showInactive)
            {
                var now = DateTime.UtcNow;
                query = query.Where(o =>
                    o.IsVisible &&
                    (o.User.LockoutEnd == null || o.User.LockoutEnd <= DateTimeOffset.UtcNow) &&
                    (o.ExpirationDate == null || o.ExpirationDate > now)
                );
            }

            return View(await query.ToListAsync());
        }

        public async Task<IActionResult> ClosedListings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var closedOffers = await _context.JobOffers
                .Where(o => o.RecruiterId == userId && (!o.IsVisible || (o.ExpirationDate.HasValue && o.ExpirationDate < DateTime.Now)))
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(closedOffers);
        }

        [Authorize]
        public async Task<IActionResult> CvView(int id)
        {
            var userId = _userManager.GetUserId(User);

            var application = await _context.Applications
                .Include(a => a.JobOffer)
                .Include(a => a.Cv)
                .FirstOrDefaultAsync(a => a.CvId == id && a.JobOffer.RecruiterId == userId);

            if (application == null)
            {
                var ownCv = await _context.Cvs.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
                if (ownCv == null) return NotFound();

                ViewBag.OfferTitle = "Your Profile";
                return View(ownCv);
            }

            ViewBag.OfferTitle = application.JobOffer.Title;
            return View(application.Cv);
        }

        [Authorize]
        public async Task<IActionResult> AiAnalysisDetails(int id)
        {
            var userId = _userManager.GetUserId(User);

            var offer = await _context.JobOffers
                .Include(o => o.Applications)
                    .ThenInclude(a => a.Cv)
                .FirstOrDefaultAsync(o => o.Id == id && (o.RecruiterId == userId || User.IsInRole("ADMIN")));

            if (offer == null)
                return NotFound();

            var applicationIds = offer.Applications.Select(a => a.Id).ToList();

            var aiReports = await _context.AiApplicationReports
                .Where(r => applicationIds.Contains(r.JobApplicationId))
                .ToListAsync();

            ViewBag.AiReports = aiReports;

            return View(offer);
        }

        [Authorize]
        public async Task<IActionResult> ViewResults(int id)
        {
            var userId = _userManager.GetUserId(User);

            var applications = await _context.Applications
                .Include(a => a.JobOffer)
                .Include(a => a.Cv)
                .Where(a => a.JobOfferId == id && a.JobOffer.RecruiterId == userId)
                .OrderByDescending(a => a.AppliedAt)
                .ToListAsync();

            var offer = await _context.JobOffers.FirstOrDefaultAsync(o => o.Id == id && o.RecruiterId == userId);
            if (offer == null) return NotFound();

            ViewBag.OfferTitle = offer.Title;
            ViewBag.AiStatus = offer.AiAnalysisStatus;

            var applicationIds = applications.Select(a => a.Id).ToList();
            var aiReports = await _context.AiApplicationReports
                .Where(r => applicationIds.Contains(r.JobApplicationId))
                .ToListAsync();

            ViewBag.AiReports = aiReports;

            return View(applications);
        }

        public async Task<IActionResult> Details(int id)
        {
            var jobOffer = await _context.JobOffers
                .Include(j => j.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (jobOffer == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (userId != null)
            {
                ViewBag.UserApplication = await _context.Applications
                    .FirstOrDefaultAsync(a => a.JobOfferId == id && a.CandidateId == userId);
            }

            return View(jobOffer);
        }

        public async Task<IActionResult> HideOffer(int id)
        {
            var offer = await _context.JobOffers.FindAsync(id);
            if (offer == null) return NotFound();

            offer.IsVisible = false;
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                offer.RecruiterId,
                "Offer Hidden",
                $"Your offer '{offer.Title}' has been hidden by a moderator due to policy violation.",
                Url.Action("Details", "Offer", new { id = offer.Id })
            );

            TempData["StatusMessage"] = "Offer has been hidden.";
            return RedirectToAction(nameof(List), new { category = offer.Category });
        }

        [Authorize(Roles = "MODERATOR, ADMIN")]
        [HttpPost]
        public async Task<IActionResult> ReportUser(int id)
        {
            var offer = await _context.JobOffers.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == id);
            if (offer == null) return NotFound();

            offer.IsVisible = false;
            await _context.SaveChangesAsync();

            await _notificationService.SendAsync(
                offer.RecruiterId,
                "Account under review",
                "A moderator has reported your profile. Your offers are temporarily hidden.",
                null
            );

            var admins = await _userManager.GetUsersInRoleAsync("ADMIN");
            foreach (var admin in admins)
            {
                await _notificationService.SendAsync(
                    admin.Id,
                    "URGENT: Serious Rules Violation",
                    $"User {offer.User?.UserName ?? offer.RecruiterId} (Offer ID: {offer.Id}) reported. Action required.",
                    Url.Action("Details", "Offer", new { id = offer.Id })
                );
            }

            TempData["StatusMessage"] = "User reported to Admin and offer hidden.";
            return RedirectToAction(nameof(List), new { category = offer.Category });
        }

        [Authorize]
        public async Task<IActionResult> MyListings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            ViewBag.IsVerifiedEmployer = user.IsEmployer;

            var myOffers = await _context.JobOffers
                .Where(o => o.RecruiterId == user.Id)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
            return View(myOffers);
        }

        [Authorize(Roles = "MODERATOR, ADMIN")]
        [HttpPost]
        public async Task<IActionResult> ShowOffer(int id)
        {
            var offer = await _context.JobOffers.FindAsync(id);
            if (offer == null) return NotFound();

            offer.IsVisible = true;
            await _context.SaveChangesAsync();

            TempData["StatusMessage"] = "Offer is now visible to everyone.";
            return Redirect(Request.Headers["Referer"].ToString() ?? Url.Action("List", new { category = offer.Category }));
        }

        [Authorize]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || !user.IsEmployer)
            {
                TempData["ErrorMessage"] = "Your account is pending moderator verification.";
                return RedirectToAction(nameof(MyListings));
            }

            await PrepareViewBags();

            var model = new JobOffer { Company = user.CompanyName };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(JobOffer offer, string duration, DateTime? customDate, string SalaryType)
        {
            var user = await _userManager.GetUserAsync(User);

            offer.RecruiterId = user.Id;
            offer.CreatedAt = DateTime.UtcNow;
            offer.Company = user.CompanyName;
            offer.IsVisible = true;

            offer.SalaryType = SalaryType;
            if (SalaryType == "none" || SalaryType == "negotiable")
            {
                offer.MinimumSalary = null;
                offer.MaximumSalary = null;
            }
            else if (SalaryType == "fixed")
            {
                offer.MaximumSalary = null;
            }

            offer.ExpirationDate = duration switch
            {
                "1w" => DateTime.UtcNow.AddDays(7),
                "1m" => DateTime.UtcNow.AddMonths(1),
                "3m" => DateTime.UtcNow.AddMonths(3),
                "1y" => DateTime.UtcNow.AddYears(1),
                "custom" => customDate,
                _ => null
            };

            ModelState.Remove("RecruiterId");
            ModelState.Remove("User");
            ModelState.Remove("Applications");
            ModelState.Remove("Company");

            if (ModelState.IsValid)
            {
                _context.Add(offer);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Offer created successfully!";
                return RedirectToAction(nameof(MyListings));
            }

            await PrepareViewBags();
            return View(offer);
        }

        [Authorize]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var offer = await _context.JobOffers.FindAsync(id);
            if (offer == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (offer.RecruiterId != userId) return Forbid();

            await PrepareViewBags();
            return View(offer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, JobOffer offer)
        {
            if (id != offer.Id) return NotFound();

            var existingOffer = await _context.JobOffers.FindAsync(id);
            if (existingOffer == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (existingOffer.RecruiterId != userId) return Forbid();

            ModelState.Remove("User");
            ModelState.Remove("RecruiterId");
            ModelState.Remove("Applications");

            if (ModelState.IsValid)
            {
                try
                {
                    existingOffer.Title = offer.Title;
                    existingOffer.Category = offer.Category;
                    existingOffer.Subcategory = offer.Subcategory;
                    existingOffer.JobType = offer.JobType;
                    existingOffer.Location = offer.Location;
                    existingOffer.Description = offer.Description;
                    existingOffer.Requirements = offer.Requirements;
                    existingOffer.RequiredLanguages = offer.RequiredLanguages;
                    existingOffer.ExpirationDate = offer.ExpirationDate;
                    existingOffer.SalaryType = offer.SalaryType;
                    existingOffer.MinimumSalary = offer.MinimumSalary;
                    existingOffer.MaximumSalary = offer.MaximumSalary;

                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Offer updated successfully!";
                    return RedirectToAction(nameof(MyListings));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.JobOffers.Any(e => e.Id == offer.Id)) return NotFound();
                    else throw;
                }
            }

            await PrepareViewBags();
            return View(offer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> CloseOffer(int id)
        {
            var offer = await _context.JobOffers.FindAsync(id);
            if (offer == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (offer.RecruiterId != userId) return Forbid();

            offer.ExpirationDate = DateTime.UtcNow;

            _context.Update(offer);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Recruitment has been closed.";
            return RedirectToAction(nameof(MyListings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReopenOffer(int id)
        {
            var offer = await _context.JobOffers.FindAsync(id);
            if (offer == null) return NotFound();

            offer.IsVisible = true;
            offer.ExpirationDate = DateTime.Now.AddDays(30);

            offer.AiAnalysisStatus = null;
            offer.AiAnalysisComment = null;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyListings));
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> SelectForApplication(int jobId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var offer = await _context.JobOffers.FindAsync(jobId);

            if (offer == null) return NotFound();

            if (offer.RecruiterId == userId)
            {
                TempData["ErrorMessage"] = "You cannot apply to your own job offer.";
                return RedirectToAction("Details", new { id = jobId });
            }

            var alreadyApplied = await _context.Applications
                .AnyAsync(a => a.JobOfferId == jobId && a.CandidateId == userId);

            if (alreadyApplied)
            {
                TempData["ErrorMessage"] = "You have already submitted an application for this position.";
                return RedirectToAction("Details", new { id = jobId });
            }

            var userResumes = await _context.Cvs
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            if (!userResumes.Any())
            {
                TempData["ErrorMessage"] = "You need to create at least one CV before applying.";
                return RedirectToAction("Create", "Cv");
            }

            var viewModel = new ApplyViewModel
            {
                JobOfferId = jobId,
                JobOffer = offer,
                UserResumes = userResumes
            };

            return View(viewModel);
        }

        [HttpPost]
        [AllowAnonymous]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateAiResult([FromBody] AiResultModel data)
        {
            Console.WriteLine($"--- CALLBACK OTRZYMANY: OfferId={data.offerId}, Status={data.status} ---");
            try
            {
                var offer = await _context.JobOffers
                    .Include(o => o.Applications)
                    .FirstOrDefaultAsync(o => o.Id == data.offerId);

                if (offer == null) return NotFound();

                offer.AiAnalysisStatus = data.status;
                offer.AiAnalysisComment = $"Analysis completed. Processed {data.results?.Count ?? 0} candidates.";

                var applicationIds = offer.Applications.Select(a => a.Id).ToList();
                var oldReports = await _context.AiApplicationReports
                    .Where(r => applicationIds.Contains(r.JobApplicationId))
                    .ToListAsync();

                if (oldReports.Any())
                {
                    _context.AiApplicationReports.RemoveRange(oldReports);
                }

                if (data.results != null)
                {
                    foreach (var result in data.results)
                    {
                        if (applicationIds.Contains(result.applicationId))
                        {
                            var report = new AiApplicationReport
                            {
                                JobApplicationId = result.applicationId,
                                Score = result.score,
                                Description = result.description,
                                Pros = result.pros != null ? string.Join("\n", result.pros) : string.Empty,
                                Cons = result.cons != null ? string.Join("\n", result.cons) : string.Empty,
                                AnalyzedAt = DateTime.UtcNow
                            };
                            _context.AiApplicationReports.Add(report);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return Ok(new { message = "Data updated successfully and relationally saved" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd aktualizacji relacyjnej: {ex.Message}");
                return StatusCode(500);
            }
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitApplication(ApplyViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var offer = await _context.JobOffers.FindAsync(model.JobOfferId);

            if (offer == null) return NotFound();
            if (offer.RecruiterId == userId) return Forbid();

            var alreadyApplied = await _context.Applications
                .AnyAsync(a => a.JobOfferId == model.JobOfferId && a.CandidateId == userId);

            if (alreadyApplied)
            {
                TempData["ErrorMessage"] = "Application already sent.";
                return RedirectToAction("Details", new { id = model.JobOfferId });
            }

            var application = new JobApplication
            {
                JobOfferId = model.JobOfferId,
                CvId = model.SelectedCvId,
                CandidateId = userId,
                AppliedAt = DateTime.UtcNow,
                Message = model.Message,
                Status = "Pending"
            };

            _context.Applications.Add(application);

            await _notificationService.SendAsync(
                offer.RecruiterId,
                "New Application",
                $"Someone applied for your offer: '{offer.Title}'.",
                Url.Action("Details", "Offer", new { id = offer.Id })
            );

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Application sent successfully!";
            return RedirectToAction("Details", new { id = model.JobOfferId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WithdrawApplication(int jobId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var application = await _context.Applications
                .Include(a => a.JobOffer)
                .FirstOrDefaultAsync(a => a.JobOfferId == jobId && a.CandidateId == userId);

            if (application == null) return NotFound();

            if (application.JobOffer.ExpirationDate.HasValue &&
                application.JobOffer.ExpirationDate.Value <= DateTime.UtcNow)
            {
                TempData["ErrorMessage"] = "You cannot withdraw your application because the job offer is already closed.";
                return RedirectToAction("Details", new { id = jobId });
            }

            if (application.Status != "Pending")
            {
                TempData["ErrorMessage"] = "You cannot withdraw an application that has already been processed by the recruiter.";
                return RedirectToAction("Details", new { id = jobId });
            }

            _context.Applications.Remove(application);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Your application has been withdrawn.";
            return RedirectToAction("Details", new { id = jobId });
        }

        private async Task PrepareViewBags()
        {
            ViewBag.Categories = new List<string>
            {
                "IT", "Data Science", "Marketing", "Finance",
                "Healthcare", "Engineering", "Sales", "Customer Service",
                "Human Resources", "Design & Creative", "Logistics",
                "Legal", "Education", "Construction", "Hospitality", "Other"
            };

            var existingSubcats = await _context.JobOffers
                .Where(o => !string.IsNullOrEmpty(o.Subcategory))
                .Select(o => new { o.Category, o.Subcategory })
                .Distinct()
                .ToListAsync();

            ViewBag.SubcategoryMap = existingSubcats
                .GroupBy(x => x.Category)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Subcategory).ToList());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOffer(int id)
        {
            var offer = await _context.JobOffers
                .Include(o => o.Applications)
                .ThenInclude(a => a.Cv)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null) return NotFound();

            offer.AiAnalysisStatus = "Pending";
            offer.AiAnalysisComment = "Analysis in progress...";
            _context.Update(offer);
            await _context.SaveChangesAsync();

            bool sentToAi = await SendOfferToPythonAi(offer);
            if (!sentToAi)
            {
                offer.AiAnalysisStatus = "Failed";
                offer.AiAnalysisComment = "Could not reach the AI service or credentials invalid.";
                _context.Update(offer);
                await _context.SaveChangesAsync();

                TempData["ErrorMessage"] = "Failed to communicate with AI service.";
            }
            else
            {
                TempData["SuccessMessage"] = "AI Analysis has been queued successfully.";
            }

            return RedirectToAction(nameof(ViewResults), new { id = offer.Id });
        }

        private async Task<bool> SendOfferToPythonAi(JobOffer offer)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();

                var pythonServiceUrl = _configuration["AiService:Url"]
                    ?? throw new InvalidOperationException("Missing confu=iguration 'AiService:Url' in system.");

                var apiKey = _configuration["AiService:ApiKey"]
                    ?? throw new InvalidOperationException("Missing confu=iguration 'AiService:ApiKey' in system.");

                var payload = new
                {
                    offerId = offer.Id,
                    title = offer.Title,
                    description = offer.Description,
                    requirements = offer.Requirements,
                    requiredLanguages = offer.RequiredLanguages ?? "Not specified",
                    applications = offer.Applications.Select(app => new
                    {
                        applicationId = app.Id,
                        cv = app.Cv != null ? new
                        {
                            name = app.Cv.Name,
                            surname = app.Cv.Surname,
                            experience = app.Cv.ProfessionalExperience,
                            education = app.Cv.Education,
                            skills = app.Cv.Skills,
                            languages = app.Cv.Languages
                        } : null
                    }).ToList()
                };

                var jsonPayload = JsonSerializer.Serialize(payload);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("X-AI-Key", apiKey);

                var response = await client.PostAsync(pythonServiceUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                Console.WriteLine($"Python AI Service returned error status: {response.StatusCode}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error during communication with Python AI: {ex.Message}");
                return false;
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var offer = await _context.JobOffers
                .Include(o => o.Applications)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (offer == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (offer.RecruiterId != userId) return Forbid();

            if (offer.Applications != null && offer.Applications.Any())
            {
                var applicationIds = offer.Applications.Select(a => a.Id).ToList();

                var linkedAiReports = await _context.AiApplicationReports
                    .Where(r => applicationIds.Contains(r.JobApplicationId))
                    .ToListAsync();

                if (linkedAiReports.Any())
                {
                    _context.AiApplicationReports.RemoveRange(linkedAiReports);
                }

                _context.Applications.RemoveRange(offer.Applications);
            }

            _context.JobOffers.Remove(offer);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Offer and all related applications deleted successfully.";
            return RedirectToAction(nameof(MyListings));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAiStatus(int id)
        {
            var offer = await _context.JobOffers.FindAsync(id);
            if (offer == null) return NotFound();

            return Json(new { status = offer.AiAnalysisStatus });
        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ExportAiResultsToCsv(int id)
        {
            var userId = _userManager.GetUserId(User);

            var dataToExport = await _context.Applications
                .Include(a => a.Cv)
                .Include(a => a.JobOffer)
                .Where(a => a.JobOfferId == id && a.JobOffer.RecruiterId == userId)
                .Select(a => new
                {
                    Candidate = a.Cv != null ? $"{a.Cv.Name} {a.Cv.Surname}" : "Hidden Name",
                    Email = a.Cv != null ? a.Cv.Email : "N/A",
                    Score = _context.AiApplicationReports.Where(r => r.JobApplicationId == a.Id).Select(r => r.Score).FirstOrDefault(),
                    Description = _context.AiApplicationReports.Where(r => r.JobApplicationId == a.Id).Select(r => r.Description).FirstOrDefault()
                })
                .OrderByDescending(x => x.Score)
                .ToListAsync();

            if (!dataToExport.Any()) return NotFound("No data to export.");

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine("Candidate;Email;AI Match (%);Profile Summary");

            foreach (var item in dataToExport)
            {
                string cleanDesc = item.Description?.Replace(";", ",").Replace("\r", "").Replace("\n", " ") ?? "";
                csvBuilder.AppendLine($"{item.Candidate};{item.Email};{item.Score};{cleanDesc}");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(csvBuilder.ToString());
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            byte[] finalFile = new byte[bom.Length + buffer.Length];

            Buffer.BlockCopy(bom, 0, finalFile, 0, bom.Length);
            Buffer.BlockCopy(buffer, 0, finalFile, bom.Length, buffer.Length);

            return File(finalFile, "text/csv", $"AI_Report_Offer_{id}.csv");
        }
    }

    public class AiResultModel
    {
        public int offerId { get; set; }
        public string status { get; set; }
        public List<AiCandidateAnalysisResult> results { get; set; }
    }

    public class AiCandidateAnalysisResult
    {
        public int applicationId { get; set; }
        public int score { get; set; }
        public string description { get; set; }
        public List<string> pros { get; set; }
        public List<string> cons { get; set; }
    }
}