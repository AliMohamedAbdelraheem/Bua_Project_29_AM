using BUA_project.Models;
using BUA_project.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Controllers
{
    [Authorize(Roles = "Admin")]
    public class VehicleController : Controller
    {
        private readonly Entity _context;

        public VehicleController(Entity context)
        {
            _context = context;
        }


        // ============================================================
        // GET: Vehicle
        // ============================================================

        public async Task<IActionResult> Index()
        {
            var vehicles = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .OrderBy(v => v.PlateNumber)
                .ToListAsync();

            return View(vehicles);
        }


        // ============================================================
        // GET: Vehicle/Details/5
        // ============================================================

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
                return NotFound();

            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id);

            if (vehicle == null)
                return NotFound();

            return View(vehicle);
        }


        // ============================================================
        // GET: Vehicle/Create
        // ============================================================

        public async Task<IActionResult> Create()
        {
            await LoadVehicleSpecifications();

            return View();
        }


        // ============================================================
        // POST: Vehicle/Create
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            VehicleCreateViewModel model)
        {
            Console.WriteLine(
                "========== CREATE VEHICLE ==========");

            Console.WriteLine($"Type: {model.Type}");
            Console.WriteLine($"Brand: {model.Brand}");
            Console.WriteLine($"Model: {model.Model}");
            Console.WriteLine($"Seats: {model.Seats}");
            Console.WriteLine($"FuelType: {model.FuelType}");
            Console.WriteLine($"PlateNumber: {model.PlateNumber}");
            Console.WriteLine($"Status: {model.Status}");
            Console.WriteLine($"Year: {model.Year}");
            Console.WriteLine(
                $"SpecificationId: {model.VehicleSpecificationId}");

            Console.WriteLine(
                $"ModelState Valid: {ModelState.IsValid}");

            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine(
                        $"MODEL ERROR [{state.Key}]: {error.ErrorMessage}");

                    if (error.Exception != null)
                    {
                        Console.WriteLine(
                            $"EXCEPTION: {error.Exception.Message}");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                await LoadVehicleSpecifications();

                return View(model);
            }

            var specificationExists =
                await _context.VehicleSpecifications
                    .AnyAsync(vs =>
                        vs.VehicleSpecificationId ==
                        model.VehicleSpecificationId);

            Console.WriteLine(
                $"Specification Exists: {specificationExists}");

            if (!specificationExists)
            {
                ModelState.AddModelError(
                    "VehicleSpecificationId",
                    "Please select a valid vehicle specification.");

                await LoadVehicleSpecifications();

                return View(model);
            }


            // ========================================================
            // Create Vehicle
            // ========================================================

            var vehicle = new Vehicle
            {
                Type =
                    model.Type,

                Brand =
                    model.Brand,

                Model =
                    model.Model,

                Seats =
                    model.Seats,

                FuelType =
                    model.FuelType,

                PlateNumber =
                    model.PlateNumber,

                Status =
                    model.Status,

                Year =
                    model.Year,

                VehicleSpecificationId =
                    model.VehicleSpecificationId
            };


            // ========================================================
            // Upload Vehicle Image
            // ========================================================

            if (model.Image != null &&
                model.Image.Length > 0)
            {
                var uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    "vehicles"
                );

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(
                        uploadsFolder);
                }

                var fileName =
                    Guid.NewGuid().ToString()
                    + Path.GetExtension(
                        model.Image.FileName);

                var filePath =
                    Path.Combine(
                        uploadsFolder,
                        fileName);

                using (var stream =
                    new FileStream(
                        filePath,
                        FileMode.Create))
                {
                    await model.Image.CopyToAsync(stream);
                }

                vehicle.ImageUrl =
                    "/images/vehicles/" + fileName;
            }


            // ========================================================
            // Save Vehicle
            // ========================================================

            _context.Vehicles.Add(vehicle);

            Console.WriteLine(
                "Calling SaveChangesAsync...");

            await _context.SaveChangesAsync();

            Console.WriteLine(
                "Vehicle Created Successfully!");

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // GET: Vehicle/Edit/5
        // ============================================================

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
                return NotFound();

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id);

            if (vehicle == null)
                return NotFound();

            var model = new VehicleEditViewModel
            {
                VehicleId =
                    vehicle.VehicleId,

                Type =
                    vehicle.Type,

                Brand =
                    vehicle.Brand,

                Model =
                    vehicle.Model,

                Seats =
                    vehicle.Seats,

                FuelType =
                    vehicle.FuelType,

                PlateNumber =
                    vehicle.PlateNumber,

                Status =
                    vehicle.Status,

                Year =
                    vehicle.Year,

                VehicleSpecificationId =
                    vehicle.VehicleSpecificationId,

                ExistingImageUrl =
                    vehicle.ImageUrl
            };

            await LoadVehicleSpecifications();

            return View(model);
        }


        // ============================================================
        // POST: Vehicle/Edit/5
        // ============================================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            int id,
            VehicleEditViewModel model)
        {
            Console.WriteLine(
                "========== EDIT VEHICLE ==========");

            Console.WriteLine(
                $"Route ID: {id}");

            Console.WriteLine(
                $"Vehicle ID: {model.VehicleId}");

            Console.WriteLine(
                $"Type: {model.Type}");

            Console.WriteLine(
                $"Brand: {model.Brand}");

            Console.WriteLine(
                $"Model: {model.Model}");

            Console.WriteLine(
                $"Seats: {model.Seats}");

            Console.WriteLine(
                $"FuelType: {model.FuelType}");

            Console.WriteLine(
                $"PlateNumber: {model.PlateNumber}");

            Console.WriteLine(
                $"Status: {model.Status}");

            Console.WriteLine(
                $"Year: {model.Year}");

            Console.WriteLine(
                $"SpecificationId: {model.VehicleSpecificationId}");

            Console.WriteLine(
                $"ModelState Valid: {ModelState.IsValid}");

            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine(
                        $"MODEL ERROR [{state.Key}]: {error.ErrorMessage}");

                    if (error.Exception != null)
                    {
                        Console.WriteLine(
                            $"EXCEPTION: {error.Exception.Message}");
                    }
                }
            }

            if (id != model.VehicleId)
                return NotFound();

            if (!ModelState.IsValid)
            {
                await LoadVehicleSpecifications();

                return View(model);
            }

            var specificationExists =
                await _context.VehicleSpecifications
                    .AnyAsync(vs =>
                        vs.VehicleSpecificationId ==
                        model.VehicleSpecificationId);

            Console.WriteLine(
                $"Specification Exists: {specificationExists}");

            if (!specificationExists)
            {
                ModelState.AddModelError(
                    "VehicleSpecificationId",
                    "Please select a valid vehicle specification.");

                await LoadVehicleSpecifications();

                return View(model);
            }

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id);

            if (vehicle == null)
                return NotFound();


            // ========================================================
            // Update Vehicle Information
            // ========================================================

            vehicle.Type =
                model.Type;

            vehicle.Brand =
                model.Brand;

            vehicle.Model =
                model.Model;

            vehicle.Seats =
                model.Seats;

            vehicle.FuelType =
                model.FuelType;

            vehicle.PlateNumber =
                model.PlateNumber;

            vehicle.Status =
                model.Status;

            vehicle.Year =
                model.Year;

            vehicle.VehicleSpecificationId =
                model.VehicleSpecificationId;


            // ========================================================
            // Upload New Image
            // ========================================================

            if (model.Image != null &&
                model.Image.Length > 0)
            {
                var uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "images",
                    "vehicles"
                );

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(
                        uploadsFolder);
                }


                // ====================================================
                // Delete Old Image
                // ====================================================

                if (!string.IsNullOrEmpty(
                    vehicle.ImageUrl))
                {
                    var oldImagePath =
                        Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot",
                            vehicle.ImageUrl
                                .TrimStart('/')
                                .Replace(
                                    "/",
                                    Path.DirectorySeparatorChar
                                        .ToString()
                                )
                        );

                    if (System.IO.File.Exists(
                        oldImagePath))
                    {
                        System.IO.File.Delete(
                            oldImagePath);
                    }
                }


                // ====================================================
                // Save New Image
                // ====================================================

                var fileName =
                    Guid.NewGuid().ToString()
                    + Path.GetExtension(
                        model.Image.FileName);

                var filePath =
                    Path.Combine(
                        uploadsFolder,
                        fileName);

                using (var stream =
                    new FileStream(
                        filePath,
                        FileMode.Create))
                {
                    await model.Image.CopyToAsync(
                        stream);
                }

                vehicle.ImageUrl =
                    "/images/vehicles/" + fileName;
            }


            // ========================================================
            // Save Changes
            // ========================================================

            Console.WriteLine(
                "Calling SaveChangesAsync...");

            await _context.SaveChangesAsync();

            Console.WriteLine(
                "Vehicle Updated Successfully!");

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // GET: Vehicle/Delete/5
        // ============================================================

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
                return NotFound();

            var vehicle = await _context.Vehicles
                .Include(v => v.VehicleSpecification)
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id);

            if (vehicle == null)
                return NotFound();

            return View(vehicle);
        }


        // ============================================================
        // POST: Vehicle/Delete/5
        // ============================================================

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(
            int id)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v =>
                    v.VehicleId == id);

            if (vehicle == null)
                return NotFound();


            // ========================================================
            // Delete Vehicle Image
            // ========================================================

            if (!string.IsNullOrEmpty(
                vehicle.ImageUrl))
            {
                var imagePath =
                    Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        vehicle.ImageUrl
                            .TrimStart('/')
                            .Replace(
                                "/",
                                Path.DirectorySeparatorChar
                                    .ToString()
                            )
                    );

                if (System.IO.File.Exists(
                    imagePath))
                {
                    System.IO.File.Delete(
                        imagePath);
                }
            }


            // ========================================================
            // Delete Vehicle From Database
            // ========================================================

            _context.Vehicles.Remove(vehicle);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["DeleteError"] =
                    "This vehicle cannot be deleted because it is used in other records.";

                return RedirectToAction(
                    nameof(Delete),
                    new { id });
            }

            return RedirectToAction(nameof(Index));
        }


        // ============================================================
        // Load Vehicle Specifications
        // ============================================================

        private async Task LoadVehicleSpecifications()
        {
            ViewBag.VehicleSpecifications =
                await _context.VehicleSpecifications
                    .OrderBy(vs =>
                        vs.VehicleSpecificationId)
                    .ToListAsync();
        }


        // ============================================================
        // Vehicle Exists
        // ============================================================

        private async Task<bool> VehicleExists(int id)
        {
            return await _context.Vehicles
                .AnyAsync(v =>
                    v.VehicleId == id);
        }
    }
}