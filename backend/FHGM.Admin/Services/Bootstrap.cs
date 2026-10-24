using FHGM.Admin.Data;
using FHGM.Admin.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FHGM.Admin.Services;

public static class Bootstrap
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Initial foundation only. Replace EnsureCreated with reviewed EF migrations before evolving a live database.
        await db.Database.EnsureCreatedAsync();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole(role)));

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.Users.AnyAsync()) { await transaction.CommitAsync(); return; }
        var email = configuration["Bootstrap:Email"];
        var password = configuration["Bootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Set Bootstrap__Email and Bootstrap__Password for the first administrator.");
        var user = new AppUser { UserName = email, Email = email, DisplayName = "FHGM Administrator", EmailConfirmed = true };
        Ensure(await users.CreateAsync(user, password));
        Ensure(await users.AddToRoleAsync(user, Roles.SuperAdmin));
        await transaction.CommitAsync();
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
