using Recruit_Finder_AI.Models;
using System.ComponentModel.DataAnnotations;
namespace Recruit_Finder_AI.DTO
{
    public class ApiRegisterDto
    {

        [Required]
        public InputData Input { get; set; }

        public class InputData
        {
            [Required]
            [DataType(DataType.Text)]
            public string Username { get; set; }

            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            public string ReCaptchaToken { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Hasła nie pasują.")]
            public string ConfirmPassword { get; set; }

            [Required(ErrorMessage = "Musisz zaakceptować warunki.")]
            public bool AcceptTerms { get; set; }
        }
    }
}
