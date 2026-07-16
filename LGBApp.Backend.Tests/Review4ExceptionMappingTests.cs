using LGBApp.Backend.Middleware;
using LGBApp.Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LGBApp.Backend.Tests;

public class Review4ExceptionMappingTests
{
    [Fact]
    public void MapException_DomainException_UsesDomainStatus()
    {
        var (status, message) = ExceptionHandlingExtensions.MapException(new DomainException("nope", 403));
        Assert.Equal(403, status);
        Assert.Equal("nope", message);
    }

    [Fact]
    public void MapException_DbUpdateConcurrency_Returns409()
    {
        var (status, message) = ExceptionHandlingExtensions.MapException(new DbUpdateConcurrencyException());
        Assert.Equal(409, status);
        Assert.Equal(ExceptionHandlingExtensions.ConcurrencyConflictMessage, message);
    }

    [Fact]
    public void MapException_UniqueViolationMessage_Returns409()
    {
        var inner = new InvalidOperationException("23505: duplicate key value violates unique constraint");
        var ex = new DbUpdateException("could not insert", inner);
        Assert.True(ExceptionHandlingExtensions.IsUniqueViolation(ex));
        var (status, message) = ExceptionHandlingExtensions.MapException(ex);
        Assert.Equal(409, status);
        Assert.Equal(ExceptionHandlingExtensions.UniqueConflictMessage, message);
    }

    [Fact]
    public void MapException_OtherException_Returns500()
    {
        var (status, _) = ExceptionHandlingExtensions.MapException(new InvalidOperationException("boom"));
        Assert.Equal(500, status);
    }

    [Fact]
    public void Pagination_NormalizesAndCaps()
    {
        Assert.Equal((1, 100), Pagination.Normalize(null, null));
        Assert.Equal((2, 50), Pagination.Normalize(2, 50));
        Assert.Equal((1, 200), Pagination.Normalize(0, 999));
    }
}
