using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class InternalWorkVisibilityTests
{
    [Fact]
    public void UnreleasedServiceJob_HiddenFromInternal()
    {
        var job = new JobRequest
        {
            TaskType = "Service",
            TotalQty = 1,
            InternalHandoffStatus = string.Empty,
            Units = [new JobRequestUnit { UnitNumber = 1 }],
        };

        Assert.False(InternalWorkVisibilityHelper.IsJobLineReleasedToInternal(job, []));
    }

    [Fact]
    public void ClientSubmittedJob_VisibleToInternal()
    {
        var job = new JobRequest
        {
            TaskType = "Service",
            TotalQty = 1,
            InternalHandoffStatus = JobHandoffStatuses.ClientSubmitted,
            Units = [new JobRequestUnit { UnitNumber = 1 }],
        };

        Assert.True(InternalWorkVisibilityHelper.IsJobLineReleasedToInternal(job, []));
    }

    [Fact]
    public void ScheduledButNotSubmittedUnit_NotReleased()
    {
        var job = new JobRequest
        {
            TaskType = "Service",
            TotalQty = 2,
            Units =
            [
                new JobRequestUnit { JobRequestUnitId = 1, UnitNumber = 1, ScheduledDate = DateTime.UtcNow.Date },
                new JobRequestUnit { JobRequestUnitId = 2, UnitNumber = 2 },
            ],
        };

        Assert.False(InternalWorkVisibilityHelper.IsUnitReleasedToInternal(job, job.Units.Last(), null));
    }

    [Fact]
    public void FilterJobsForInternal_ExcludesUnreleasedLines()
    {
        var released = new JobRequest
        {
            JobRequestId = 1,
            TaskType = "Service",
            TotalQty = 1,
            InternalHandoffStatus = JobHandoffStatuses.ClientSubmitted,
            Units = [new JobRequestUnit { UnitNumber = 1 }],
        };
        var unreleased = new JobRequest
        {
            JobRequestId = 2,
            TaskType = "Service",
            TotalQty = 1,
            InternalHandoffStatus = string.Empty,
            Units = [new JobRequestUnit { UnitNumber = 1 }],
        };

        var filtered = InternalWorkVisibilityHelper.FilterJobsForInternal(
            [released, unreleased],
            new Dictionary<int, List<MOIForm>>());

        Assert.Single(filtered);
        Assert.Equal(1, filtered[0].JobRequestId);
    }
}
