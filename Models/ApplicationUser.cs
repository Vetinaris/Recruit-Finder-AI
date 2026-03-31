using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recruit_Finder_AI.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? CompanyName { get; set; }
        public string? NIP { get; set; }
        public string? CompanyAddress { get; set; }

        public bool IsEmployer { get; set; }
        public DateTime? PasswordExpiration { get; set; } = DateTime.UtcNow.AddDays(30);
        public virtual ICollection<PasswordHistory> PasswordHistories { get; set; } = new List<PasswordHistory>();
        public int ResetPasswordAttemptCount { get; set; } = 0;
        public DateTime? LastResetAttempt { get; set; }
        public bool IsPermanentBan { get; set; } = false;
        [MaxLength(500)]
        public string? BanReason { get; set; }
    }
    public class PasswordHistory
    {
        public int Id { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string ApplicationUserId { get; set; }

        [ForeignKey(nameof(ApplicationUserId))]
        public virtual ApplicationUser ApplicationUser { get; set; }
    }
}