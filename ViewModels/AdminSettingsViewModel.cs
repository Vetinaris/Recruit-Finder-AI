using System.ComponentModel.DataAnnotations;

namespace Recruit_Finder_AI.ViewModels
{
    public class AdminSettingsViewModel
    {
        [Display(Name = "Password Expiration (Days)")]
        [Required]
        [Range(1, 365, ErrorMessage = "The value must be between {1} and {2} days.")]
        public int PasswordExpirationDays { get; set; }

        [Display(Name = "Password History Depth")]
        [Required]
        [Range(1, 10, ErrorMessage = "The value must be between {1} and {2}.")]
        public int PasswordHistoryDepth { get; set; }

        [Display(Name = "Minimum Password Length")]
        [Required]
        [Range(6, 50, ErrorMessage = "The password length must be between {1} and {2} characters.")]
        public int MinPasswordLength { get; set; }

        [Display(Name = "Allow New User Registration")]
        public bool EnableRegistration { get; set; }
    }
}