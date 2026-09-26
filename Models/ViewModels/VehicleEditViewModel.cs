using Microsoft.AspNetCore.Http;

namespace BUA_project.Models.ViewModels
{
    public class VehicleEditViewModel
    {
        public int VehicleId { get; set; }

        public string Type { get; set; }

        public string Brand { get; set; }

        public string Model { get; set; }

        public int Seats { get; set; }

        public string FuelType { get; set; }

        public string PlateNumber { get; set; }

        public string Status { get; set; }

        public int Year { get; set; }

        public int VehicleSpecificationId { get; set; }

        public string? ExistingImageUrl { get; set; }

        public IFormFile? Image { get; set; }
    }
}