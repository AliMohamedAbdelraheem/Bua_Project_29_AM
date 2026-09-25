using BUA_project.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin,Dispatcher")]
    public class ReservationController : Controller
    {
        private readonly Entity _context;

        public ReservationController(Entity context)
        {
            _context = context;
        }

        // GET: /Reservation/Index
        // Admin / Dispatcher can view reservations
        public async Task<IActionResult> Index(
            string? search,
            string? status)
        {
            var query = _context.Reservations
                .Include(r => r.Vehicle)
                .Include(r => r.User)
                .Include(r => r.Driver)
                .AsQueryable();

            // Search by origin, destination, purpose,
            // requester name, or reservation ID
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    r.Origin.Contains(search) ||
                    r.Destination.Contains(search) ||
                    r.Purpose.Contains(search) ||
                    r.User.Name.Contains(search) ||
                    r.ReservationId.ToString().Contains(search));
            }

            // Filter by reservation status
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status == status);
            }

            var reservations = await query
                .OrderByDescending(r => r.StartDateTime)
                .ToListAsync();

            ViewBag.Search = search;
            ViewBag.SelectedStatus = status;

            ViewBag.Statuses = await _context.Reservations
                .Select(r => r.Status)
                .Distinct()
                .OrderBy(s => s)
                .ToListAsync();

            return View(reservations);
        }

        // GET: /Reservation/Details/5
        // Read-only details for Admin / Dispatcher
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleSpecification)
                .Include(r => r.User)
                .Include(r => r.Driver)
                .Include(r => r.FuelEstimate)
                .Include(r => r.Trip)
                .FirstOrDefaultAsync(
                    r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            return View(reservation);
        }
    }
}
