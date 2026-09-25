namespace BUA_project.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        public string Action { get; set; }

        public string EntityName { get; set; }

        public int EntityId { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        public DateTime Timestamp { get; set; }

        public string? Details { get; set; }
    }
}