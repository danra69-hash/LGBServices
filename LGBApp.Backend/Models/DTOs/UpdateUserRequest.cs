using System.ComponentModel.DataAnnotations;

namespace LGBApp.Backend.Models.DTOs;

public class UpdateUserRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    public string Mobile { get; set; } = string.Empty;

    public string Role { get; set; } = "User";
    public string JobTitle { get; set; } = string.Empty;
    public bool CanRecommendMoi { get; set; }
    public int? CustomerId { get; set; }
}
