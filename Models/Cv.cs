using System;
using System.ComponentModel.DataAnnotations;
using Recruit_Finder_AI.Entities;

namespace Recruit_Finder_AI.Models
{
    public class Cv
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string Surname { get; set; } = string.Empty;

        public string? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string ProfessionalExperience { get; set; } = string.Empty;

        [Required]
        public string Education { get; set; } = string.Empty;

        public string? Portfolio { get; set; }
        public string? Languages { get; set; }
        public string? Skills { get; set; }
        public string? Interests { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public bool IsVerified { get; set; } = false;
        public string? AiFeedback { get; set; }
    }
}