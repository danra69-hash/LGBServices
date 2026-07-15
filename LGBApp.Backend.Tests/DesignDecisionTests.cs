using LGBApp.Backend.Models;
using LGBApp.Backend.Services;
using Xunit;

namespace LGBApp.Backend.Tests;

public class DesignDecisionTests
{
    [Fact]
    public void D3_Signature_RequiresDataUrlImageOrPdf()
    {
        Assert.False(ClientApprovalService.IsValidSignature(null, null));
        Assert.False(ClientApprovalService.IsValidSignature("plain", "x.png"));
        Assert.True(ClientApprovalService.IsValidSignature("data:image/png;base64,iVBORw0KGgo=", "pad.png"));
        Assert.True(ClientApprovalService.IsValidSignature("data:application/pdf;base64,JVBERi0=", "scan.pdf"));
    }

    [Fact]
    public void D4_HasSigned_PrefersUserId_OverNameCollision()
    {
        var holder = new AccountHolder { AccountHolderId = 1, Name = "Alex", UserId = 10, NeedsMoa = true };
        var records = new List<ClientApprovalRecord>
        {
            // Internal user named Alex must not satisfy client holder UserId 10
            new() { UserId = 99, AccountHolderName = "Alex", SignedAt = DateTime.UtcNow },
        };
        Assert.False(ClientApprovalService.HasSigned(records, holder));

        records.Add(new ClientApprovalRecord
        {
            UserId = 10,
            AccountHolderName = "Alex",
            SignedAt = DateTime.UtcNow,
        });
        Assert.True(ClientApprovalService.HasSigned(records, holder));
    }

    [Fact]
    public void D4_MoaClientPhaseComplete_IgnoresInternalNameMatch()
    {
        var customer = new Customer
        {
            CustomerId = 1,
            Company = "Acme",
            AccountHolders =
            [
                new AccountHolder { AccountHolderId = 1, Name = "Alice Client", UserId = 5, NeedsMoa = true },
            ],
        };
        var records = new List<ClientApprovalRecord>
        {
            new() { UserId = 1, AccountHolderName = "Alice Client", SignedAt = DateTime.UtcNow },
        };
        Assert.False(ClientApprovalService.MoaClientPhaseComplete(customer, records));

        records.Add(new ClientApprovalRecord
        {
            UserId = 5,
            AccountHolderName = "Alice Client",
            SignedAt = DateTime.UtcNow,
        });
        Assert.True(ClientApprovalService.MoaClientPhaseComplete(customer, records));
    }
}
