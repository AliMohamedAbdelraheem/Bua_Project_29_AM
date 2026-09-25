namespace BUA_project.Models
{
    public class AuditLog
    {
        public int AuditLogId { get; set; }

        public string Action { get; set; } = string.Empty;

        public string EntityName { get; set; } = string.Empty;

        public int EntityId { get; set; }

        public int? UserId { get; set; }
        public User? User { get; set; }

        // Role at the moment the action happened
        public string? UserRole { get; set; }

        // Required for critical actions
        // such as approval, reassignment, cancellation and override
        public string? Reason { get; set; }

        public DateTime Timestamp { get; set; }

        // Additional non-sensitive information
        public string? Details { get; set; }
    }
}