using BUA_project.DTOs;
using BUA_project.Models;
using BUA_project.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    public class FuelEstimateController : Controller
    {
        private readonly Entity _context;
        private readonly FuelPredictionService _fuelPredictionService;

        public FuelEstimateController(
            Entity context,
            FuelPredictionService fuelPredictionService)
        {
            _context = context;
            _fuelPredictionService = fuelPredictionService;
        }

        // GET: /FuelEstimate
        public async Task<IActionResult> Index()
        {
            var fuelEstimates = await _context.FuelEstimates
                .Include(f => f.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .ToListAsync();

            return View(fuelEstimates);
        }

        // GET: /FuelEstimate/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var fuelEstimate = await _context.FuelEstimates
                .Include(f => f.Reservation)
                    .ThenInclude(r => r.Vehicle)
                .FirstOrDefaultAsync(
                    f => f.FuelEstimateId == id);

            if (fuelEstimate == null)
                return NotFound();

            return View(fuelEstimate);
        }

        // GET: /FuelEstimate/Predict?reservationId=5
        [HttpGet]
        public async Task<IActionResult> Predict(int reservationId)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleSpecification)

                .Include(r => r.Trip)
                    .ThenInclude(t => t.RouteEstimate)

                .FirstOrDefaultAsync(
                    r => r.ReservationId == reservationId);

            if (reservation == null)
                return NotFound();

            return View(reservation);
        }

        // POST: /FuelEstimate/PredictFuel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PredictFuel(int reservationId)
        {
            // 1. Get Reservation + required data
            var reservation = await _context.Reservations
                .Include(r => r.Vehicle)
                    .ThenInclude(v => v.VehicleSpecification)

                .Include(r => r.Trip)
                    .ThenInclude(t => t.RouteEstimate)

                .FirstOrDefaultAsync(
                    r => r.ReservationId == reservationId);

            if (reservation == null)
                return NotFound();

            var existingEstimate = await _context.FuelEstimates
            .FirstOrDefaultAsync(f =>
            f.ReservationId == reservationId);

            if (existingEstimate != null)
            {
                return RedirectToAction(
                    nameof(Details),
                    new { id = existingEstimate.FuelEstimateId });
            }

            // 2. Check Vehicle
            if (reservation.Vehicle == null)
            {
                TempData["Error"] =
                    "Vehicle information is not available.";

                return RedirectToAction(
                    "Details",
                    "Reservation",
                    new { id = reservationId });
            }


            // 3. Check Vehicle Specification
            if (reservation.Vehicle.VehicleSpecification == null)
            {
                TempData["Error"] =
                    "Vehicle specification is not available.";

                return RedirectToAction(
                    "Details",
                    "Reservation",
                    new { id = reservationId });
            }


            // 4. Check Trip
            if (reservation.Trip == null)
            {
                TempData["Error"] =
                    "Trip information is not available.";

                return RedirectToAction(
                    "Details",
                    "Reservation",
                    new { id = reservationId });
            }


            // 5. Check Route Estimate
            if (reservation.Trip.RouteEstimate == null)
            {
                TempData["Error"] =
                    "Route estimate is not available.";

                return RedirectToAction(
                    "Details",
                    "Reservation",
                    new { id = reservationId });
            }


            // 6. Get applicable fuel price
            // Select the latest price for the vehicle's fuel type
            // that was effective on or before the reservation start date.
            var fuelPrice = await _context.FuelPrices
                .Where(f =>
                    f.FuelType == reservation.Vehicle.FuelType &&
                    f.EffectiveDate <= reservation.StartDateTime)
                .OrderByDescending(f => f.EffectiveDate)
                .FirstOrDefaultAsync();


            // 7. Prepare request for ML API
            var request = new FuelPredictionRequest
            {
                Distance_km =
                    (float)reservation.Trip
                        .RouteEstimate.Distance,

                Vehicle_Type =
                    reservation.Vehicle.Type,

                Passengers =
                    reservation.Passengers,

                Nominal_L_per_100km =
                    (float)reservation.Vehicle
                        .VehicleSpecification
                        .NominalLPer100Km,

                Duration_min =
                    (float)reservation.Trip
                        .RouteEstimate.Duration
                        .TotalMinutes
            };


            // 8. Call ML API
            var result = await _fuelPredictionService
                .PredictAsync(request);

            if (result == null)
            {
                TempData["Error"] =
                    "Fuel prediction API failed.";

                return RedirectToAction(
                    "Details",
                    "Reservation",
                    new { id = reservationId });
            }


            // 9. Calculate estimated cost
            decimal estimatedCost =
                (decimal)result.predicted_fuel
                * fuelPrice.PricePerLiter;


            // 10. Create FuelEstimate
            var fuelEstimate = new FuelEstimate
            {
                PredictedFuel =
                    result.predicted_fuel,

                FuelPricePerLiter =
                    fuelPrice.PricePerLiter,

                EstimatedCost =
                    estimatedCost,

                Model =
                    "ML API",

                Features =
                    "Distance_km, Vehicle_Type, Passengers, " +
                    "Nominal_L_per_100km, Duration_min",

                ReservationId =
                    reservation.ReservationId
            };


            // 11. Save to database
            _context.FuelEstimates.Add(fuelEstimate);

            await _context.SaveChangesAsync();


            // 12. Show result
            return RedirectToAction(
                nameof(Details),
                new { id = fuelEstimate.FuelEstimateId });
        }
    }
}