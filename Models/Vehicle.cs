namespace BUA_project.Models
{
    public class Vehicle
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
        public VehicleSpecification VehicleSpecification { get; set; }

        public ICollection<Reservation> Reservations { get; set; }
            = new List<Reservation>();

        public ICollection<VehicleMaintenance> Maintenances { get; set; }
            = new List<VehicleMaintenance>();
    }
}