using System.Text.Json.Serialization;

namespace BUA_project.DTOs
{
    public class FuelPredictionRequest
    {
        [JsonPropertyName("Distance_km")]
        public float Distance_km { get; set; }

        [JsonPropertyName("Vehicle_Type")]
        public string Vehicle_Type { get; set; }

        [JsonPropertyName("Passengers")]
        public int Passengers { get; set; }

        [JsonPropertyName("Nominal_L_per_100km")]
        public float Nominal_L_per_100km { get; set; }

        [JsonPropertyName("Duration_min")]
        public float Duration_min { get; set; }
    }
}