namespace LGBApp.Backend.Models;

public class AppNotification
{
    public int AppNotificationId { get; set; }
    public int UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? JobRequestId { get; set; }
    public int? CustomerId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
