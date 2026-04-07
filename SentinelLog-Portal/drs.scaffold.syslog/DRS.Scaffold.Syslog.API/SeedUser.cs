using BCrypt.Net;
using DRS.Scaffold.Syslog.Core.Data;
using DRS.Scaffold.Syslog.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DRS.Scaffold.Syslog.API;

/// <summary>
/// Seed helper � inserts test users into the <c>appuser</c> table if they
/// do not already exist.  Called from Program.cs when the
/// <c>--seed-users</c> CLI argument is present, or automatically in
/// Development when the table is empty.
/// </summary>
public static class SeedUser
{
    // ?? Test accounts ?????????????????????????????????????????????????????????
    private static readonly SeedAccount[] Accounts =
    [
        new("admin",    "admin@sentinellog.local",    "Admin@12345!",  "Platform Administrator", "Admin"),
        new("analyst",  "analyst@sentinellog.local",  "Analyst@12345!", "SOC Analyst",           "Analyst"),
        new("readonly", "readonly@sentinellog.local", "ReadOnly@12345!", "Read-Only Viewer",     "ReadOnly"),
    ];

    /// <summary>
    /// Seeds missing test users.  Safe to call on every startup �
    /// skips any username that already exists.
    /// </summary>
    public static async Task RunAsync(IServiceProvider services, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SyslogDbContext>();

        // Ensure base roles exist before creating users
        foreach (var roleName in new[] { "Admin", "Analyst", "ReadOnly" })
        {
            if (!await db.Roles.AnyAsync(r => r.Name == roleName))
            {
                db.Roles.Add(new Role
                {
                    Name        = roleName,
                    Description = roleName + " role",
                    CreatedAt   = DateTimeOffset.UtcNow
                });
            }
        }
        await db.SaveChangesAsync();

        foreach (var account in Accounts)
        {
            bool exists = await db.AppUsers
                .AnyAsync(u => u.Username == account.Username);

            if (exists)
            {
                logger.LogInformation("[SeedUser] '{Username}' already exists � skipped.", account.Username);
                continue;
            }

            // Hash with BCrypt work-factor 12 (same as UserRepository.CreateAsync)
            var hash = BCrypt.Net.BCrypt.HashPassword(account.PlainPassword, workFactor: 12);

            var user = new AppUser
            {
                Username     = account.Username,
                Email        = account.Email,
                PasswordHash = hash,
                FullName     = account.FullName,
                Active       = true,
                CreatedBy    = "seed",
                CreatedAt    = DateTimeOffset.UtcNow,
                UpdatedAt    = DateTimeOffset.UtcNow,
                IsDeleted    = false
            };

            db.AppUsers.Add(user);
            await db.SaveChangesAsync();

            // Assign role if it exists in the role table
            var role = await db.Roles.FirstOrDefaultAsync(r => r.Name == account.RoleName);
            if (role is not null)
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
                await db.SaveChangesAsync();
            }

            logger.LogInformation(
                "[SeedUser] Created '{Username}' (id={Id}) role='{Role}'.",
                user.Username, user.Id, account.RoleName);
        }
    }

    private record SeedAccount(
        string Username,
        string Email,
        string PlainPassword,
        string FullName,
        string RoleName);
}
