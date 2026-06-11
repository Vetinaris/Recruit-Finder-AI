using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Models;
using Recruit_Finder_AI.Entities;
using System.Linq;
using System.Threading.Tasks;

[Authorize]
public class NotificationsController : Controller
{
    private readonly Recruit_Finder_AIContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(Recruit_Finder_AIContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);

        var notes = await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync<Notification>();

        var activeOfferIds = await _context.JobOffers.Select(o => o.Id).ToListAsync();

        foreach (var note in notes)
        {
            if (!string.IsNullOrEmpty(note.ActionUrl))
            {
                var segments = note.ActionUrl.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 3 && (segments[0].Equals("Offer", StringComparison.OrdinalIgnoreCase)))
                {
                    if (int.TryParse(segments[2], out int offerId))
                    {
                        if (!activeOfferIds.Contains(offerId))
                        {
                            note.ActionUrl = null;
                            note.Content += " (This offer is no longer available / has been deleted)";
                        }
                    }
                }
            }
        }

        return View(notes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsCompleted(int id)
    {
        var currentUserId = _userManager.GetUserId(User);
        var note = await _context.Notifications.FindAsync(id);

        if (note != null && note.UserId == currentUserId)
        {
            note.IsRead = true;
            note.IsCompleted = true;
            await _context.SaveChangesAsync();
            return Ok();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsCompleted()
    {
        var currentUserId = _userManager.GetUserId(User);

        var uncompletedNotifications = await _context.Notifications
            .Where(n => n.UserId == currentUserId && !n.IsCompleted)
            .ToListAsync();

        if (uncompletedNotifications.Any())
        {
            foreach (var note in uncompletedNotifications)
            {
                note.IsRead = true;
                note.IsCompleted = true;
            }
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }
}