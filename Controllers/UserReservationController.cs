using BUA_project.Models;
using BUA_project.Models.ViewModels;
using BUA_project.Services;
using BUA_project.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "User")]
    public class UserReservationController : Controller
    {
        private readonly Entity _context;
        private readonly FuelPredictionService _fuelPredictionService;

        public UserReservationController(
            Entity context,
            FuelPredictionService fuelPredictionService)
        {
            _context = context;
            _fuelPredictionService = fuelPredictionService;
        }

        // ============================================================
        // GET: UserReservation/Create
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Create(int? vehicleId)
        {
            if (vehicleId == null)
                return BadRequest();

            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == vehicleId &&
                    v.Status == "Available");

            if (vehicle == null)
                return NotFound();

            var routes = await _context.RouteEstimates
                .OrderBy(r => r.Origin)
                .ThenBy(r => r.Destination)
                .ToListAsync();

            var viewModel = new UserReservationViewModel
            {
                VehicleId = vehicle.VehicleId
            };

            ViewBag.Vehicle = vehicle;

            ViewBag.Origins = routes
                .Select(r => r.Origin)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            ViewBag.Routes = routes;

            return View(viewModel);
        }

        // ============================================================
        // API: Get Calendar Availability
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> GetCalendarAvailability(
            int vehicleId,
            int year,
            int month)
        {
            var vehicleExists = await _context.Vehicles
                .AnyAsync(v =>
                    v.VehicleId == vehicleId);

            if (!vehicleExists)
                return NotFound();

            var firstDay = new DateTime(year, month, 1);
            var lastDay = firstDay.AddMonths(1);

            var reservations = await _context.Reservations
                .Where(r =>
                    r.VehicleId == vehicleId &&
                    r.StartDateTime < lastDay &&
                    r.EndDateTime >= firstDay &&
                    (
                        r.Status == "Pending" ||
                        r.Status == "Approved"
                    ))
                .Select(r => new
                {
                    r.StartDateTime,
                    r.EndDateTime,
                    r.Status
                })
                .ToListAsync();

            var days = new List<object>();

            for (
                var date = firstDay;
                date < lastDay;
                date = date.AddDays(1))
            {
                var dayStart = date;
                var dayEnd = date.AddDays(1);

                var dayReservations = reservations
                    .Where(r =>
                        r.StartDateTime < dayEnd &&
                        r.EndDateTime > dayStart)
                    .ToList();

                string status = "Available";

                if (dayReservations.Any(r => r.Status == "Approved"))
                {
                    status = "Approved";
                }
                else if (dayReservations.Any(r => r.Status == "Pending"))
                {
                    status = "Pending";
                }

                days.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    status = status
                });
            }

            return Json(days);
        }

        // ============================================================
        // API: Get Hourly Availability
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> GetDayAvailability(
            int vehicleId,
            string date)
        {
            if (!DateTime.TryParse(
                    date,
                    out DateTime selectedDate))
            {
                return BadRequest();
            }

            selectedDate = selectedDate.Date;

            var nextDay = selectedDate.AddDays(1);

            var reservations = await _context.Reservations
                .Where(r =>
                    r.VehicleId == vehicleId &&
                    r.StartDateTime < nextDay &&
                    r.EndDateTime > selectedDate &&
                    (
                        r.Status == "Pending" ||
                        r.Status == "Approved"
                    ))
                .Select(r => new
                {
                    r.StartDateTime,
                    r.EndDateTime,
                    r.Status
                })
                .ToListAsync();

            var hours = new List<object>();

            // 24 hours
            for (int hour = 0; hour < 24; hour++)
            {
                var slotStart =
                    selectedDate.AddHours(hour);

                var slotEnd =
                    slotStart.AddHours(1);

                var reservation =
                    reservations.FirstOrDefault(r =>
                        r.StartDateTime < slotEnd &&
                        r.EndDateTime > slotStart);

                string status = "Available";

                if (reservation != null)
                {
                    status = reservation.Status;
                }

                hours.Add(new
                {
                    hour = hour,
                    start = slotStart.ToString("HH:mm"),
                    end = slotEnd.ToString("HH:mm"),
                    status = status,
                    available = status == "Available"
                });
            }

            return Json(hours);
        }

        // ============================================================
        // POST: UserReservation/Create
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            UserReservationViewModel model)
        {
            // --------------------------------------------------------
            // Model Validation
            // --------------------------------------------------------

            if (!ModelState.IsValid)
            {
                await LoadCreateData(model.VehicleId);
                return View(model);
            }

            // --------------------------------------------------------
            // Current User
            // --------------------------------------------------------

            var currentUserEmail = User.Identity?.Name;

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == currentUserEmail);

            if (currentUser == null)
                return Unauthorized();

            // --------------------------------------------------------
            // Check Vehicle
            // --------------------------------------------------------

            var selectedVehicle = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == model.VehicleId &&
                    v.Status == "Available");

            if (selectedVehicle == null)
            {
                TempData.Clear();

                return RedirectToAction(
                    "Index",
                    "UserDashboard");
            }

            // ========================================================
            // Validate Date Range
            // ========================================================

            if (model.StartDateTime >= model.EndDateTime)
            {
                ModelState.AddModelError(
                    "",
                    "End date and time must be after the start date and time.");
            }

            // ========================================================
            // Check Vehicle Maintenance
            // ========================================================

            var vehicleUnderMaintenance =
                await IsVehicleUnderMaintenance(
                    model.VehicleId,
                    model.StartDateTime,
                    model.EndDateTime);

            if (vehicleUnderMaintenance)
            {
                ModelState.AddModelError(
                    "",
                    "This vehicle is under maintenance during the selected trip period.");
            }

            // ========================================================
            // Check Existing Reservations
            // ========================================================

            var overlappingReservation =
                await HasOverlappingReservation(
                    model.VehicleId,
                    model.StartDateTime,
                    model.EndDateTime);

            if (overlappingReservation)
            {
                ModelState.AddModelError(
                    "",
                    "This vehicle is already reserved during the selected time period.");
            }

            // --------------------------------------------------------
            // Validate Passengers
            // --------------------------------------------------------

            if (model.Passengers < 1)
            {
                ModelState.AddModelError(
                    nameof(model.Passengers),
                    "Passengers must be at least 1.");
            }
            else if (model.Passengers > selectedVehicle.Seats)
            {
                ModelState.AddModelError(
                    nameof(model.Passengers),
                    $"Passengers cannot exceed the vehicle capacity of {selectedVehicle.Seats}.");
            }

            // --------------------------------------------------------
            // Validate Load
            // --------------------------------------------------------

            if (model.Load < 0)
            {
                ModelState.AddModelError(
                    nameof(model.Load),
                    "Load cannot be negative.");
            }
            else if (selectedVehicle.VehicleSpecification == null)
            {
                ModelState.AddModelError(
                    "",
                    "Vehicle specification is missing.");
            }
            else if (
                model.Load >
                selectedVehicle.VehicleSpecification.AllowedLoad)
            {
                ModelState.AddModelError(
                    nameof(model.Load),
                    $"Load cannot exceed the allowed load of {selectedVehicle.VehicleSpecification.AllowedLoad}.");
            }

            // --------------------------------------------------------
            // Return If Validation Failed
            // --------------------------------------------------------

            if (!ModelState.IsValid)
            {
                await LoadCreateData(model.VehicleId);
                return View(model);
            }

            // --------------------------------------------------------
            // Find Route
            // --------------------------------------------------------

            var route = await _context.RouteEstimates
                .FirstOrDefaultAsync(r =>
                    r.Origin == model.Origin &&
                    r.Destination == model.Destination);

            if (route == null)
            {
                ModelState.AddModelError(
                    "",
                    "The selected route is not available.");

                await LoadCreateData(model.VehicleId);

                return View(model);
            }

            // ========================================================
            // Get Fuel Price
            // ========================================================

            var fuelPrice = await _context.FuelPrices
                .Where(f =>
                    f.FuelType == selectedVehicle.FuelType)
                .OrderByDescending(f => f.EffectiveDate)
                .FirstOrDefaultAsync();

            if (fuelPrice == null)
            {
                fuelPrice = await _context.FuelPrices
                    .OrderByDescending(f => f.EffectiveDate)
                    .FirstOrDefaultAsync();
            }

            if (fuelPrice == null)
            {
                ModelState.AddModelError(
                    "",
                    "Fuel price has not been configured by administrator.");

                await LoadCreateData(model.VehicleId);

                return View(model);
            }

            // ========================================================
            // Prepare ML Prediction
            // ========================================================

            float fuelDistance =
                (float)route.Distance;

            string fuelVehicleType =
                selectedVehicle.Type;

            float fuelNominal =
                (float)selectedVehicle
                    .VehicleSpecification!
                    .NominalLPer100Km;

            float fuelDuration =
                (float)route.Duration.TotalMinutes;

            var fuelRequest = new FuelPredictionRequest
            {
                Distance_km = fuelDistance,
                Vehicle_Type = fuelVehicleType,
                Passengers = model.Passengers,
                Nominal_L_per_100km = fuelNominal,
                Duration_min = fuelDuration
            };

            var fuelPrediction =
                await _fuelPredictionService
                    .PredictAsync(fuelRequest);

            if (fuelPrediction == null)
            {
                ModelState.AddModelError(
                    "",
                    "Fuel prediction service is currently unavailable.");

                await LoadCreateData(model.VehicleId);

                return View(model);
            }

            // ========================================================
            // Calculate Fuel
            // ========================================================

            double predictedFuel =
                fuelPrediction.predicted_fuel;

            double fuelMin =
                predictedFuel * 0.90;

            double fuelMax =
                predictedFuel * 1.10;

            // ========================================================
            // Calculate Cost
            // ========================================================

            double fuelPricePerLiter =
                (double)fuelPrice.PricePerLiter;

            double fuelCost =
                predictedFuel * fuelPricePerLiter;

            double fuelCostMin =
                fuelMin * fuelPricePerLiter;

            double fuelCostMax =
                fuelMax * fuelPricePerLiter;

            // ========================================================
            // Store Fuel Data
            // ========================================================

            TempData["PredictedFuel"] =
                predictedFuel.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelMin"] =
                fuelMin.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelMax"] =
                fuelMax.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelCost"] =
                fuelCost.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelCostMin"] =
                fuelCostMin.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelCostMax"] =
                fuelCostMax.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelPricePerLiter"] =
                fuelPricePerLiter.ToString(
                    CultureInfo.InvariantCulture);

            // ========================================================
            // Store ML Features
            // ========================================================

            TempData["FuelDistance"] =
                fuelDistance.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelVehicleType"] =
                fuelVehicleType;

            TempData["FuelNominal"] =
                fuelNominal.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelDuration"] =
                fuelDuration.ToString(
                    CultureInfo.InvariantCulture);

            TempData["FuelMethod"] =
                "ML Prediction";

            TempData["FuelAssumptions"] =
                "Fuel range ±10% of ML prediction";

            // ========================================================
            // Store Reservation Data
            // ========================================================

            TempData["VehicleId"] =
                model.VehicleId.ToString();

            TempData["StartDateTime"] =
                model.StartDateTime.ToString("O");

            TempData["EndDateTime"] =
                model.EndDateTime.ToString("O");

            TempData["Origin"] =
                model.Origin;

            TempData["Destination"] =
                model.Destination;

            TempData["Passengers"] =
                model.Passengers.ToString();

            TempData["Load"] =
                model.Load.ToString(
                    CultureInfo.InvariantCulture);

            TempData["Purpose"] =
                model.Purpose;

            TempData["RouteEstimateId"] =
                route.RouteEstimateId.ToString();

            return RedirectToAction(
                "Confirm",
                "UserReservation");
        }

        // ============================================================
        // GET: UserReservation/Confirm
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> Confirm()
        {
            if (TempData["VehicleId"] == null ||
                TempData["RouteEstimateId"] == null)
            {
                return BadRequest();
            }

            int vehicleId =
                int.Parse(
                    TempData["VehicleId"]!
                        .ToString()!);

            int routeEstimateId =
                int.Parse(
                    TempData["RouteEstimateId"]!
                        .ToString()!);

            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == vehicleId &&
                    v.Status == "Available");

            if (vehicle == null)
            {
                TempData.Clear();

                return RedirectToAction(
                    "Index",
                    "UserDashboard");
            }

            var route = await _context.RouteEstimates
                .FirstOrDefaultAsync(r =>
                    r.RouteEstimateId == routeEstimateId);

            if (route == null)
            {
                TempData.Clear();

                return RedirectToAction(
                    "Index",
                    "UserDashboard");
            }

            DateTime startDateTime =
                DateTime.Parse(
                    TempData["StartDateTime"]!
                        .ToString()!);

            DateTime endDateTime =
                DateTime.Parse(
                    TempData["EndDateTime"]!
                        .ToString()!);

            string origin =
                TempData["Origin"]?
                    .ToString() ?? "";

            string destination =
                TempData["Destination"]?
                    .ToString() ?? "";

            int passengers =
                int.Parse(
                    TempData["Passengers"]!
                        .ToString()!);

            double load =
                double.Parse(
                    TempData["Load"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            string purpose =
                TempData["Purpose"]?
                    .ToString() ?? "";

            double predictedFuel =
                double.Parse(
                    TempData["PredictedFuel"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelMin =
                double.Parse(
                    TempData["FuelMin"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelMax =
                double.Parse(
                    TempData["FuelMax"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelCost =
                double.Parse(
                    TempData["FuelCost"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelCostMin =
                double.Parse(
                    TempData["FuelCostMin"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelCostMax =
                double.Parse(
                    TempData["FuelCostMax"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelPricePerLiter =
                double.Parse(
                    TempData["FuelPricePerLiter"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            string fuelMethod =
                TempData["FuelMethod"]?
                    .ToString()
                ?? "ML Prediction";

            string fuelAssumptions =
                TempData["FuelAssumptions"]?
                    .ToString()
                ?? "";

            var confirmationViewModel =
                new UserReservationConfirmationViewModel
                {
                    VehicleId = vehicle.VehicleId,
                    StartDateTime = startDateTime,
                    EndDateTime = endDateTime,
                    Origin = origin,
                    Destination = destination,
                    Passengers = passengers,
                    Load = load,
                    Purpose = purpose,
                    Vehicle = vehicle,
                    Distance = route.Distance,
                    Duration = route.Duration,
                    RouteProvider = route.ProviderSnapshot,
                    EstimatedFuel = predictedFuel,
                    FuelMin = fuelMin,
                    FuelMax = fuelMax,
                    FuelCost = fuelCost,
                    FuelCostMin = fuelCostMin,
                    FuelCostMax = fuelCostMax,
                    FuelMethod = fuelMethod,
                    FuelAssumptions = fuelAssumptions,
                    FuelPricePerLiter = fuelPricePerLiter
                };

            TempData.Keep();

            return View(confirmationViewModel);
        }

        // ============================================================
        // POST: ConfirmReservation
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReservation()
        {
            if (TempData["VehicleId"] == null ||
                TempData["RouteEstimateId"] == null)
            {
                return BadRequest();
            }

            var currentUserEmail =
                User.Identity?.Name;

            var currentUser =
                await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Email == currentUserEmail);

            if (currentUser == null)
                return Unauthorized();

            int vehicleId =
                int.Parse(
                    TempData["VehicleId"]!
                        .ToString()!);

            int routeEstimateId =
                int.Parse(
                    TempData["RouteEstimateId"]!
                        .ToString()!);

            DateTime startDateTime =
                DateTime.Parse(
                    TempData["StartDateTime"]!
                        .ToString()!);

            DateTime endDateTime =
                DateTime.Parse(
                    TempData["EndDateTime"]!
                        .ToString()!);

            string origin =
                TempData["Origin"]?
                    .ToString() ?? "";

            string destination =
                TempData["Destination"]?
                    .ToString() ?? "";

            int passengers =
                int.Parse(
                    TempData["Passengers"]!
                        .ToString()!);

            double load =
                double.Parse(
                    TempData["Load"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            string purpose =
                TempData["Purpose"]?
                    .ToString() ?? "";

            double predictedFuel =
                double.Parse(
                    TempData["PredictedFuel"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelPricePerLiter =
                double.Parse(
                    TempData["FuelPricePerLiter"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelCost =
                double.Parse(
                    TempData["FuelCost"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            string fuelMethod =
                TempData["FuelMethod"]?
                    .ToString()
                ?? "ML Prediction";

            double fuelDistance =
                double.Parse(
                    TempData["FuelDistance"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            string fuelVehicleType =
                TempData["FuelVehicleType"]?
                    .ToString()
                ?? "";

            double fuelNominal =
                double.Parse(
                    TempData["FuelNominal"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            double fuelDuration =
                double.Parse(
                    TempData["FuelDuration"]!
                        .ToString()!,
                    CultureInfo.InvariantCulture);

            // ========================================================
            // Final Vehicle Check
            // ========================================================

            var selectedVehicle =
                await _context.Vehicles
                    .FirstOrDefaultAsync(v =>
                        v.VehicleId == vehicleId &&
                        v.Status == "Available");

            if (selectedVehicle == null)
            {
                TempData.Clear();

                return RedirectToAction(
                    "Index",
                    "UserDashboard");
            }

            // ========================================================
            // Final Maintenance Check
            // ========================================================

            var vehicleUnderMaintenance =
                await IsVehicleUnderMaintenance(
                    vehicleId,
                    startDateTime,
                    endDateTime);

            if (vehicleUnderMaintenance)
            {
                TempData.Clear();

                TempData["ReservationError"] =
                    "This vehicle is under maintenance during the selected trip period.";

                return RedirectToAction(
                    "Index",
                    "UserDashboard");
            }

            // ========================================================
            // Final Overlap Check
            // ========================================================

            var overlappingReservation =
                await HasOverlappingReservation(
                    vehicleId,
                    startDateTime,
                    endDateTime);

            if (overlappingReservation)
            {
                TempData.Clear();

                TempData["ReservationError"] =
                    "This vehicle has already been reserved during the selected time period.";

                return RedirectToAction(
                    "Index",
                    "UserDashboard");
            }

            // ========================================================
            // Create Reservation
            // ========================================================

            var reservation = new Reservation
            {
                VehicleId = vehicleId,
                StartDateTime = startDateTime,
                EndDateTime = endDateTime,
                Origin = origin,
                Destination = destination,
                Passengers = passengers,
                Load = load,
                Purpose = purpose,
                UserId = currentUser.UserId,
                Status = "Pending"
            };

            _context.Reservations.Add(reservation);

            await _context.SaveChangesAsync();

            // ========================================================
            // Create Fuel Estimate
            // ========================================================

            var fuelEstimate = new FuelEstimate
            {
                PredictedFuel = predictedFuel,
                FuelPricePerLiter = (decimal)fuelPricePerLiter,
                EstimatedCost = (decimal)fuelCost,
                Model = fuelMethod,

                Features =
                    $"Distance_km={fuelDistance}; " +
                    $"Vehicle_Type={fuelVehicleType}; " +
                    $"Passengers={passengers}; " +
                    $"Nominal_L_per_100km={fuelNominal}; " +
                    $"Duration_min={fuelDuration}",

                ReservationId =
                    reservation.ReservationId
            };

            _context.FuelEstimates.Add(fuelEstimate);

            await _context.SaveChangesAsync();

            TempData.Clear();

            return RedirectToAction(
                "Index",
                "UserDashboard");
        }

        // ============================================================
        // GET: MyReservations
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> MyReservations()
        {
            var currentUserEmail = User.Identity?.Name;

            if (string.IsNullOrEmpty(currentUserEmail))
                return Unauthorized();

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == currentUserEmail);

            if (currentUser == null)
                return Unauthorized();

            var reservations = await _context.Reservations
                .Where(r =>
                    r.UserId == currentUser.UserId)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleSpecification)
                .Include(r => r.FuelEstimate)
                .OrderByDescending(r => r.StartDateTime)
                .ToListAsync();

            var viewModel = reservations
                .Select(r => new MyReservationViewModel
                {
                    ReservationId = r.ReservationId,
                    StartDateTime = r.StartDateTime,
                    EndDateTime = r.EndDateTime,
                    Origin = r.Origin,
                    Destination = r.Destination,
                    Passengers = r.Passengers,
                    Load = r.Load,
                    Purpose = r.Purpose,
                    Status = r.Status,
                    Vehicle = r.Vehicle,
                    PredictedFuel = r.FuelEstimate?.PredictedFuel,
                    FuelPricePerLiter = r.FuelEstimate?.FuelPricePerLiter,
                    EstimatedCost = r.FuelEstimate?.EstimatedCost
                })
                .ToList();

            return View(viewModel);
        }

        // ============================================================
        // Cancel Confirmation
        // ============================================================

        [HttpGet]
        public IActionResult Cancel()
        {
            TempData.Clear();

            return RedirectToAction(
                "Index",
                "UserDashboard");
        }

        // ============================================================
        // Helper: Load Create Data
        // ============================================================

        private async Task LoadCreateData(int? vehicleId)
        {
            if (vehicleId == null)
                return;

            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == vehicleId);

            var routes = await _context.RouteEstimates
                .OrderBy(r => r.Origin)
                .ThenBy(r => r.Destination)
                .ToListAsync();

            ViewBag.Vehicle = vehicle;

            ViewBag.Origins = routes
                .Select(r => r.Origin)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            ViewBag.Routes = routes;
        }

        // ============================================================
        // Helper: Maintenance
        // ============================================================

        private async Task<bool> IsVehicleUnderMaintenance(
            int vehicleId,
            DateTime startDateTime,
            DateTime endDateTime)
        {
            return await _context.Set<VehicleMaintenance>()
                .AnyAsync(m =>
                    m.VehicleId == vehicleId &&
                    (
                        m.Status == "Scheduled" ||
                        m.Status == "In Progress"
                    ) &&
                    m.ServiceDate <= endDateTime &&
                    (
                        m.NextServiceDate == null ||
                        m.NextServiceDate >= startDateTime
                    )
                );
        }

        // ============================================================
        // Helper: Reservation Overlap
        // ============================================================

        private async Task<bool> HasOverlappingReservation(
            int vehicleId,
            DateTime startDateTime,
            DateTime endDateTime)
        {
            return await _context.Reservations
                .AnyAsync(r =>
                    r.VehicleId == vehicleId &&
                    (
                        r.Status == "Pending" ||
                        r.Status == "Approved"
                    ) &&
                    r.StartDateTime < endDateTime &&
                    r.EndDateTime > startDateTime
                );
        }
        // ============================================================
        // GET: UserReservation/GetVehicleAvailability
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> GetVehicleAvailability(int vehicleId)
        {
            var vehicleExists = await _context.Vehicles
                .AnyAsync(v => v.VehicleId == vehicleId);

            if (!vehicleExists)
                return NotFound();

            var reservations = await _context.Reservations
                .Where(r =>
                    r.VehicleId == vehicleId &&
                    (
                        r.Status == "Pending" ||
                        r.Status == "Approved"
                    ))
                .Select(r => new
                {
                    r.ReservationId,
                    r.StartDateTime,
                    r.EndDateTime,
                    r.Status
                })
                .ToListAsync();

            var firstDate = DateTime.Today.AddMonths(-1);
            var lastDate = DateTime.Today.AddMonths(12);

            var result = new List<object>();

            for (
                var date = firstDate;
                date <= lastDate;
                date = date.AddDays(1))
            {
                var dayReservations = reservations
                    .Where(r =>
                        r.StartDateTime.Date <= date.Date &&
                        r.EndDateTime.Date >= date.Date)
                    .Select(r => new
                    {
                        id = r.ReservationId,
                        start = r.StartDateTime,
                        end = r.EndDateTime,
                        status = r.Status
                    })
                    .ToList();

                string dayStatus = "available";

                if (dayReservations.Any())
                {
                    var hasApproved =
                        dayReservations.Any(r =>
                            r.status == "Approved");

                    var hasPending =
                        dayReservations.Any(r =>
                            r.status == "Pending");

                    /*
                     * Priority:
                     * Approved > Pending > Available
                     */

                    if (hasApproved)
                    {
                        dayStatus = "approved";
                    }
                    else if (hasPending)
                    {
                        dayStatus = "pending";
                    }
                }

                result.Add(new
                {
                    date = date.ToString("yyyy-MM-dd"),
                    status = dayStatus,
                    reservations = dayReservations
                });
            }

            return Json(result);
        }
    }
}