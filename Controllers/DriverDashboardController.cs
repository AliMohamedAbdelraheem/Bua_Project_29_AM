using BUA_project.Models;
using BUA_project.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Driver")]
    public class DriverDashboardController : Controller
    {
        private readonly Entity _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DriverDashboardController(
            Entity context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        // =========================================================
        // DRIVER DASHBOARD
        // =========================================================

        public async Task<IActionResult> Index()
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var businessUser = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.UserId == identityUser.BusinessUserId);

            if (businessUser == null)
            {
                await _userManager.RemoveFromRoleAsync(identityUser, "Driver");

                await HttpContext.SignOutAsync();

                return RedirectToAction("Login", "Account");
            }

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == businessUser.UserId);

            if (driver == null)
            {
                await _userManager.RemoveFromRoleAsync(identityUser, "Driver");

                await HttpContext.SignOutAsync();

                return RedirectToAction("Login", "Account");
            }

            // Driver must be Qualified
            if (driver.QualificationStatus != "Qualified")
            {
                ViewBag.AccessMessage =
                    "Your driver qualification has not been approved yet.";

                ViewBag.DriverName = driver.Name;

                return View("NotApproved");
            }

            // Qualification must not be expired
            if (driver.QualificationValidUntil.Date < DateTime.Today)
            {
                ViewBag.AccessMessage =
                    "Your driver qualification has expired.";

                ViewBag.DriverName = driver.Name;

                return View("NotApproved");
            }

            ViewBag.DriverName = driver.Name;
            ViewBag.DriverId = driver.DriverId;

            return View(driver);
        }


        // =========================================================
        // MY TRIPS
        // =========================================================

        public async Task<IActionResult> MyTrips()
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            if (driver.QualificationStatus != "Qualified" ||
                driver.QualificationValidUntil.Date < DateTime.Today)
            {
                return RedirectToAction(nameof(Index));
            }

            var trips = await _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .Where(t =>
                    t.Reservation.DriverId == driver.DriverId &&
                    t.Status == "Assigned")
                .OrderBy(t => t.Reservation.StartDateTime)
                .Select(t => new DriverTripViewModel
                {
                    TripId = t.TripId,
                    ReservationId = t.ReservationId,

                    StartDateTime = t.Reservation.StartDateTime,
                    EndDateTime = t.Reservation.EndDateTime,

                    Origin = t.Reservation.Origin,
                    Destination = t.Reservation.Destination,

                    Passengers = t.Reservation.Passengers,
                    Load = t.Reservation.Load,
                    Purpose = t.Reservation.Purpose,

                    Status = t.Status,

                    VehicleId = t.Reservation.VehicleId,

                    VehicleName =
                        t.Reservation.Vehicle.Brand + " " +
                        t.Reservation.Vehicle.Model
                })
                .ToListAsync();

            return View(trips);
        }


        // =========================================================
        // ACTIVE TRIP
        // =========================================================

        public async Task<IActionResult> ActiveTrip(int? id)
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            var query = _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .Where(t =>
                    t.Reservation.DriverId == driver.DriverId &&
                    t.Status == "InProgress");

            Trip? trip;

            if (id.HasValue)
            {
                trip = await query
                    .FirstOrDefaultAsync(t => t.TripId == id.Value);
            }
            else
            {
                trip = await query
                    .OrderByDescending(t => t.StartedAt)
                    .FirstOrDefaultAsync();
            }

            if (trip == null)
            {
                return View("NoActiveTrip");
            }

            return View(trip);
        }


        // =========================================================
        // TRIP HISTORY
        // =========================================================

        public async Task<IActionResult> TripHistory()
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            var trips = await _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .Where(t =>
                    t.Reservation.DriverId == driver.DriverId &&
                    t.Status == "Completed")
                .OrderByDescending(t => t.CompletedAt)
                .ToListAsync();

            return View(trips);
        }


        // =========================================================
        // TRIP DETAILS
        // =========================================================

        public async Task<IActionResult> TripDetails(int id)
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .FirstOrDefaultAsync(t =>
                    t.TripId == id &&
                    t.Reservation.DriverId == driver.DriverId);

            if (trip == null)
                return NotFound();

            return View(trip);
        }


        // =========================================================
        // START TRIP - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> StartTrip(int id)
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .FirstOrDefaultAsync(t =>
                    t.TripId == id &&
                    t.Reservation.DriverId == driver.DriverId &&
                    t.Status == "Assigned");

            if (trip == null)
                return NotFound();

            return View(trip);
        }


        // =========================================================
        // START TRIP - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartTrip(
            int id,
            double startOdometer)
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                .FirstOrDefaultAsync(t =>
                    t.TripId == id &&
                    t.Reservation.DriverId == driver.DriverId);

            if (trip == null)
                return NotFound();

            if (trip.Status != "Assigned")
            {
                TempData["Error"] =
                    "This trip cannot be started.";

                return RedirectToAction(nameof(MyTrips));
            }

            if (startOdometer < 0)
            {
                TempData["Error"] =
                    "Invalid odometer reading.";

                return RedirectToAction(
                    nameof(StartTrip),
                    new { id });
            }

            trip.StartOdometer = startOdometer;
            trip.StartedAt = DateTime.Now;
            trip.Status = "InProgress";

            await _context.SaveChangesAsync();

            return RedirectToAction(
                nameof(ActiveTrip),
                new { id = trip.TripId });
        }


        // =========================================================
        // UPDATE LOCATION
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLocation(
            int tripId,
            double latitude,
            double longitude)
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                .FirstOrDefaultAsync(t =>
                    t.TripId == tripId &&
                    t.Reservation.DriverId == driver.DriverId &&
                    t.Status == "InProgress");

            if (trip == null)
                return NotFound();

            if (latitude < -90 || latitude > 90 ||
                longitude < -180 || longitude > 180)
            {
                TempData["Error"] =
                    "Invalid GPS coordinates.";

                return RedirectToAction(
                    nameof(ActiveTrip),
                    new { id = tripId });
            }

            var locationPing = new LocationPing
            {
                Latitude = latitude,
                Longitude = longitude,
                Timestamp = DateTime.Now,
                TripId = trip.TripId
            };

            _context.LocationPings.Add(locationPing);

            await _context.SaveChangesAsync();

            TempData["Success"] =
                "Your location has been updated successfully.";

            return RedirectToAction(
                nameof(ActiveTrip),
                new { id = tripId });
        }


        // =========================================================
        // COMPLETE TRIP - GET
        // =========================================================

        [HttpGet]
        public async Task<IActionResult> CompleteTrip(int id)
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .FirstOrDefaultAsync(t =>
                    t.TripId == id &&
                    t.Reservation.DriverId == driver.DriverId &&
                    t.Status == "InProgress");

            if (trip == null)
                return NotFound();

            return View(trip);
        }


        // =========================================================
        // COMPLETE TRIP - POST
        // =========================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteTrip(
            int id,
            double endOdometer,
            double actualFuelLiters,
            string? incidentNotes,
            double latitude,
            double longitude)
        {
            var identityUser = await _userManager.GetUserAsync(User);

            if (identityUser == null)
                return RedirectToAction("Login", "Account");

            var driver = await _context.Drivers
                .FirstOrDefaultAsync(d =>
                    d.UserId == identityUser.BusinessUserId);

            if (driver == null)
                return NotFound();

            var trip = await _context.Trips
                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .FirstOrDefaultAsync(t =>
                    t.TripId == id &&
                    t.Reservation.DriverId == driver.DriverId &&
                    t.Status == "InProgress");

            if (trip == null)
                return NotFound();

            // Validate End Odometer
            if (endOdometer < trip.StartOdometer)
            {
                ModelState.AddModelError(
                    "endOdometer",
                    "End odometer cannot be less than start odometer.");

                return View(trip);
            }

            // Validate Actual Fuel
            if (actualFuelLiters < 0)
            {
                ModelState.AddModelError(
                    "actualFuelLiters",
                    "Actual fuel used cannot be negative.");

                return View(trip);
            }

            // Validate GPS
            if (latitude < -90 || latitude > 90 ||
                longitude < -180 || longitude > 180)
            {
                ModelState.AddModelError(
                    "",
                    "Invalid GPS coordinates.");

                return View(trip);
            }

            // Calculate Actual Distance
            double actualDistanceKm =
                endOdometer - trip.StartOdometer;

            // Get Current Fuel Price
            var vehicleFuelType =
                trip.Reservation.Vehicle.FuelType;

            var completionTime = DateTime.Now;

            var fuelPrice = await _context.FuelPrices
                .Where(f =>
                    f.FuelType == vehicleFuelType &&
                    f.EffectiveDate <= completionTime)
                .OrderByDescending(f => f.EffectiveDate)
                .FirstOrDefaultAsync();

            // Make sure Fuel Price exists
            if (fuelPrice == null)
            {
                ModelState.AddModelError(
                    "",
                    $"No fuel price has been configured for {vehicleFuelType}. Please contact the administrator.");

                return View(trip);
            }

            // Calculate Actual Fuel Cost
            decimal actualFuelCost =
                (decimal)actualFuelLiters *
                fuelPrice.PricePerLiter;

            // Save Final GPS Location
            var finalLocation = new LocationPing
            {
                Latitude = latitude,
                Longitude = longitude,
                Timestamp = completionTime,
                TripId = trip.TripId
            };

            _context.LocationPings.Add(finalLocation);

            // Update Trip
            trip.EndOdometer = endOdometer;
            trip.ActualDistanceKm = actualDistanceKm;
            trip.ActualFuelLiters = actualFuelLiters;
            trip.ActualFuelCost = actualFuelCost;
            trip.IncidentNotes = incidentNotes;
            trip.CompletedAt = completionTime;
            trip.Status = "Completed";

            await _context.SaveChangesAsync();

            TempData["Success"] =
                $"Trip completed successfully. Distance: {actualDistanceKm:0.0} KM.";

            return RedirectToAction(nameof(MyTrips));
        }
    }
}