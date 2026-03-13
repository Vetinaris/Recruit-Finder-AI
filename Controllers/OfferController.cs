using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using System.Security.Claims;

namespace Recruit_Finder_AI.Controllers
{
    public class OfferController : Controller
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        public OfferController(Recruit_Finder_AIContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> List(string category)
        {
            ViewBag.CategoryName = category;
            var offers = await _context.JobOffers
                .Where(o => o.Category == category)
                .ToListAsync();
            return View(offers);
        }
        [Authorize]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null || string.IsNullOrEmpty(user.CompanyName))
            {
                TempData["ErrorMessage"] = "You must complete your Company Profile before posting a job.";
                return RedirectToPage("/Account/Manage/Index", new { area = "Identity" });
            }

            var model = new JobOffer { Company = user.CompanyName };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Create(JobOffer offer)
        {
            offer.RecruiterId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            ModelState.Remove("RecruiterId");

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

            ModelState.Remove("RecruiterId");

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

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var offer = await _context.JobOffers.FirstOrDefaultAsync(m => m.Id == id);
            if (offer == null) return NotFound();

            return View(offer);
        }

        [Authorize]
        public async Task<IActionResult> MyListings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var myOffers = await _context.JobOffers
                .Where(o => o.RecruiterId == userId)
                .ToListAsync();
            return View(myOffers);
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

            return RedirectToAction(nameof(MyListings));
        }
    }
}