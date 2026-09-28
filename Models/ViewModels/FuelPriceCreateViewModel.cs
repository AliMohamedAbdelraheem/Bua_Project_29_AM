namespace BUA_project.Models.ViewModels
{
	public class FuelPriceCreateViewModel
	{
		public string FuelType { get; set; } = string.Empty;

		public decimal PricePerLiter { get; set; }

		public DateTime EffectiveDate { get; set; }

		public List<string> FuelTypes { get; set; } = new();
	}
}