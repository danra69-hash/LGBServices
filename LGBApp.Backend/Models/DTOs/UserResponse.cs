namespace LGBApp.Backend.Models.DTOs;

public class UserResponse
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Mobile { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public bool CanRecommendMoi { get; set; }
    public bool CanApproveMoiIntake { get; set; }
    public bool CanApproveMoi { get; set; }
    public bool CanApproveMoa { get; set; }
    public bool IsInternalSignatory { get; set; }
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public bool NeedsMoi { get; set; }
    public bool NeedsMoiApproval { get; set; }
    public bool NeedsMoa { get; set; }
    public bool IsVerified { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SignatoryCompanyAccessDto>? AccessibleCompanies { get; set; }
}
