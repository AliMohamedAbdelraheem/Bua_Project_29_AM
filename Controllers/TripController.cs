using BUA_project.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    public class TripController : Controller
    {
        private readonly Entity _context;

        public TripController(Entity context)
        {
            _context = context;
        }

        // GET: Trip
        public async Task<IActionResult> Index()
        {
            var trips = await _context.Trips
                .Include(t => t.Reservation)
                .Include(t => t.RouteEstimate)
                .ToListAsync();

            return View(trips);
        }

        // GET: Trip/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.User)
                .Include(t => t.RouteEstimate)
                .Include(t => t.LocationPings)
                .FirstOrDefaultAsync(
                    t => t.TripId == id);

            if (trip == null)
                return NotFound();

            return View(trip);
        }

        // GET: Trip/Create
        public async Task<IActionResult> Create()
        {
            await LoadDropdowns();

            return View();
        }

        // POST: Trip/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            Trip trip)
        {
            if (!ModelState.IsValid)
            {
                await LoadDropdowns();

                return View(trip);
            }

            _context.Trips.Add(trip);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Trip/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    t => t.TripId == id);

            if (trip == null)
                return NotFound();

            await LoadDropdowns();

            return View(trip);
        }

        // POST: Trip/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            Trip trip)
        {
            if (id != trip.TripId)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadDropdowns();

                return View(trip);
            }

            try
            {
                _context.Trips.Update(trip);

                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TripExists(trip.TripId))
                    return NotFound();

                throw;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Trip/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                .FirstOrDefaultAsync(
                    t => t.TripId == id);

            if (trip == null)
                return NotFound();

            return View(trip);
        }

        // POST: Trip/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(
            int id)
        {
            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    t => t.TripId == id);

            if (trip == null)
                return NotFound();

            _context.Trips.Remove(trip);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Trip/Start/5
        public async Task<IActionResult> Start(int? id)
        {
            if (id == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                .FirstOrDefaultAsync(
                    t => t.TripId == id);

            if (trip == null)
                return NotFound();

            return View(trip);
        }

        // POST: Trip/Start
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Start(int id)
        {
            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    t => t.TripId == id);

            if (trip == null)
                return NotFound();

            trip.StartedAt = DateTime.Now;
            trip.Status = "InProgress";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Trip/Complete/5
        public async Task<IActionResult> Complete(int? id)
        {
            if (id == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                .FirstOrDefaultAsync(
                    t => t.TripId == id);

            if (trip == null)
                return NotFound();

            return View(trip);
        }

        // POST: Trip/Complete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Complete(
            int id,
            double endOdometer,
            double actualDistanceKm,
            double actualFuelLiters,
            decimal actualFuelCost,
            string? incidentNotes)
        {
            var trip = await _context.Trips
                .FirstOrDefaultAsync(
                    t => t.TripId == id);

            if (trip == null)
                return NotFound();

            trip.EndOdometer = endOdometer;
            trip.ActualDistanceKm = actualDistanceKm;
            trip.ActualFuelLiters = actualFuelLiters;
            trip.ActualFuelCost = actualFuelCost;
            trip.IncidentNotes = incidentNotes;

            trip.CompletedAt = DateTime.Now;
            trip.Status = "Completed";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task LoadDropdowns()
        {
            ViewBag.Reservations =
                await _context.Reservations
                    .Where(r =>
                        r.Status == "Approved" &&
                        r.DriverId != null)
                    .ToListAsync();

            ViewBag.RouteEstimates =
                await _context.RouteEstimates
                    .ToListAsync();
        }

        private bool TripExists(int id)
        {
            return _context.Trips
                .Any(t => t.TripId == id);
        }
    }
}