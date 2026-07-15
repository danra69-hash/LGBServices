namespace LGBApp.Backend.Services;

public static class PackageItemStatuses
{
    public const string MoiNotReceived = "moi_not_received";
    public const string MoiRejected = "moi_rejected";
    public const string AwaitingIntake = "awaiting_intake";
    public const string AdminBypass = "admin_bypass";
    public const string ResolutionPrep = "resolution_prep";
    public const string PendingRecommendation = "pending_recommendation";
    public const string MoiSignOff = "moi_sign_off";
    public const string AwaitingSecAssignment = "awaiting_sec_assignment";
    public const string MoiApproved = "moi_approved";
    public const string Completed = "completed";

    public const string AwaitingMoi = "awaiting_moi";
    public const string PendingSignOff = "pending_sign_off";
    public const string Approved = "approved";

    public const string MoiNotComplete = "moi_not_complete";
    public const string ReadyForMoa = "ready_for_moa";
    public const string MoaCirculation = "moa_circulation";
    public const string PendingExecute = "pending_execute";
    public const string Execution = "execution";
    public const string ExecutionPendingApproval = "execution_pending_approval";

    public const string NotStarted = "not_started";
    public const string InProgress = "in_progress";
    public const string Canceled = "canceled";

    private static readonly Dictionary<string, string> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        [MoiNotReceived] = "MOI not received",
        [MoiRejected] = "MOI rejected",
        [AwaitingIntake] = "With LGB for review",
        [AdminBypass] = "Client note for LGB",
        [ResolutionPrep] = "Resolution prep",
        [PendingRecommendation] = "Pending recommendation",
        [MoiSignOff] = "MOI sign-off",
        [AwaitingSecAssignment] = "Assign secretarial team",
        [MoiApproved] = "MOI approved",
        [Completed] = "Completed",
        [AwaitingMoi] = "Awaiting MOI",
        [PendingSignOff] = "Pending sign-off",
        [Approved] = "Approved",
        [MoiNotComplete] = "MOI not complete",
        [ReadyForMoa] = "Ready for MOA",
        [MoaCirculation] = "MOA circulation",
        [PendingExecute] = "Executing",
        [Execution] = "Executing",
        [ExecutionPendingApproval] = "Awaiting execution sign-off",
        [NotStarted] = "Not started",
        [InProgress] = "In progress",
        [Canceled] = "Canceled",
    };

    public static string LabelFor(string key) =>
        Labels.TryGetValue(key, out var label) ? label : key;

    public static bool IsCompletedBucket(string key) =>
        key is Completed or Approved;

    public static bool IsPendingBucket(string key) =>
        key is MoiNotReceived or MoiRejected or NotStarted or MoiNotComplete or AwaitingMoi;

    public static bool IsInProgressBucket(string key) =>
        !IsCompletedBucket(key) && !IsPendingBucket(key) && key != Canceled;
}

public readonly record struct PackageItemStatusResult(string Key, string Label);
