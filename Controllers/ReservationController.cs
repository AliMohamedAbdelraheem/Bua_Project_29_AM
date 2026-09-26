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

        // GET: Reservation
        public async Task<IActionResult> Index(
            string? search,
            string? status)
        {
            var query = _context.Reservations
                .Include(r => r.Vehicle)
                .Include(r => r.User)
                .Include(r => r.Driver)
                .AsQueryable();

            // Search
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(r =>
                    r.Origin.Contains(search) ||
                    r.Destination.Contains(search) ||
                    r.Purpose.Contains(search) ||
                    r.User.Name.Contains(search) ||
                    r.ReservationId
                        .ToString()
                        .Contains(search));
            }

            // Status filter
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(
                    r => r.Status == status);
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

        // GET: Reservation/Details/5
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

        // GET: Reservation/Create
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();

            return View();
        }

        // POST: Reservation/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Reservation reservation)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns();

                return View(reservation);
            }

            _context.Reservations.Add(reservation);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Reservation/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            await LoadDropdowns();

            return View(reservation);
        }

        // POST: Reservation/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Reservation reservation)
        {
            if (id != reservation.ReservationId)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadDropdowns();

                return View(reservation);
            }

            try
            {
                _context.Reservations.Update(reservation);

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReservationExists(
                    reservation.ReservationId))
                {
                    return NotFound();
                }

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Reservation/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.Vehicle)
                .Include(r => r.User)
                .Include(r => r.Driver)
                .FirstOrDefaultAsync(
                    r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            return View(reservation);
        }

        // POST: Reservation/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(
            int id)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            _context.Reservations.Remove(reservation);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private bool ReservationExists(int id)
        {
            return _context.Reservations
                .Any(r => r.ReservationId == id);
        }

        private async Task LoadDropdowns()
        {
            ViewBag.Vehicles =
                await _context.Vehicles
                    .ToListAsync();

            ViewBag.Users =
                await _context.Users
                    .ToListAsync();

            ViewBag.Drivers =
                await _context.Drivers
                    .ToListAsync();
        }
    }
}