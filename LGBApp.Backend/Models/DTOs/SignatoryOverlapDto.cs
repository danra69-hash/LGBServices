namespace LGBApp.Backend.Models.DTOs;

public class SignatoryOverlapDto
{
    public string Email { get; set; } = string.Empty;
    public string PrimaryName { get; set; } = string.Empty;
    public int CompanyCount { get; set; }
    public bool IsLinked { get; set; }
    public int? LinkedUserId { get; set; }
    public List<SignatoryOverlapCompanyDto> Companies { get; set; } = [];
}

public class SignatoryOverlapCompanyDto
{
    public int CustomerId { get; set; }
    public string Company { get; set; } = string.Empty;
    public int AccountHolderId { get; set; }
    public string HolderName { get; set; } = string.Empty;
    public bool NeedsMoi { get; set; }
    public bool NeedsMoiApproval { get; set; }
    public bool NeedsMoa { get; set; }
    public int? UserId { get; set; }
}

public class SignatoryLinkResultDto
{
    public string Email { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int HoldersLinked { get; set; }
    public List<int> CustomerIds { get; set; } = [];
    public string Message { get; set; } = string.Empty;
}

public class SignatoryCompanyAccessDto
{
    public int CustomerId { get; set; }
    public string Company { get; set; } = string.Empty;
}
