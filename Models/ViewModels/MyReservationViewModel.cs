using BUA_project.Models;

namespace BUA_project.Models.ViewModels
{
    public class MyReservationViewModel
    {
        public int ReservationId { get; set; }

        // Reservation Information
        public DateTime StartDateTime { get; set; }

        public DateTime EndDateTime { get; set; }

        public string Origin { get; set; } = "";

        public string Destination { get; set; } = "";

        public int Passengers { get; set; }

        public double Load { get; set; }

        public string Purpose { get; set; } = "";

        public string Status { get; set; } = "";


        // Vehicle Information
        public Vehicle? Vehicle { get; set; }


        // Fuel Information
        public double? PredictedFuel { get; set; }

        public decimal? FuelPricePerLiter { get; set; }

        public decimal? EstimatedCost { get; set; }
    }
}