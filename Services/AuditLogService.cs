using BUA_project.Models;
using Microsoft.AspNetCore.Identity;

namespace BUA_project.Services
{
    public class AuditLogService
    {
        private readonly Entity _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditLogService(
            Entity context,
            UserManager<ApplicationUser> userManager,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _userManager = userManager;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(
            string action,
            string entityName,
            int entityId,
            string? reason = null,
            string? details = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext == null)
                return;

            if (!httpContext.User.Identity?.IsAuthenticated ?? true)
                return;

            var identityUser = await _userManager.GetUserAsync(httpContext.User);

            if (identityUser == null)
                return;

            var roles = await _userManager.GetRolesAsync(identityUser);

            var auditLog = new AuditLog
            {
                Action = action,
                EntityName = entityName,
                EntityId = entityId,

                // Business User ID, not Identity string ID
                UserId = identityUser.BusinessUserId,

                // Snapshot of the role at the time of the event
                UserRole = roles.FirstOrDefault(),

                Reason = reason,

                Details = details,

                Timestamp = DateTime.Now
            };

            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();
        }
    }
}
