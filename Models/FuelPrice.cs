namespace BUA_project.Models
{
    public class FuelPrice
    {
        public int FuelPriceId { get; set; }

        // Example: Petrol 92, Petrol 95, Diesel
        public string FuelType { get; set; }

        public decimal PricePerLiter { get; set; }

        public DateTime EffectiveDate { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? CreatedByUserId { get; set; }

        public User? CreatedByUser { get; set; }
    }
}