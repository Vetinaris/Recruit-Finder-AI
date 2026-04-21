using Recruit_Finder_AI.Models;
namespace Recruit_Finder_AI.Entities
{
    public class Notification
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
        public bool IsCompleted { get; set; } = false;
        public string? ActionUrl { get; set; }
    }
}