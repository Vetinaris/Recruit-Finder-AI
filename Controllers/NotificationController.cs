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
        }

        return RedirectToAction(nameof(Index));
    }
}