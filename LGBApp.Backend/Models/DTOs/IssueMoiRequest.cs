namespace LGBApp.Backend.Models.DTOs;

public class IssueMoiRequest
{
    public int? CustomerId { get; set; }
    public int? CustomerPackageId { get; set; }
    public string Service { get; set; } = string.Empty;
    public string? TypeOfDocument { get; set; }
    public string? DocumentTitle { get; set; }
    public string? InitiationDate { get; set; }
    public string? RequestedBy { get; set; }
    public bool AdHoc { get; set; }
}
