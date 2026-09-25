using BUA_project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditLogController : Controller
    {
        private readonly Entity _context;

        public AuditLogController(Entity context)
        {
            _context = context;
        }

        // GET: /AuditLog
        public async Task<IActionResult> Index(
            string? search,
            string? action,
            string? entityName)
        {
            var query = _context.AuditLogs
                .Include(a => a.User)
                .AsQueryable();

            // Search by user name or entity ID
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(a =>
                    (a.User != null && a.User.Name.Contains(search)) ||
                    a.EntityId.ToString().Contains(search));
            }

            // Filter by action
            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }

            // Filter by entity
            if (!string.IsNullOrWhiteSpace(entityName))
            {
                query = query.Where(a => a.EntityName == entityName);
            }

            var logs = await query
                .OrderByDescending(a => a.Timestamp)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.SelectedAction = action;
            ViewBag.SelectedEntity = entityName;

            ViewBag.Actions = await _context.AuditLogs
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .ToListAsync();

            ViewBag.Entities = await _context.AuditLogs
                .Select(a => a.EntityName)
                .Distinct()
                .OrderBy(e => e)
                .ToListAsync();

            return View(logs);
        }

        // GET: /AuditLog/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var auditLog = await _context.AuditLogs
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.AuditLogId == id);

            if (auditLog == null)
                return NotFound();

            return View(auditLog);
        }
    }
}
