using LGBApp.Backend.Models;
using LGBApp.Backend.Models.DTOs;

namespace LGBApp.Backend.Services;

public static class CompletedServiceMapper
{
    public static CompletedServiceResponse ToResponse(CompletedService service) => new()
    {
        Id = service.Id,
        Customer = service.Customer,
        Service = service.Service,
        UsedQty = service.UsedQty,
        TotalQty = service.TotalQty,
        DateRequested = service.DateRequested.ToString("yyyy-MM-dd"),
        DateCompleted = service.DateCompleted.ToString("yyyy-MM-dd"),
        AccountHolder = service.AccountHolder,
        JobAssignedTo = service.JobAssignedTo,
        Status = service.Status
    };
}
