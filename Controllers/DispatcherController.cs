using BUA_project.Models;
using BUA_project.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Dispatcher")]
    public class DispatcherController : Controller
    {
        private readonly Entity _context;

        public DispatcherController(Entity context)
        {
            _context = context;
        }

        // =========================================================
        // GET: Dispatcher/Dashboard
        // Dispatcher Dashboard
        // =========================================================

        public async Task<IActionResult> Dashboard()
        {
            var viewModel = new DispatcherDashboardViewModel
            {
                PendingRequestsCount = await _context.Reservations
                    .CountAsync(r => r.Status == "Pending"),

                AvailableVehiclesCount = await _context.Vehicles
                    .CountAsync(v => v.Status == "Available"),

                AvailableDriversCount = await _context.Drivers
                    .CountAsync(d =>
                        d.QualificationStatus == "Qualified" &&
                        d.QualificationValidUntil >= DateTime.Now),

                ActiveTripsCount = await _context.Trips
                    .CountAsync(t =>
                        t.Status == "InProgress" &&
                        t.CompletedAt == null),

                RecentPendingRequests = await _context.Reservations
                    .Include(r => r.User)
                    .Include(r => r.Vehicle)
                    .Where(r => r.Status == "Pending")
                    .OrderByDescending(r => r.StartDateTime)
                    .Take(5)
                    .ToListAsync()
            };

            return View("Dashboard/Index", viewModel);
        }

        // =========================================================
        // GET: Dispatcher/ActiveTrips
        // =========================================================

        public async Task<IActionResult> ActiveTrips()
        {
            var activeTrips = await _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.User)
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Driver)
                .Where(t =>
                    t.Status == "InProgress" &&
                    t.CompletedAt == null)
                .OrderByDescending(t => t.StartedAt)
                .ToListAsync();

            return View(activeTrips);
        }

        // =========================================================
        // GET: Dispatcher
        // Pending Requests / Reservations waiting for Driver
        // =========================================================

        public async Task<IActionResult> Index()
        {
            var reservations = await _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Vehicle)
                .Include(r => r.Driver)
                .Where(r =>
                    r.Status == "Pending" ||
                    (r.Status == "Approved" && r.DriverId == null))
                .OrderBy(r => r.StartDateTime)
                .ToListAsync();

            return View(reservations);
        }

        // =========================================================
        // GET: Dispatcher/Details/5
        // =========================================================

        public async Task<IActionResult> Details(int? id)
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

        // =========================================================
        // GET: Dispatcher/Approve/5
        // =========================================================

        public async Task<IActionResult> Approve(int? id)
        {
            if (id == null)
                return NotFound();

            var reservation = await _context.Reservations
                .Include(r => r.Vehicle)
                .Include(r => r.User)
                .FirstOrDefaultAsync(
                    r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            if (reservation.Status != "Pending")
                return BadRequest(
                    "Only pending reservations can be approved.");

            // -----------------------------------------------------
            // Find reservations that overlap with this reservation
            // -----------------------------------------------------

            var conflictingReservations =
                await _context.Reservations
                    .Where(r =>
                        r.ReservationId != reservation.ReservationId &&
                        (
                            r.Status == "Approved" ||
                            r.Status == "Dispatched" ||
                            r.Status == "Active"
                        ) &&
                        r.StartDateTime < reservation.EndDateTime &&
                        r.EndDateTime > reservation.StartDateTime
                    )
                    .ToListAsync();

            // -----------------------------------------------------
            // Vehicles already used during this period
            // -----------------------------------------------------

            var conflictingVehicleIds =
                conflictingReservations
                    .Select(r => r.VehicleId)
                    .Distinct()
                    .ToList();

            // -----------------------------------------------------
            // Drivers already used during this period
            // -----------------------------------------------------

            var conflictingDriverIds =
                conflictingReservations
                    .Where(r => r.DriverId.HasValue)
                    .Select(r => r.DriverId!.Value)
                    .Distinct()
                    .ToList();

            // -----------------------------------------------------
            // Available Vehicles
            // -----------------------------------------------------

            var vehicles = await _context.Vehicles
                .Where(v =>
                    v.Status == "Available" &&
                    !conflictingVehicleIds.Contains(v.VehicleId))
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();

            // -----------------------------------------------------
            // Available Qualified Drivers
            // -----------------------------------------------------

            var drivers = await _context.Drivers
                .Where(d =>
                    d.QualificationStatus == "Qualified" &&
                    d.QualificationValidUntil >= reservation.EndDateTime &&
                    !conflictingDriverIds.Contains(d.DriverId))
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.Vehicles = vehicles;
            ViewBag.Drivers = drivers;

            return View(reservation);
        }

        // =========================================================
        // POST: Dispatcher/Approve
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            int id,
            int vehicleId,
            int driverId)
        {
            // -----------------------------------------------------
            // Get Reservation
            // -----------------------------------------------------

            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            // -----------------------------------------------------
            // Reservation must still be Pending
            // -----------------------------------------------------

            if (reservation.Status != "Pending")
                return BadRequest(
                    "Only pending reservations can be approved.");

            // -----------------------------------------------------
            // Get Selected Vehicle
            // -----------------------------------------------------

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(
                    v => v.VehicleId == vehicleId);

            if (vehicle == null)
                return NotFound("Vehicle not found.");

            // -----------------------------------------------------
            // Vehicle must be Available
            // -----------------------------------------------------

            if (vehicle.Status != "Available")
            {
                ModelState.AddModelError(
                    "",
                    "The selected vehicle is no longer available.");

                return await ReturnApproveView(reservation);
            }

            // -----------------------------------------------------
            // Get Selected Driver
            // -----------------------------------------------------

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(
                    d => d.DriverId == driverId);

            if (driver == null)
                return NotFound("Driver not found.");

            // -----------------------------------------------------
            // Driver must be Qualified
            // -----------------------------------------------------

            if (driver.QualificationStatus != "Qualified")
            {
                ModelState.AddModelError(
                    "",
                    "The selected driver is not qualified.");

                return await ReturnApproveView(reservation);
            }

            // -----------------------------------------------------
            // Driver qualification must still be valid
            // -----------------------------------------------------

            if (driver.QualificationValidUntil <
                reservation.EndDateTime)
            {
                ModelState.AddModelError(
                    "",
                    "The driver's qualification is not valid for this reservation.");

                return await ReturnApproveView(reservation);
            }

            // -----------------------------------------------------
            // Find Overlapping Reservations
            // -----------------------------------------------------

            var overlappingReservations =
                await _context.Reservations
                    .Where(r =>
                        r.ReservationId != reservation.ReservationId &&
                        (
                            r.Status == "Approved" ||
                            r.Status == "Dispatched" ||
                            r.Status == "Active"
                        ) &&
                        r.StartDateTime < reservation.EndDateTime &&
                        r.EndDateTime > reservation.StartDateTime
                    )
                    .ToListAsync();

            // -----------------------------------------------------
            // Check Vehicle Conflict
            // -----------------------------------------------------

            var vehicleConflict =
                overlappingReservations
                    .Any(r => r.VehicleId == vehicleId);

            if (vehicleConflict)
            {
                ModelState.AddModelError(
                    "",
                    "The selected vehicle is already reserved during this time.");

                return await ReturnApproveView(reservation);
            }

            // -----------------------------------------------------
            // Check Driver Conflict
            // -----------------------------------------------------

            var driverConflict =
                overlappingReservations
                    .Any(r => r.DriverId == driverId);

            if (driverConflict)
            {
                ModelState.AddModelError(
                    "",
                    "The selected driver is already assigned during this time.");

                return await ReturnApproveView(reservation);
            }

            // -----------------------------------------------------
            // Assign Vehicle
            // -----------------------------------------------------

            reservation.VehicleId = vehicleId;

            // -----------------------------------------------------
            // Assign Driver
            // -----------------------------------------------------

            reservation.DriverId = driverId;

            // -----------------------------------------------------
            // Approve Reservation
            // -----------------------------------------------------

            reservation.Status = "Approved";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // Reload Approve View After Validation Error
        // =========================================================

        private async Task<IActionResult> ReturnApproveView(
            Reservation reservation)
        {
            var conflictingReservations =
                await _context.Reservations
                    .Where(r =>
                        r.ReservationId != reservation.ReservationId &&
                        (
                            r.Status == "Approved" ||
                            r.Status == "Dispatched" ||
                            r.Status == "Active"
                        ) &&
                        r.StartDateTime < reservation.EndDateTime &&
                        r.EndDateTime > reservation.StartDateTime
                    )
                    .ToListAsync();

            var conflictingVehicleIds =
                conflictingReservations
                    .Select(r => r.VehicleId)
                    .Distinct()
                    .ToList();

            var conflictingDriverIds =
                conflictingReservations
                    .Where(r => r.DriverId.HasValue)
                    .Select(r => r.DriverId!.Value)
                    .Distinct()
                    .ToList();

            var vehicles = await _context.Vehicles
                .Where(v =>
                    v.Status == "Available" &&
                    !conflictingVehicleIds.Contains(v.VehicleId))
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();

            var drivers = await _context.Drivers
                .Where(d =>
                    d.QualificationStatus == "Qualified" &&
                    d.QualificationValidUntil >= reservation.EndDateTime &&
                    !conflictingDriverIds.Contains(d.DriverId))
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.Vehicles = vehicles;
            ViewBag.Drivers = drivers;

            return View("Approve", reservation);
        }

        // =========================================================
        // GET: Dispatcher/Reject/5
        // =========================================================

        public async Task<IActionResult> Reject(int? id)
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

        // =========================================================
        // POST: Dispatcher/Reject
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            reservation.Status = "Rejected";

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // =========================================================
        // GET: Dispatcher/AssignDriver/5
        // =========================================================

        public async Task<IActionResult> AssignDriver(int? id)
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

            if (reservation.Status != "Approved")
                return BadRequest(
                    "Reservation must be approved first.");

            ViewBag.Drivers = await _context.Drivers
                .ToListAsync();

            return View(reservation);
        }

        // =========================================================
        // POST: Dispatcher/AssignDriver
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDriver(
            int id,
            int driverId)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(
                    r => r.ReservationId == id);

            if (reservation == null)
                return NotFound();

            if (reservation.Status != "Approved")
                return BadRequest(
                    "Reservation must be approved first.");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(
                    d => d.DriverId == driverId);

            if (driver == null)
                return NotFound();

            reservation.DriverId = driverId;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
