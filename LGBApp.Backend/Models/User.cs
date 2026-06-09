namespace LGBApp.Backend.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Email { get; set; } = string.Empty;

        // This maps to 'password' from the frontend
        public string PasswordHash { get; set; } = string.Empty;

        // Brand new additions to match your Figma elements:
        public string Name { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string Role { get; set; } = "User";

        public bool IsVerified { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}