namespace BUA_project.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TotalVehicles { get; set; }

        public int AvailableVehicles { get; set; }

        public int TotalReservations { get; set; }

        public int PendingReservations { get; set; }

        public int ApprovedReservations { get; set; }

        public int TotalUsers { get; set; }

        public int TotalDrivers { get; set; }
    }
}