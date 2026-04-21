using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recruit_Finder_AI.Models
{
    public class JobOffer
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Job Title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Company { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        [Required]
        public string Subcategory { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Job Type")]
        public string JobType { get; set; } = string.Empty;

        public string Location { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        public string SalaryType { get; set; } = "Undisclosed";
        public decimal? MinimumSalary { get; set; }
        public decimal? MaximumSalary { get; set; }

        public string? RequiredLanguages { get; set; }

        public string? RecruiterId { get; set; }

        [ForeignKey("RecruiterId")]
        public virtual ApplicationUser? User { get; set; }

        public bool IsVisible { get; set; } = true;

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpirationDate { get; set; }
    }
}