using LGBApp.Backend.Models;
using LGBApp.Backend.Services;

namespace LGBApp.Backend.Data;

/// <summary>Seeds LGB internal team from MOI/MOA workflow (Sharon intake, reso prep team).</summary>
public static class InternalStaffSeeder
{
    private static readonly (string Email, string Name, string Role, string JobTitle, bool CanApproveMoiIntake, bool CanRecommendMoi)[] Staff =
    [
        ("sharon@lgb.test", "Sharon", UserRoles.Admin, "MOI intake & assignment", true, false),
        ("ngpohli@lgb.test", "Ng Poh Li", UserRoles.User, "Resolution preparation", false, false),
        ("nita@lgb.test", "Nita", UserRoles.User, "Resolution preparation", false, false),
        ("siti@lgb.test", "Siti", UserRoles.User, "Resolution preparation", false, false),
        ("nadia@lgb.test", "Nadia", UserRoles.User, "Resolution preparation", false, false),
    ];

    public static void Seed(AppDbContext context)
    {
        foreach (var (email, name, role, jobTitle, canApproveIntake, canRecommend) in Staff)
        {
            var existing = context.Users.FirstOrDefault(u => u.Email == email);
            if (existing != null)
            {
                existing.Name = name;
                existing.Role = role;
                existing.JobTitle = jobTitle;
                existing.CanApproveMoiIntake = canApproveIntake;
                existing.CanRecommendMoi = canRecommend;
                continue;
            }

            context.Users.Add(new User
            {
                Email = email,
                PasswordHash = PasswordHasher.Hash("password123"),
                Name = name,
                Mobile = "",
                Role = role,
                JobTitle = jobTitle,
                CanApproveMoiIntake = canApproveIntake,
                CanRecommendMoi = canRecommend,
                IsVerified = true,
                MustChangePassword = true,
                CreatedAt = DateTime.UtcNow,
            });
        }

        context.SaveChanges();
    }
}
