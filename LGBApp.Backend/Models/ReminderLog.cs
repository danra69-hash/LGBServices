namespace LGBApp.Backend.Models;

/// <summary>Persisted reminder send state — caps + multi-instance claim (SR7 W1).</summary>
public class ReminderLog
{
    public int ReminderLogId { get; set; }
    /// <summary>MoiHodReminder | MoiRequesterPrompt | MoaApproverReminder | MoaCosecPrompt</summary>
    public string Kind { get; set; } = string.Empty;
    /// <summary>MOIForm | WorkflowStepInstance</summary>
    public string TargetEntityType { get; set; } = string.Empty;
    public int TargetEntityId { get; set; }
    public int SentCount { get; set; }
    public DateTime? LastSentAt { get; set; }
    /// <summary>Exclusive claim window for multi-instance workers.</summary>
    public DateTime? ClaimedUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class ReminderKinds
{
    public const string MoiHodReminder = "MoiHodReminder";
    public const string MoiRequesterPrompt = "MoiRequesterPrompt";
    public const string MoaApproverReminder = "MoaApproverReminder";
    public const string MoaCosecPrompt = "MoaCosecPrompt";

    public const string TargetMoiForm = "MOIForm";
    public const string TargetWorkflowStep = "WorkflowStepInstance";
}
