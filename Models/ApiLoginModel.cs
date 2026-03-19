using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
namespace Recruit_Finder_AI.Models
{
    public class ApiLoginModel
    {
        [Required(ErrorMessage = "Pole Email lub Nazwa użytkownika jest wymagane.")]

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