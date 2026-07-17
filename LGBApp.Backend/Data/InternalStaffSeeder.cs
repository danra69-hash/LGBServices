using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Data;

/// <summary>
/// Seeds LGB internal team: Sharon (admin + intake/MOI/MOA sign-off), resolution prep staff (assigned work only).
/// Default password only for Development; production must pass SEED_STAFF_PASSWORD via env (C2).
/// Also ensures live email-chain test accounts (gmail admin / berkeley client).
/// </summary>
public static class InternalStaffSeeder
{
    private static readonly (string Email, string Name, string Role, string JobTitle,
        bool CanApproveMoiIntake, bool CanRecommendMoi, bool CanApproveMoi, bool CanApproveMoa)[] Staff =
    [
        ("sharon@lgb.test", "Sharon", UserRoles.Admin, "Senior Manager, Company Secretarial", true, true, true, true),
        // Live email-chain testing (Resend): admin + full MOA powers
        ("ryannnism@gmail.com", "Ryan Admin", UserRoles.Admin, "Senior Manager, Company Secretarial", true, true, true, true),
        ("ngpohli@lgb.test", "Ng Poh Li", UserRoles.User, "Resolution preparation", false, false, false, false),
        ("nita@lgb.test", "Nita", UserRoles.User, "Resolution preparation", false, false, false, false),
        ("siti@lgb.test", "Siti", UserRoles.User, "Resolution preparation", false, false, false, false),
        ("nadia@lgb.test", "Nadia", UserRoles.User, "Resolution preparation", false, false, false, false),
    ];

    private static readonly string[] IntakeApproverEmails =
    [
        "sharon@lgb.test",
        "ryannnism@gmail.com",
        "danra69@gmail.com",
    ];

    public const string LiveTestAdminEmail = "ryannnism@gmail.com";
    public const string LiveTestClientEmail = "ryannnism@berkeley.edu";

    public static void Seed(
        AppDbContext context,
        bool resetPasswordsInDevelopment = false,
        string initialPassword = "password123")
    {
        if (string.IsNullOrWhiteSpace(initialPassword) || initialPassword.Length < 6)
            throw new InvalidOperationException(
                $"Initial staff password must be at least {PasswordPolicy.MinLength} characters.");

        foreach (var (email, name, role, jobTitle, canApproveIntake, canRecommend, canApproveMoi, canApproveMoa) in Staff)
        {
            var existing = context.Users.FirstOrDefault(u => u.Email == email);
            if (existing != null)
            {
                existing.Name = name;
                existing.Role = role;
                existing.JobTitle = jobTitle;
                existing.CanApproveMoiIntake = canApproveIntake;
                existing.CanRecommendMoi = canRecommend;
                existing.CanApproveMoi = canApproveMoi;
                existing.CanApproveMoa = canApproveMoa;
                if (resetPasswordsInDevelopment)
                {
                    existing.PasswordHash = PasswordHasher.Hash(initialPassword);
                    existing.MustChangePassword = true;
                }
                continue;
            }

            context.Users.Add(new User
            {
                Email = email,
                PasswordHash = PasswordHasher.Hash(initialPassword),
                Name = name,
                Mobile = "",
                Role = role,
                JobTitle = jobTitle,
                CanApproveMoiIntake = canApproveIntake,
                CanRecommendMoi = canRecommend,
                CanApproveMoi = canApproveMoi,
                CanApproveMoa = canApproveMoa,
                IsVerified = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        foreach (var email in IntakeApproverEmails)
        {
            var user = context.Users.FirstOrDefault(u => u.Email == email);
            if (user != null)
                user.CanApproveMoiIntake = true;
        }

        EnsureLiveTestClientAdmin(context, initialPassword, resetPasswordsInDevelopment);
        context.SaveChanges();
    }

    /// <summary>
    /// Client portal test account for live email-chain runs (berkeley.edu).
    /// Linked to the first Active customer when present.
    /// </summary>
    private static void EnsureLiveTestClientAdmin(
        AppDbContext context,
        string initialPassword,
        bool resetPassword)
    {
        var customer = context.Customers.FirstOrDefault(c => c.Status == "Active")
            ?? context.Customers.FirstOrDefault();

        var existing = context.Users.FirstOrDefault(u => u.Email == LiveTestClientEmail);
        if (existing != null)
        {
            existing.Role = UserRoles.ClientAdmin;
            existing.Name = string.IsNullOrWhiteSpace(existing.Name) ? "Ryan Client" : existing.Name;
            existing.IsVerified = true;
            if (customer != null)
                existing.CustomerId = customer.CustomerId;
            if (resetPassword)
            {
                existing.PasswordHash = PasswordHasher.Hash(initialPassword);
                existing.MustChangePassword = true;
            }
            return;
        }

        if (customer == null)
            return;

        context.Users.Add(new User
        {
            Email = LiveTestClientEmail,
            PasswordHash = PasswordHasher.Hash(initialPassword),
            Name = "Ryan Client",
            Mobile = "",
            Role = UserRoles.ClientAdmin,
            CustomerId = customer.CustomerId,
            IsVerified = true,
            MustChangePassword = true,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
