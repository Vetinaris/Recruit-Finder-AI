using Recruit_Finder_AI.Models;
using System.ComponentModel.DataAnnotations;
namespace Recruit_Finder_AI.ViewModels
{
    public class EditUserViewModel
    {
        [Required]
        public string Id { get; set; }
        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; }
        [Required]
        [EmailAddress]
        [Display(Name = "Email Address")]
        public string Email { get; set; }
        [Display(Name = "Account Locked")]
        public bool IsLocked { get; set; }
        public DateTimeOffset? LockoutEndDisplay { get; set; }
        [Display(Name = "Force Password Reset")]
        public bool ForcePasswordReset { get; set; } = false;
        [DataType(DataType.DateTime)]
        [Display(Name = "Password Expires")]
        public DateTime? PasswordExpiration { get; set; }
        public List<UserRoleViewModel> AvailableRoles { get; set; }
        public string? CompanyName { get; set; }
        public string? NIP { get; set; }
        public string? CompanyAddress { get; set; }
        public bool IsEmployer { get; set; }
    }
}
