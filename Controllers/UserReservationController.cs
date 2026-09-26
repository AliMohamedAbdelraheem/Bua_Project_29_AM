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
                ModelState.AddModelError(
                    "",
                    "This vehicle is no longer available.");

                await LoadCreateData(model.VehicleId);

                return View(model);
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

            // --------------------------------------------------------
            // Get Fuel Price
            // --------------------------------------------------------

            var fuelPrice = await _context.FuelPrices
                .Where(f =>
                    f.EffectiveDate <= model.StartDateTime)
                .OrderByDescending(f => f.EffectiveDate)
                .FirstOrDefaultAsync();

            if (fuelPrice == null)
            {
                ModelState.AddModelError(
                    "",
                    "Fuel price has not been configured by the administrator.");

                await LoadCreateData(model.VehicleId);

                return View(model);
            }

            // ========================================================
            // Prepare ML Prediction Data
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

            // --------------------------------------------------------
            // Create ML Request
            // --------------------------------------------------------

            var fuelRequest = new FuelPredictionRequest
            {
                Distance_km = fuelDistance,
                Vehicle_Type = fuelVehicleType,
                Passengers = model.Passengers,
                Nominal_L_per_100km = fuelNominal,
                Duration_min = fuelDuration
            };

            // ========================================================
            // Call ML API
            // ========================================================

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
            // Store Fuel Data In TempData
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

            // ========================================================
            // Go To Confirmation
            // ========================================================

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
            // --------------------------------------------------------
            // Check Required TempData
            // --------------------------------------------------------

            if (TempData["VehicleId"] == null ||
                TempData["RouteEstimateId"] == null)
            {
                return BadRequest();
            }

            // --------------------------------------------------------
            // Read IDs
            // --------------------------------------------------------

            int vehicleId =
                int.Parse(
                    TempData["VehicleId"]!
                        .ToString()!);

            int routeEstimateId =
                int.Parse(
                    TempData["RouteEstimateId"]!
                        .ToString()!);

            // --------------------------------------------------------
            // Get Vehicle
            // --------------------------------------------------------

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

            // --------------------------------------------------------
            // Get Route
            // --------------------------------------------------------

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

            // ========================================================
            // Read Reservation Data
            // ========================================================

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

            // ========================================================
            // Fuel Data
            // ========================================================

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

            // ========================================================
            // Build Confirmation ViewModel
            // ========================================================

            var confirmationViewModel =
                new UserReservationConfirmationViewModel
                {
                    VehicleId =
                        vehicle.VehicleId,

                    StartDateTime =
                        startDateTime,

                    EndDateTime =
                        endDateTime,

                    Origin =
                        origin,

                    Destination =
                        destination,

                    Passengers =
                        passengers,

                    Load =
                        load,

                    Purpose =
                        purpose,

                    Vehicle =
                        vehicle,

                    Distance =
                        route.Distance,

                    Duration =
                        route.Duration,

                    RouteProvider =
                        route.ProviderSnapshot,

                    EstimatedFuel =
                        predictedFuel,

                    FuelMin =
                        fuelMin,

                    FuelMax =
                        fuelMax,

                    FuelCost =
                        fuelCost,

                    FuelCostMin =
                        fuelCostMin,

                    FuelCostMax =
                        fuelCostMax,

                    FuelMethod =
                        fuelMethod,

                    FuelAssumptions =
                        fuelAssumptions,

                    FuelPricePerLiter =
                        fuelPricePerLiter
                };

            // --------------------------------------------------------
            // Keep TempData For POST ConfirmReservation
            // --------------------------------------------------------

            TempData.Keep();

            return View(confirmationViewModel);
        }

        // ============================================================
        // POST: UserReservation/ConfirmReservation
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReservation()
        {
            // --------------------------------------------------------
            // Check TempData
            // --------------------------------------------------------

            if (TempData["VehicleId"] == null ||
                TempData["RouteEstimateId"] == null)
            {
                return BadRequest();
            }

            // --------------------------------------------------------
            // Current User
            // --------------------------------------------------------

            var currentUserEmail =
                User.Identity?.Name;

            var currentUser =
                await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Email == currentUserEmail);

            if (currentUser == null)
                return Unauthorized();

            // ========================================================
            // Reservation Data
            // ========================================================

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

            // ========================================================
            // Fuel Data
            // ========================================================

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

            // ========================================================
            // Fuel Features
            // ========================================================

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
            // Check Vehicle Again
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
            // Create Reservation
            // ========================================================

            var reservation = new Reservation
            {
                VehicleId =
                    vehicleId,

                StartDateTime =
                    startDateTime,

                EndDateTime =
                    endDateTime,

                Origin =
                    origin,

                Destination =
                    destination,

                Passengers =
                    passengers,

                Load =
                    load,

                Purpose =
                    purpose,

                UserId =
                    currentUser.UserId,

                Status =
                    "Pending"
            };

            // ========================================================
            // Save Reservation
            // ========================================================

            _context.Reservations.Add(reservation);

            await _context.SaveChangesAsync();

            // ========================================================
            // Create Fuel Estimate
            // ========================================================

            var fuelEstimate = new FuelEstimate
            {
                PredictedFuel =
                    predictedFuel,

                FuelPricePerLiter =
                    (decimal)fuelPricePerLiter,

                EstimatedCost =
                    (decimal)fuelCost,

                Model =
                    fuelMethod,

                Features =
                    $"Distance_km={fuelDistance}; " +
                    $"Vehicle_Type={fuelVehicleType}; " +
                    $"Passengers={passengers}; " +
                    $"Nominal_L_per_100km={fuelNominal}; " +
                    $"Duration_min={fuelDuration}",

                ReservationId =
                    reservation.ReservationId
            };

            // ========================================================
            // Save Fuel Estimate
            // ========================================================

            _context.FuelEstimates.Add(fuelEstimate);

            await _context.SaveChangesAsync();

            // ========================================================
            // Clear TempData
            // ========================================================

            TempData.Clear();

            // ========================================================
            // Return Dashboard
            // ========================================================

            return RedirectToAction(
                "Index",
                "UserDashboard");
        }

        // ============================================================
        // GET: UserReservation/MyReservations
        // ============================================================

        [HttpGet]
        public async Task<IActionResult> MyReservations()
        {
            // ============================================
            // Get Current User
            // ============================================

            var currentUserEmail = User.Identity?.Name;

            if (string.IsNullOrEmpty(currentUserEmail))
                return Unauthorized();

            // ============================================
            // Get Current User From Database
            // ============================================

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == currentUserEmail);

            if (currentUser == null)
                return Unauthorized();

            // ============================================
            // Get User Reservations
            // ============================================

            var reservations = await _context.Reservations
                .Where(r =>
                    r.UserId == currentUser.UserId)
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleSpecification)
                .Include(r => r.FuelEstimate)
                .OrderByDescending(r => r.StartDateTime)
                .ToListAsync();

            // ============================================
            // Convert To ViewModel
            // ============================================

            var viewModel = reservations
                .Select(r => new MyReservationViewModel
                {
                    ReservationId =
                        r.ReservationId,

                    StartDateTime =
                        r.StartDateTime,

                    EndDateTime =
                        r.EndDateTime,

                    Origin =
                        r.Origin,

                    Destination =
                        r.Destination,

                    Passengers =
                        r.Passengers,

                    Load =
                        r.Load,

                    Purpose =
                        r.Purpose,

                    Status =
                        r.Status,

                    Vehicle =
                        r.Vehicle,

                    PredictedFuel =
                        r.FuelEstimate?.PredictedFuel,

                    FuelPricePerLiter =
                        r.FuelEstimate?.FuelPricePerLiter,

                    EstimatedCost =
                        r.FuelEstimate?.EstimatedCost
                })
                .ToList();

            // ============================================
            // Return View
            // ============================================

            return View(viewModel);
        }

        // ============================================================
        // GET: UserReservation/Cancel
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
        // Helper Method
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

            ViewBag.Vehicle =
                vehicle;

            ViewBag.Origins =
                routes
                    .Select(r => r.Origin)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();

            ViewBag.Routes =
                routes;
        }
    }
}