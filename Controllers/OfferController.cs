using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Services;
using System.Security.AccessControl;
using System.Security.Claims;

namespace Recruit_Finder_AI.Controllers
{
    public class OfferController : Controller
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly NotificationService _notificationService;

        public OfferController(
            Recruit_Finder_AIContext context,
            UserManager<ApplicationUser> userManager,
            NotificationService notificationService)
        {
            _context = context;
            _userManager = userManager;
            _notificationService = notificationService;
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

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var offer = await _context.JobOffers
                .Include(o => o.User)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (offer == null) return NotFound();

            bool isStaff = User.IsInRole("MODERATOR") || User.IsInRole("ADMIN");
            bool isAuthorBanned = offer.User?.LockoutEnd > DateTimeOffset.UtcNow;

            if (!offer.IsVisible || isAuthorBanned)
            {
                if (!isStaff)
                {
                    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    if (offer.RecruiterId != currentUserId)
                    {
                        return NotFound();
                    }
                }
            }

            return View(offer);
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
                TempData["ErrorMessage"] = "Your account is pending moderator verification. You cannot post offers yet.";
                return RedirectToAction(nameof(MyListings));
            }

            ViewBag.Categories = new List<string>
            {
                "IT", "Data Science", "Marketing", "Finance",
                "Healthcare", "Engineering", "Sales", "Customer Service",
                "Human Resources", "Design & Creative", "Logistics",
                "Legal", "Education", "Construction", "Hospitality"
            }; ;
            ViewBag.JobTypes = new List<string> { "Full-time", "Part-time", "Contract", "Freelance", "B2B", "Internship" };

            var existingSubcats = await _context.JobOffers
                .Select(o => new { o.Category, o.Subcategory })
                .Where(o => !string.IsNullOrEmpty(o.Subcategory))
                .Distinct()
                .ToListAsync();

            var subcatMap = existingSubcats
                .GroupBy(x => x.Category)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Subcategory).ToList());

            ViewBag.SubcategoryMap = subcatMap;

            var model = new JobOffer { Company = user.CompanyName };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(JobOffer offer, string duration, DateTime? customDate)
        {
            offer.RecruiterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            offer.CreatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(offer.RequiredLanguages))
            {
                var langList = offer.RequiredLanguages
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => char.ToUpper(l.Trim()[0]) + l.Trim().Substring(1).ToLower())
                    .Distinct()
                    .ToList();

                offer.RequiredLanguages = string.Join(", ", langList);
            }
            if (!string.IsNullOrWhiteSpace(offer.Subcategory))
            {
                offer.Subcategory = char.ToUpper(offer.Subcategory.Trim()[0]) + offer.Subcategory.Trim().Substring(1).ToLower();
            }
            offer.ExpirationDate = duration switch
            {
                "1w" => DateTime.UtcNow.AddDays(7),
                "1m" => DateTime.UtcNow.AddMonths(1),
                "3m" => DateTime.UtcNow.AddMonths(3),
                "1y" => DateTime.UtcNow.AddYears(1),
                "custom" => customDate,
                "manual" => null,
                _ => null
            };

            ModelState.Remove("RecruiterId");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                _context.Add(offer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(MyListings));
            }
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

            ViewBag.Categories = new List<string>
            {
                "IT", "Data Science", "Marketing", "Finance",
                "Healthcare", "Engineering", "Sales", "Customer Service",
                "Human Resources", "Design & Creative", "Logistics",
                "Legal", "Education", "Construction", "Hospitality"
            };

            var existingSubcats = await _context.JobOffers
                .Select(o => new { o.Category, o.Subcategory })
                .Where(o => !string.IsNullOrEmpty(o.Subcategory))
                .Distinct()
                .ToListAsync();

            ViewBag.SubcategoryMap = existingSubcats
                .GroupBy(x => x.Category)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Subcategory).ToList());

            return View(offer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(int id, JobOffer offer)
        {
            if (id != offer.Id) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (offer.RecruiterId != userId) return Forbid();

            if (!string.IsNullOrWhiteSpace(offer.RequiredLanguages))
            {
                var langList = offer.RequiredLanguages
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => char.ToUpper(l.Trim()[0]) + l.Trim().Substring(1).ToLower())
                    .Distinct()
                    .ToList();

                offer.RequiredLanguages = string.Join(", ", langList);
            }

            ModelState.Remove("RecruiterId");
            ModelState.Remove("User");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(offer);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.JobOffers.Any(e => e.Id == offer.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(MyListings));
            }
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
        [Authorize]
        public async Task<IActionResult> ReopenOffer(int id)
        {
            var offer = await _context.JobOffers.FindAsync(id);
            if (offer == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (offer.RecruiterId != userId) return Forbid();

            offer.ExpirationDate = null;
            offer.IsVisible = true;

            _context.Update(offer);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Offer has been reopened and is now live.";
            return RedirectToAction(nameof(MyListings));
        }
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var offer = await _context.JobOffers.FindAsync(id);
            if (offer == null) return NotFound();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (offer.RecruiterId != userId) return Forbid();

            _context.JobOffers.Remove(offer);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Offer deleted successfully.";
            return RedirectToAction(nameof(MyListings));
        }
    }
}