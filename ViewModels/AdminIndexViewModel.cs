using Recruit_Finder_AI.Data;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Generic;
namespace Recruit_Finder_AI.ViewModels
{
    public class UserIndexRowViewModel
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public DateTime? PasswordExpiration { get; set; }
        public string PrimaryRole { get; set; }
    }
    public class AdminIndexViewModel
    {
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public List<UserIndexRowViewModel> Users { get; set; } = new List<UserIndexRowViewModel>();
    }
}
