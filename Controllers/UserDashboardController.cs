using BUA_project.Models;
using BUA_project.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace BUA_project.Controllers
{
    public class UserDashboardController : Controller
    {
        private readonly Entity _context = new Entity();

        // GET: UserDashboard/Index
        public async Task<IActionResult> Index(
            string? type,
            string? brand,
            int? seats,
            string? fuelType,
            string? transmission,
            string? accessibility)
        {
            // Get available vehicles
            var now = DateTime.Now;

            var vehiclesQuery = _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .Where(v => v.Status == "Available")
                .Where(v =>
                    !_context.Set<VehicleMaintenance>()
                        .Any(m =>
                            m.VehicleId == v.VehicleId &&

                            (
                                m.Status == "Scheduled" ||
                                m.Status == "In Progress"
                            ) &&

                            m.ServiceDate <= now &&

                            (
                                m.NextServiceDate == null ||
                                m.NextServiceDate >= now
                            )
                        )
                )
                .AsQueryable();

            // Filter by Type
            if (!string.IsNullOrEmpty(type))
            {
                vehiclesQuery = vehiclesQuery
                    .Where(v => v.Type == type);
            }

            // Filter by Brand
            if (!string.IsNullOrEmpty(brand))
            {
                vehiclesQuery = vehiclesQuery
                    .Where(v => v.Brand == brand);
            }

            // Filter by Seats
            if (seats.HasValue)
            {
                vehiclesQuery = vehiclesQuery
                    .Where(v => v.Seats == seats.Value);
            }

            // Filter by Fuel Type
            if (!string.IsNullOrEmpty(fuelType))
            {
                vehiclesQuery = vehiclesQuery
                    .Where(v => v.FuelType == fuelType);
            }

            // Filter by Transmission
            if (!string.IsNullOrEmpty(transmission))
            {
                vehiclesQuery = vehiclesQuery
                    .Where(v =>
                        v.VehicleSpecification != null &&
                        v.VehicleSpecification.Transmission == transmission);
            }

            // Filter by Accessibility
            if (!string.IsNullOrEmpty(accessibility))
            {
                vehiclesQuery = vehiclesQuery
                    .Where(v =>
                        v.VehicleSpecification != null &&
                        v.VehicleSpecification.Accessibility == accessibility);
            }


            // Get current logged-in user
            var currentUserEmail = User.Identity?.Name;

            var currentUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == currentUserEmail);

            if (currentUser == null)
                return Unauthorized();


            // Get current user's reservations
            var myReservations = _context.Reservations
                .Where(r => r.UserId == currentUser.UserId);


            // =========================
            // Dashboard Metrics
            // =========================

            // Available vehicles
            

            var availableVehiclesCount =
                await _context.Vehicles
                    .CountAsync(v =>
                        v.Status == "Available" &&

                        !_context.Set<VehicleMaintenance>()
                            .Any(m =>
                                m.VehicleId == v.VehicleId &&

                                (
                                    m.Status == "Scheduled" ||
                                    m.Status == "In Progress"
                                ) &&

                                m.ServiceDate <= now &&

                                (
                                    m.NextServiceDate == null ||
                                    m.NextServiceDate >= now
                                )
                            )
                    );


            // Pending or Approved requests
            var myActiveRequestsCount = await myReservations
                .CountAsync(r =>
                    r.Status == "Pending" ||
                    r.Status == "Approved");


            // Approved upcoming trips
            var approvedUpcomingTripsCount = await myReservations
                .CountAsync(r =>
                    r.Status == "Approved" &&
                    r.StartDateTime > DateTime.Now);


            // Completed trips
            var completedTripsCount = await myReservations
                .CountAsync(r =>
                    r.Status == "Completed");


            // =========================
            // Build ViewModel
            // =========================

            var viewModel = new UserDashboardViewModel
            {
                AvailableVehiclesCount = availableVehiclesCount,

                MyActiveRequestsCount = myActiveRequestsCount,

                ApprovedUpcomingTripsCount =
                    approvedUpcomingTripsCount,

                CompletedTripsCount =
                    completedTripsCount,

                Vehicles = await vehiclesQuery.ToListAsync()
            };


            return View(viewModel);
        }
    }
}
