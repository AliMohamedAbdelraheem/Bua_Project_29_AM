namespace BUA_project.Models.ViewModels
{
    public class ActiveTripViewModel
    {
        // Trip
        public int TripId { get; set; }

        public DateTime? StartedAt { get; set; }

        public string Status { get; set; } = "";

        public double? ActualDistance { get; set; }

        public double? ActualFuel { get; set; }

        public double? ActualCost { get; set; }

        public double? StartOdometer { get; set; }

        public double? EndOdometer { get; set; }

        public string? IncidentNotes { get; set; }


        // Reservation
        public int ReservationId { get; set; }

        public string Origin { get; set; } = "";

        public string Destination { get; set; } = "";

        public DateTime StartDateTime { get; set; }

        public DateTime EndDateTime { get; set; }

        public int Passengers { get; set; }

        public double Load { get; set; }

        public string Purpose { get; set; } = "";


        // Vehicle
        public int VehicleId { get; set; }

        public string VehicleBrand { get; set; } = "";

        public string VehicleModel { get; set; } = "";

        public string VehicleType { get; set; } = "";

        public string PlateNumber { get; set; } = "";


        // Driver
        public int? DriverId { get; set; }

        public string? DriverName { get; set; }

        public string? DriverLicenseNumber { get; set; }


        // Current Location
        public double? CurrentLatitude { get; set; }

        public double? CurrentLongitude { get; set; }

        public DateTime? LastLocationTime { get; set; }
    }
}