namespace BUA_project.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string Role { get; set; }

        // Relationships
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public Driver? Driver { get; set; }   // Is relationship (1:0..1)
    }
}