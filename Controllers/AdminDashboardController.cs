using BUA_project.Models;
using BUA_project.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly Entity _context = new Entity();

        // GET: AdminDashboard/Index
        public async Task<IActionResult> Index()
        {
            var viewModel = new AdminDashboardViewModel
            {
                // Vehicles
                TotalVehicles = await _context.Vehicles
                    .CountAsync(),

                AvailableVehicles = await _context.Vehicles
                    .CountAsync(v => v.Status == "Available"),

                // Reservations
                TotalReservations = await _context.Reservations
                    .CountAsync(),

                PendingReservations = await _context.Reservations
                    .CountAsync(r => r.Status == "Pending"),

                ApprovedReservations = await _context.Reservations
                    .CountAsync(r => r.Status == "Approved"),

                // Users
                TotalUsers = await _context.Users
                    .CountAsync(),

                // Drivers
                TotalDrivers = await _context.Drivers
                    .CountAsync()
            };

            return View("~/Views/Admin/Dashboard/Index.cshtml", viewModel);
        }
    }
}