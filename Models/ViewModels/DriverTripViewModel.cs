namespace BUA_project.Models.ViewModels
{
    public class DriverTripViewModel
    {
        public int TripId { get; set; }

        public int ReservationId { get; set; }

        public DateTime StartDateTime { get; set; }

        public DateTime EndDateTime { get; set; }

        public string Origin { get; set; }

        public string Destination { get; set; }

        public int Passengers { get; set; }

        public double Load { get; set; }

        public string Purpose { get; set; }

        public string Status { get; set; }

        public int VehicleId { get; set; }

        public string VehicleName { get; set; }
    }
}