using BUA_project.Models;
using BUA_project.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "User")]
    public class UserTripController : Controller
    {
        private readonly Entity _context;

        public UserTripController(Entity context)
        {
            _context = context;
        }


        // ============================================
        // GET: UserTrip/ActiveTrip
        // ============================================

        [HttpGet]
        public async Task<IActionResult> ActiveTrip()
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
            // Get Active Trip
            // ============================================

            var trip = await _context.Trips

                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Vehicle)

                .Include(t => t.Reservation)
                    .ThenInclude(r => r.Driver)

                .Include(t => t.LocationPings)

                .Where(t =>
                    t.Reservation.UserId == currentUser.UserId &&
                    t.Status == "Active")

                .OrderByDescending(t => t.StartedAt)

                .FirstOrDefaultAsync();


            // ============================================
            // No Active Trip
            // ============================================

            if (trip == null)
            {
                return View(null);
            }


            // ============================================
            // Get Latest Location
            // ============================================

            var latestLocation = trip.LocationPings
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefault();


            // ============================================
            // Build ViewModel
            // ============================================

            var viewModel = new ActiveTripViewModel
            {
                // ========================================
                // Trip Information
                // ========================================

                TripId =
                    trip.TripId,

                StartedAt =
                    trip.StartedAt,

                Status =
                    trip.Status,

                ActualDistance =
                    trip.ActualDistanceKm,

                ActualFuel =
                    trip.ActualFuelLiters,

                ActualCost =
                    (double)trip.ActualFuelCost,


                StartOdometer =
                    trip.StartOdometer,

                EndOdometer =
                    trip.EndOdometer,

                IncidentNotes =
                    trip.IncidentNotes,


                // ========================================
                // Reservation Information
                // ========================================

                ReservationId =
                    trip.Reservation.ReservationId,

                Origin =
                    trip.Reservation.Origin,

                Destination =
                    trip.Reservation.Destination,

                StartDateTime =
                    trip.Reservation.StartDateTime,

                EndDateTime =
                    trip.Reservation.EndDateTime,

                Passengers =
                    trip.Reservation.Passengers,

                Load =
                    trip.Reservation.Load,

                Purpose =
                    trip.Reservation.Purpose,


                // ========================================
                // Vehicle Information
                // ========================================

                VehicleId =
                    trip.Reservation.VehicleId,

                VehicleBrand =
                    trip.Reservation.Vehicle.Brand,

                VehicleModel =
                    trip.Reservation.Vehicle.Model,

                VehicleType =
                    trip.Reservation.Vehicle.Type,

                PlateNumber =
                    trip.Reservation.Vehicle.PlateNumber,


                // ========================================
                // Driver Information
                // ========================================

                DriverId =
                    trip.Reservation.DriverId,

                DriverName =
                    trip.Reservation.Driver?.Name,

                DriverLicenseNumber =
                    trip.Reservation.Driver?.LicenseNumber,


                // ========================================
                // Current Location
                // ========================================

                CurrentLatitude =
                    latestLocation?.Latitude,

                CurrentLongitude =
                    latestLocation?.Longitude,

                LastLocationTime =
                    latestLocation?.Timestamp
            };


            // ============================================
            // Return View
            // ============================================

            return View(viewModel);
        }
    }
}