using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Recruit_Finder_AI.Models;

namespace Recruit_Finder_AI.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Display(Name = "Phone Number")]
            [RegularExpression(@"^[0-9+\s\-]*$", ErrorMessage = "Phone number can only contain digits, spaces, '-' and '+'")]
            public string PhoneNumber { get; set; }

            public bool ShowCompanySection { get; set; }
            public bool IsEmployer { get; set; }

            public bool HasSubmittedCompanyData { get; set; }

            [RegularExpression(@"^[a-zA-Z0-9ąęćłńóśźżĄĘĆŁŃÓŚŹŻ\s.,\-_]*$", ErrorMessage = "Emojis are not allowed")]
            public string? CompanyName { get; set; }

            [RegularExpression(@"^[a-zA-Z0-9]*$", ErrorMessage = "Emojis are not allowed")]
            public string? NIP { get; set; }

            [RegularExpression(@"^[a-zA-Z0-9ąęćłńóśźżĄĘĆŁŃÓŚŹŻ\s.,\-\/]*$", ErrorMessage = "Emojis are not allowed")]
            public string? CompanyAddress { get; set; }

            public byte[]? ProfilePicture { get; set; }
            public string? ContentType { get; set; }
            public IFormFile? ImageFile { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            bool isVerificationActive = Input.ShowCompanySection || user.IsEmployer;

            if (isVerificationActive && !user.IsEmployer)
            {
                bool hasError = false;
                if (string.IsNullOrWhiteSpace(Input.CompanyName)) { ModelState.AddModelError("Input.CompanyName", "Company Name is required."); hasError = true; }
                if (string.IsNullOrWhiteSpace(Input.NIP)) { ModelState.AddModelError("Input.NIP", "NIP is required."); hasError = true; }
                if (string.IsNullOrWhiteSpace(Input.CompanyAddress)) { ModelState.AddModelError("Input.CompanyAddress", "Address is required."); hasError = true; }

                if (hasError)
                {
                    Username = user.UserName;
                    return Page();
                }
            }

            var currentPhone = await _userManager.GetPhoneNumberAsync(user);
            if (Input.PhoneNumber != currentPhone)
            {
                await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            }

            if (Input.ImageFile != null)
            {
                using var dataStream = new MemoryStream();
                await Input.ImageFile.CopyToAsync(dataStream);
                user.ProfilePicture = dataStream.ToArray();
                user.ProfilePictureContentType = Input.ImageFile.ContentType;
            }

            if (!user.IsEmployer && Input.ShowCompanySection)
            {
                user.CompanyName = Input.CompanyName;
                user.NIP = Input.NIP;
                user.CompanyAddress = Input.CompanyAddress;
            }

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Błąd podczas zapisu do bazy.");
                Username = user.UserName;
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Profile updated successfully.";
            return RedirectToPage();
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

            Username = userName;

            Input = new InputModel
            {
                PhoneNumber = phoneNumber,
                IsEmployer = user.IsEmployer,
                CompanyName = user.CompanyName,
                NIP = user.NIP,
                CompanyAddress = user.CompanyAddress,
                ProfilePicture = user.ProfilePicture,
                ContentType = user.ProfilePictureContentType,
                HasSubmittedCompanyData = !string.IsNullOrEmpty(user.NIP) && !user.IsEmployer,
                ShowCompanySection = !string.IsNullOrEmpty(user.NIP) || user.IsEmployer
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await LoadAsync(user);
            return Page();
        }
    }
}