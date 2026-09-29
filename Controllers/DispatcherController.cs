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
        // DASHBOARD
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
        // ACTIVE TRIPS
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
        // PENDING REQUESTS
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
        // DETAILS
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
        // APPROVE - GET
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

            // Find conflicting reservations
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

            // Available Vehicles
            var vehicles = await _context.Vehicles
                .Where(v =>
                    v.Status == "Available" &&
                    !conflictingVehicleIds.Contains(v.VehicleId))
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();

            // Qualified Drivers
            var drivers = await _context.Drivers
                .Where(d =>
                    d.QualificationStatus == "Qualified" &&
                    d.QualificationValidUntil >= reservation.EndDateTime)
                .OrderBy(d => d.Name)
                .ToListAsync();

            ViewBag.Vehicles = vehicles;
            ViewBag.Drivers = drivers;

            return View(reservation);
        }


        // =========================================================
        // APPROVE - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(
            int id,
            int vehicleId,
            int driverId)
        {
            // =========================================================
            // Get Reservation
            // =========================================================

            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r =>
                    r.ReservationId == id);

            if (reservation == null)
                return NotFound();


            // =========================================================
            // Reservation must still be Pending
            // =========================================================

            if (reservation.Status != "Pending")
            {
                TempData["Error"] =
                    "This reservation is no longer pending.";

                return RedirectToAction(nameof(Index));
            }


            // =========================================================
            // Get Vehicle
            // =========================================================

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == vehicleId);

            if (vehicle == null)
            {
                ModelState.AddModelError(
                    "",
                    "The selected vehicle was not found.");

                return await ReturnApproveView(reservation);
            }


            // =========================================================
            // Vehicle must be Available
            // =========================================================

            if (vehicle.Status != "Available")
            {
                ModelState.AddModelError(
                    "",
                    "The selected vehicle is no longer available.");

                return await ReturnApproveView(reservation);
            }


            // =========================================================
            // Check Vehicle Maintenance
            // =========================================================

            var vehicleUnderMaintenance =
                await _context.Set<VehicleMaintenance>()
                    .AnyAsync(m =>
                        m.VehicleId == vehicleId &&

                        (
                            m.Status == "Scheduled" ||
                            m.Status == "In Progress"
                        ) &&

                        m.ServiceDate <= reservation.EndDateTime &&

                        (
                            m.NextServiceDate == null ||
                            m.NextServiceDate >= reservation.StartDateTime
                        )
                    );

            if (vehicleUnderMaintenance)
            {
                ModelState.AddModelError(
                    "",
                    "This vehicle is under maintenance during the reservation period.");

                return await ReturnApproveView(reservation);
            }


            // =========================================================
            // Get Driver
            // =========================================================

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.DriverId == driverId);

            if (driver == null)
            {
                ModelState.AddModelError(
                    "",
                    "The selected driver was not found.");

                return await ReturnApproveView(reservation);
            }


            // =========================================================
            // Driver must be Qualified
            // =========================================================

            if (driver.QualificationStatus != "Qualified")
            {
                ModelState.AddModelError(
                    "",
                    "The selected driver is not qualified.");

                return await ReturnApproveView(reservation);
            }


            // =========================================================
            // Driver qualification must still be valid
            // =========================================================

            if (driver.QualificationValidUntil < reservation.EndDateTime)
            {
                ModelState.AddModelError(
                    "",
                    "The driver's qualification is not valid for this reservation.");

                return await ReturnApproveView(reservation);
            }


            // =========================================================
            // Find Existing Approved / Active Conflicts
            // =========================================================

            var existingConflicts =
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


            // =========================================================
            // Check Vehicle Conflict
            // =========================================================

            var vehicleConflict =
                await _context.Reservations
                    .AnyAsync(r =>
                        r.ReservationId != reservation.ReservationId &&

                        r.VehicleId == vehicleId &&

                        (
                            r.Status == "Approved" ||
                            r.Status == "Dispatched" ||
                            r.Status == "Active"
                        ) &&

                        r.StartDateTime < reservation.EndDateTime &&
                        r.EndDateTime > reservation.StartDateTime
                    );

            if (vehicleConflict)
            {
                ModelState.AddModelError(
                    "",
                    "The selected vehicle is already assigned to another reservation during this period.");

                return await ReturnApproveView(reservation);
            }


            // =========================================================
            // Check Driver Conflict
            // =========================================================

            var driverConflict =
                await _context.Reservations
                    .AnyAsync(r =>
                        r.ReservationId != reservation.ReservationId &&

                        r.DriverId == driverId &&

                        (
                            r.Status == "Approved" ||
                            r.Status == "Dispatched" ||
                            r.Status == "Active"
                        ) &&

                        r.StartDateTime < reservation.EndDateTime &&
                        r.EndDateTime > reservation.StartDateTime
                    );

            if (driverConflict)
            {
                ModelState.AddModelError(
                    "",
                    "The selected driver is already assigned to another reservation during this period.");

                return await ReturnApproveView(reservation);
            }


            // =========================================================
            // ASSIGN VEHICLE
            // =========================================================

            reservation.VehicleId = vehicleId;


            // =========================================================
            // ASSIGN DRIVER
            // =========================================================

            reservation.DriverId = driverId;


            // =========================================================
            // APPROVE RESERVATION
            // =========================================================

            reservation.Status = "Approved";


            // =========================================================
            // REJECT OVERLAPPING PENDING RESERVATIONS
            // =========================================================

            var conflictingPendingReservations =
                await _context.Reservations
                    .Where(r =>
                        r.ReservationId != reservation.ReservationId &&

                        r.VehicleId == reservation.VehicleId &&

                        r.Status == "Pending" &&

                        r.StartDateTime < reservation.EndDateTime &&
                        r.EndDateTime > reservation.StartDateTime
                    )
                    .ToListAsync();


            foreach (var conflictingReservation
                     in conflictingPendingReservations)
            {
                conflictingReservation.Status = "Rejected";
            }


            // =========================================================
            // CREATE TRIP
            // =========================================================

            var existingTrip = await _context.Trips
                .FirstOrDefaultAsync(t =>
                    t.ReservationId == reservation.ReservationId);


            if (existingTrip == null)
            {
                var trip = new Trip
                {
                    ReservationId = reservation.ReservationId,

                    Status = "Assigned",

                    StartedAt = DateTime.Now,

                    CompletedAt = null,

                    ActualDistanceKm = 0,

                    ActualFuelLiters = 0,

                    ActualFuelCost = 0,

                    StartOdometer = 0,

                    EndOdometer = 0,

                    IncidentNotes = null,

                    FuelPricePerLiter = 0
                };

                _context.Trips.Add(trip);
            }
            else
            {
                existingTrip.Status = "Assigned";
            }


            // =========================================================
            // SAVE EVERYTHING
            // =========================================================

            await _context.SaveChangesAsync();


            // =========================================================
            // SUCCESS MESSAGE
            // =========================================================

            if (conflictingPendingReservations.Any())
            {
                TempData["Success"] =
                    $"Reservation approved successfully. " +
                    $"{conflictingPendingReservations.Count} conflicting pending reservation(s) were rejected.";
            }
            else
            {
                TempData["Success"] =
                    "Reservation approved and trip assigned successfully.";
            }


            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // RELOAD APPROVE VIEW
        // =========================================================

        private async Task<IActionResult> ReturnApproveView(
            Reservation reservation)
        {
            // =========================================================
            // Find Existing Conflicts
            // =========================================================

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


            // =========================================================
            // Conflicting Vehicle IDs
            // =========================================================

            var conflictingVehicleIds =
                conflictingReservations
                    .Select(r => r.VehicleId)
                    .Distinct()
                    .ToList();


            // =========================================================
            // Conflicting Driver IDs
            // =========================================================

            var conflictingDriverIds =
                conflictingReservations
                    .Where(r => r.VehicleId > 0)
                    .Select(r => r.VehicleId)
                    .Distinct()
                    .ToList();


            // =========================================================
            // Available Vehicles
            // =========================================================

            var vehicles = await _context.Vehicles
                .Where(v =>
                    v.Status == "Available" &&
                    !conflictingVehicleIds.Contains(v.VehicleId))
                .OrderBy(v => v.Brand)
                .ThenBy(v => v.Model)
                .ToListAsync();


            // =========================================================
            // Available Drivers
            // =========================================================

            var drivers = await _context.Drivers
                .Where(d =>
                    d.QualificationStatus == "Qualified" &&

                    d.QualificationValidUntil >=
                        reservation.EndDateTime &&

                    !conflictingDriverIds.Contains(d.DriverId)
                )
                .OrderBy(d => d.Name)
                .ToListAsync();


            ViewBag.Vehicles = vehicles;

            ViewBag.Drivers = drivers;


            return View("Approve", reservation);
        }


        // =========================================================
        // REJECT - GET
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
        // REJECT - POST
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

            TempData["Success"] =
                "Reservation rejected successfully.";

            return RedirectToAction(nameof(Index));
        }


        // =========================================================
        // ASSIGN DRIVER - GET
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
                .Where(d =>
                    d.QualificationStatus == "Qualified" &&

                    d.QualificationValidUntil >=
                        reservation.EndDateTime)
                .OrderBy(d => d.Name)
                .ToListAsync();


            return View(reservation);
        }


        // =========================================================
        // ASSIGN DRIVER - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignDriver(
            int id,
            int driverId)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r =>
                    r.ReservationId == id);

            if (reservation == null)
                return NotFound();


            if (reservation.Status != "Approved")
                return BadRequest(
                    "Reservation must be approved first.");


            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.DriverId == driverId);

            if (driver == null)
                return NotFound();


            // =========================================================
            // Driver Qualification
            // =========================================================

            if (driver.QualificationStatus != "Qualified")
            {
                ModelState.AddModelError(
                    "",
                    "The selected driver is not qualified.");

                return await AssignDriver(id);
            }


            // =========================================================
            // Driver Qualification Validity
            // =========================================================

            if (driver.QualificationValidUntil <
                reservation.EndDateTime)
            {
                ModelState.AddModelError(
                    "",
                    "The driver's qualification is not valid for this reservation.");

                return await AssignDriver(id);
            }


            // =========================================================
            // Driver Conflict Check
            // =========================================================

            var driverConflict =
                await _context.Reservations
                    .AnyAsync(r =>
                        r.ReservationId != reservation.ReservationId &&

                        r.DriverId == driverId &&

                        (
                            r.Status == "Approved" ||
                            r.Status == "Dispatched" ||
                            r.Status == "Active"
                        ) &&

                        r.StartDateTime < reservation.EndDateTime &&
                        r.EndDateTime > reservation.StartDateTime
                    );


            if (driverConflict)
            {
                ModelState.AddModelError(
                    "",
                    "The selected driver is already assigned during this time.");

                return await AssignDriver(id);
            }


            // =========================================================
            // Assign Driver
            // =========================================================

            reservation.DriverId = driverId;


            // =========================================================
            // Create Trip if not exists
            // =========================================================

            var existingTrip = await _context.Trips
                .FirstOrDefaultAsync(t =>
                    t.ReservationId ==
                    reservation.ReservationId);


            if (existingTrip == null)
            {
                var trip = new Trip
                {
                    ReservationId =
                        reservation.ReservationId,

                    Status = "Assigned",

                    StartedAt = DateTime.Now,

                    CompletedAt = null,

                    ActualDistanceKm = 0,

                    ActualFuelLiters = 0,

                    ActualFuelCost = 0,

                    StartOdometer = 0,

                    EndOdometer = 0,

                    IncidentNotes = null,

                    FuelPricePerLiter = 0
                };

                _context.Trips.Add(trip);
            }
            else
            {
                existingTrip.Status = "Assigned";
            }


            await _context.SaveChangesAsync();


            TempData["Success"] =
                "Driver assigned successfully.";

            return RedirectToAction(nameof(Index));
        }
    }
}

