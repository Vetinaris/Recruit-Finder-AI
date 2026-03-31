using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Recruit_Finder_AI.Models
{
    public class ApiLoginModel
    {
        [Required(ErrorMessage = "Email or Username field is required.")]

        [JsonPropertyName("emailOrUsername")]
        public string EmailOrUsername { get; set; }
        [Required]
        [JsonPropertyName("password")]
        public string Password { get; set; }
        [JsonPropertyName("rememberMe")]
        public bool RememberMe { get; set; }
        [JsonPropertyName("reCaptchaToken")]
        public string ReCaptchaToken { get; set; }
    }
}
