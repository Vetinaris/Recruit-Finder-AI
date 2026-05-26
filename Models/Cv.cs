using Recruit_Finder_AI.Entities;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Recruit_Finder_AI.Models
{
    public class Cv
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "First Name is required")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last Name is required")]
        public string Surname { get; set; } = string.Empty;

        [Required(ErrorMessage = "Date of birth is required")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Required(ErrorMessage = "Address is required")]
        public string? Address { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Phone]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Work experience is required")]
        public string ProfessionalExperience { get; set; } = string.Empty;

        [Required(ErrorMessage = "Education is required")]
        public string Education { get; set; } = string.Empty;

        public string? Portfolio { get; set; }

        [Required(ErrorMessage = "Please add at least one language")]
        public string? Languages { get; set; }

        [Required(ErrorMessage = "Skills are required")]
        public string? Skills { get; set; }
        public string? Interests { get; set; }

        public bool IncludePhoto { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }

        public bool IsVerified { get; set; } = false;
        public string? AiFeedback { get; set; }

        [NotMapped]
        public bool HasPhoto => IncludePhoto && User?.ProfilePicture != null;

        [NotMapped]
        public string? PhotoUrl
        {
            get
            {
                if (User?.ProfilePicture == null) return null;
                var base64 = Convert.ToBase64String(User.ProfilePicture);
                return $"data:image/png;base64,{base64}";
            }
        }
    }
}