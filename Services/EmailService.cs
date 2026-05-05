using Microsoft.Extensions.Configuration;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace Recruit_Finder_AI.Services
{
    public class EmailService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        public EmailService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var rawUrl = _configuration["AiService:EmailServiceUrl"];
            Console.WriteLine($">>> [DIAGNOZA] Adres usługi e-mail: {rawUrl ?? "BRAK KLUCZA W KONFIGURACJI!"}");
        }

        public async Task<bool> SendPasswordResetCodeAsync(string email, string code, string testValue = null)
        {
            var payload = new
            {
                email = email,
                resetCode = code,
                testValue = testValue
            };

            return await SendToPythonAsync(payload, "Reset Hasła");
        }

        public async Task<bool> SendEmailConfirmationCodeAsync(string email, string code)
        {
            var payload = new
            {
                email = email,
                confirmationCode = code,
                type = "registration"
            };

            Console.WriteLine($">>> [EmailService] Wysyłka kodu potwierdzającego [{code}] do: {email}");
            return await SendToPythonAsync(payload, "Potwierdzenie Email");
        }

        private async Task<bool> SendToPythonAsync(object payload, string context)
        {
            var emailServiceUrl = _configuration["AiService:EmailServiceUrl"];
            var emailApiKey = _configuration["AiService:EmailApiKey"];
            var client = _httpClientFactory.CreateClient("PythonClient");

            var request = new HttpRequestMessage(HttpMethod.Post, emailServiceUrl);
            request.Headers.Add("X-Email-Key", emailApiKey);
            request.Content = JsonContent.Create(payload, options: _jsonOptions);

            try
            {
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($">>> [EmailService] Python odebrał dane ({context}) poprawnie.");
                    return true;
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($">>> [EmailService] Błąd ({context}): {response.StatusCode}, Treść: {errorBody}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> [EmailService] BŁĄD POŁĄCZENIA ({context}): {ex.Message}");
                return false;
            }
        }
    }
}