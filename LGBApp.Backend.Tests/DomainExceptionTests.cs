using LGBApp.Backend.Services;
using Xunit;

namespace LGBApp.Backend.Tests;

public class DomainExceptionTests
{
    [Fact]
    public void DefaultsToBadRequest()
    {
        var ex = new DomainException("bad input");
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal("bad input", ex.Message);
    }

    [Fact]
    public void CanSetCustomStatus()
    {
        var ex = new DomainException("slow down", 429);
        Assert.Equal(429, ex.StatusCode);
    }
}
