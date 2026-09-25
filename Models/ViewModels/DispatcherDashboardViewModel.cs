using BUA_project.Models;

namespace BUA_project.Models.ViewModels
{
    public class DispatcherDashboardViewModel
    {
        public int PendingRequestsCount { get; set; }

        public int AvailableVehiclesCount { get; set; }

        public int AvailableDriversCount { get; set; }

        public int ActiveTripsCount { get; set; }

        public List<Reservation> RecentPendingRequests { get; set; }
            = new List<Reservation>();
    }
}