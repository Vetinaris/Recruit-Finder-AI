using Microsoft.AspNetCore.Identity;

namespace Recruit_Finder_AI.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string? CompanyName { get; set; }
        public string? NIP { get; set; }
        public string? CompanyAddress { get; set; }

        public bool IsEmployer { get; set; }
    }
}