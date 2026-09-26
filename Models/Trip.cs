namespace BUA_project.Models
{
    public class Trip
    {
        public int TripId { get; set; }

        public DateTime StartedAt { get; set; }

        public DateTime? CompletedAt { get; set; }

        public double ActualDistanceKm { get; set; }

        public double ActualFuelLiters { get; set; }

        public decimal ActualFuelCost { get; set; }

        public double StartOdometer { get; set; }

        public double EndOdometer { get; set; }

        public string? IncidentNotes { get; set; }

        public string Status { get; set; }

        public decimal FuelPricePerLiter { get; set; }


        // Foreign Keys + Navigations

        public int ReservationId { get; set; }

        public Reservation Reservation { get; set; }


        public int? RouteEstimateId { get; set; }

        public RouteEstimate? RouteEstimate { get; set; }


        public ICollection<LocationPing> LocationPings { get; set; }
            = new List<LocationPing>();
    }
}