using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Recruit_Finder_AI.Data;
using Microsoft.EntityFrameworkCore;
using Recruit_Finder_AI.Models;

namespace Recruit_Finder_AI.ViewComponents
{
    public class NotificationBadgeViewComponent : ViewComponent
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationBadgeViewComponent(Recruit_Finder_AIContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = _userManager.GetUserId(HttpContext.User);

            if (string.IsNullOrEmpty(userId))
            {
                return View(0);
            }
            var count = await _context.Notifications
                .Where(n => n.UserId == userId && !n.IsCompleted)
                .CountAsync();

            return View(count);
        }
    }
}