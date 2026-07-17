namespace LGBApp.Backend.Services;

/// <summary>Injectable clock for reminder scheduling tests (SR7 W1).</summary>
public interface IAppClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemAppClock : IAppClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
