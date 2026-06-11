using Microsoft.Extensions.Configuration;
using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Recruit_Finder_AI.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly JsonSerializerOptions _jsonOptions;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var rabbitHost = _configuration["RabbitMQ:Host"] ?? "localhost";
            Console.WriteLine($">>> [DIAGNOZA] Adres brokera RabbitMQ dla maili: {rabbitHost}");
        }

        public async Task<bool> SendPasswordResetCodeAsync(string email, string code, string testValue = null)
        {
            var payload = new
            {
                email = email,
                resetCode = code,
                testValue = testValue
            };

            return await SendToRabbitMQAsync(payload, "Reset Hasła");
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
            return await SendToRabbitMQAsync(payload, "Potwierdzenie Email");
        }

        private async Task<bool> SendToRabbitMQAsync(object payload, string context)
        {
            var rabbitHost = _configuration["RabbitMQ:Host"] ?? "localhost";

            try
            {
                var factory = new ConnectionFactory() { HostName = rabbitHost };

                using var connection = await factory.CreateConnectionAsync();
                using var channel = await connection.CreateChannelAsync();

                await channel.QueueDeclareAsync(queue: "email_queue",
                                                durable: true,
                                                exclusive: false,
                                                autoDelete: false,
                                                arguments: null);

                var messageBody = JsonSerializer.Serialize(payload, _jsonOptions);
                var body = Encoding.UTF8.GetBytes(messageBody);

                var properties = new BasicProperties
                {
                    Persistent = true
                };

                await channel.BasicPublishAsync(exchange: "",
                                                routingKey: "email_queue",
                                                mandatory: false,
                                                basicProperties: properties,
                                                body: body);

                Console.WriteLine($">>> [EmailService] Sukces ({context}): Wiadomość wrzucona do email_queue w RabbitMQ.");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>> [EmailService] BŁĄD RABBITMQ ({context}): {ex.Message}");
                return false;
            }
        }
    }
}