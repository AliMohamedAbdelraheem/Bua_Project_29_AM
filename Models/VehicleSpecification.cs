using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace BUA_project.Models
{
    public class VehicleSpecification
    {
        public int VehicleSpecificationId { get; set; }

        public double NominalLPer100Km { get; set; }

        public double TankCapacity { get; set; }

        public string Accessibility { get; set; }

        public string Transmission { get; set; }

        public double AllowedLoad { get; set; }


        [ValidateNever]
        public ICollection<Vehicle> Vehicles { get; set; }
            = new List<Vehicle>();
    }
}