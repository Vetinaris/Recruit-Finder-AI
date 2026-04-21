using Recruit_Finder_AI.Data;
using Recruit_Finder_AI.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
namespace Recruit_Finder_AI.Services
{
    public class AuditService
    {
        private readonly Recruit_Finder_AIContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public AuditService(Recruit_Finder_AIContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }
        private string GetClientIp()
        {
            try
            {
                var ip = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
                return string.IsNullOrWhiteSpace(ip) ? "unknown" : ip;
            }
            catch
            {
                return "unknown";
            }
        }
        public async Task LogActionAsync(string userName, string actionType, string details, bool isSuccess, string userId)
        {

            try
            {
                if (_context == null)
                {
                    Console.WriteLine("[AUDIT ERROR] _context is NULL!");
                    return;
                }

                string ipAddress = GetClientIp();

                var auditLog = new AuditLog
                {
                    Timestamp = DateTime.UtcNow,
                    UserId = userId ?? string.Empty,
                    UserName = userName ?? string.Empty,
                    Action = actionType ?? "UNKNOWN_ACTION",
                    Details = details ?? string.Empty,
                    IsSuccess = isSuccess,
                    IpAddress = ipAddress
                };

                _context.AuditLogs.Add(auditLog);
                int affected = await _context.SaveChangesAsync();

            }
            catch (Exception ex)
            {
                Console.WriteLine($" {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($" Inner: {ex.InnerException.Message}");
            }
        }
        public Task LogAsync(string userName, string action, string details, bool isSuccess, string userId)
        {
            Console.WriteLine("LogAsync CALLED (alias)");
            return LogActionAsync(userName, action, details, isSuccess, userId);
        }
    }
}
