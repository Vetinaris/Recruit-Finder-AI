using System;
namespace Recruit_Finder_AI.Entities
{
    public class AuditLog
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public string UserName { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
        public string IpAddress { get; set; }
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; }

    }
}