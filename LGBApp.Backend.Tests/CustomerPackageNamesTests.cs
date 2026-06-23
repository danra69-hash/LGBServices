using LGBApp.Backend.Services;

namespace LGBApp.Backend.Tests;

public class CustomerPackageNamesTests
{
    [Theory]
    [InlineData("Add-ons only", true)]
    [InlineData("add-ons only", true)]
    [InlineData(" Enterprise Package ", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsAddOnsOnly_DetectsPackageName(string? name, bool expected)
    {
        Assert.Equal(expected, CustomerPackageNames.IsAddOnsOnly(name));
    }
}
