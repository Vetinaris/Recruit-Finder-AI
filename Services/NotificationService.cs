using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Entities;

public class NotificationService
{
    private readonly Recruit_Finder_AIContext _context;

    public NotificationService(Recruit_Finder_AIContext context)
    {
        _context = context;
    }

    public async Task SendAsync(string userId, string title, string content, string? url = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            ActionUrl = url,
            CreatedAt = DateTime.Now
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }
}