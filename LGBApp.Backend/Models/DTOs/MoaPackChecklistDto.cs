namespace LGBApp.Backend.Models.DTOs;

public class MoaPackChecklistDto
{
    public bool InternalChecklistA { get; set; }
    public bool InternalChecklistB { get; set; }
    public bool CleanAgreementAttached { get; set; }
    public bool ShareholdingTableAttached { get; set; }
    public string SsmRegistrationNo { get; set; } = string.Empty;
    public string SsmNewRegistrationNo { get; set; } = string.Empty;
    public string SsmEntityType { get; set; } = string.Empty;
    public string SsmStatus { get; set; } = string.Empty;
    public string SsmAsAtDate { get; set; } = string.Empty;
}

public class UpdateMoaPackRequest
{
    public MoaPackChecklistDto Checklist { get; set; } = new();
    public bool FinanceRelated { get; set; }
    public bool BankSignatoryMatter { get; set; }
    public bool ShareMovement { get; set; }
}

public class JobHandoffRequest
{
    public string Action { get; set; } = string.Empty;
    public string? Comments { get; set; }
}
