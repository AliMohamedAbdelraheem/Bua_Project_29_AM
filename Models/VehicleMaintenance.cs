namespace BUA_project.Models
{
    public class VehicleMaintenance
    {
        public int VehicleMaintenanceId { get; set; }

        public int VehicleId { get; set; }
        public Vehicle Vehicle { get; set; }

        public string MaintenanceType { get; set; }
        public string Description { get; set; }

        public DateTime ServiceDate { get; set; }
        public DateTime? NextServiceDate { get; set; }

        public decimal Cost { get; set; }

        public string Status { get; set; }

        public int? CreatedByUserId { get; set; }
        public User? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}