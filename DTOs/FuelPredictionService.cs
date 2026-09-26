using System.Net.Http.Json;
using BUA_project.DTOs;

namespace BUA_project.Services
{
    public class FuelPredictionService
    {
        private readonly HttpClient _httpClient;

        public FuelPredictionService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<FuelPredictionResponse?> PredictAsync(
            FuelPredictionRequest request)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "predict",
                    request);

                var responseBody =
                    await response.Content.ReadAsStringAsync();

                Console.WriteLine("================================");
                Console.WriteLine("Fuel API Status: " + response.StatusCode);
                Console.WriteLine("Fuel API Response:");
                Console.WriteLine(responseBody);
                Console.WriteLine("================================");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                return await response.Content
                    .ReadFromJsonAsync<FuelPredictionResponse>();
            }
            catch (Exception ex)
            {
                Console.WriteLine("================================");
                Console.WriteLine("Fuel API ERROR:");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("================================");

                return null;
            }
        }
    }
}