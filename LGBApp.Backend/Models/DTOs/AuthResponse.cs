namespace LGBApp.Backend.Models.DTOs;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public UserResponse User { get; set; } = new();
}
