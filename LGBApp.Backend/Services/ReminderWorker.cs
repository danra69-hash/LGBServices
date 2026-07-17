using Microsoft.Extensions.Hosting;

namespace LGBApp.Backend.Services;

/// <summary>SR7 W1 — periodic reminder evaluation. Uses scoped services per tick.</summary>
public sealed class ReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReminderWorker> _logger;
    private readonly TimeSpan _interval;

    public ReminderWorker(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<ReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var minutes = 15;
        if (int.TryParse(config["Reminders:IntervalMinutes"], out var m) && m >= 1)
            minutes = Math.Min(m, 120);
        _interval = TimeSpan.FromMinutes(minutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ReminderWorker started (interval {Interval})", _interval);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var evaluator = scope.ServiceProvider.GetRequiredService<ReminderEvaluationService>();
                var n = await evaluator.ProcessDueAsync(stoppingToken);
                if (n > 0)
                    _logger.LogInformation("ReminderWorker processed {Count} reminder action(s)", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ReminderWorker tick failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
