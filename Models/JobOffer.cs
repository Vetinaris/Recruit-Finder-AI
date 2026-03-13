using System.ComponentModel.DataAnnotations;

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

        public string Location { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string? RecruiterId { get; set; }
    }
}