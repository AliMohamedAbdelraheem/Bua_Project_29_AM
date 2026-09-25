using BUA_project.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BUA_project.Services
{
    public static class IdentitySeed
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            var roleManager =
                services.GetRequiredService<RoleManager<IdentityRole>>();

            var userManager =
                services.GetRequiredService<UserManager<ApplicationUser>>();

            var context =
                services.GetRequiredService<Entity>();


            // =========================================
            // 1. Create Admin Role
            // =========================================

            const string adminRole = "Admin";

            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                var roleResult =
                    await roleManager.CreateAsync(
                        new IdentityRole(adminRole));

                if (!roleResult.Succeeded)
                {
                    throw new Exception(
                        "Failed to create Admin role.");
                }
            }


            // =========================================
            // 2. Root Admin Credentials
            // =========================================

            const string adminEmail =
                "admin@bua-fleet.com";

            const string adminPassword =
                "Admin@123456";


            // =========================================
            // 3. Check if Admin already exists
            // =========================================

            var admin =
                await userManager.FindByEmailAsync(adminEmail);


            // =========================================
            // 4. Create Root Admin
            // =========================================

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true
                };

                var createResult =
                    await userManager.CreateAsync(
                        admin,
                        adminPassword);

                if (!createResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        createResult.Errors.Select(e => e.Description));

                    throw new Exception(
                        $"Failed to create Root Admin: {errors}");
                }
            }


            // =========================================
            // 5. Assign Admin Role
            // =========================================

            if (!await userManager.IsInRoleAsync(
                    admin,
                    adminRole))
            {
                var roleResult =
                    await userManager.AddToRoleAsync(
                        admin,
                        adminRole);

                if (!roleResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        roleResult.Errors.Select(e => e.Description));

                    throw new Exception(
                        $"Failed to assign Admin role: {errors}");
                }
            }


            // =========================================
            // 6. Link Admin to Business User
            // =========================================

            if (admin.BusinessUserId == null)
            {
                var businessAdmin =
                    await context.Users
                        .FirstOrDefaultAsync(
                            u => u.Email == adminEmail);

                if (businessAdmin == null)
                {
                    businessAdmin = new User
                    {
                        Name = "Admin",
                        Email = adminEmail,
                        Role = "Admin"
                    };

                    context.Users.Add(businessAdmin);

                    await context.SaveChangesAsync();
                }

                admin.BusinessUserId =
                    businessAdmin.UserId;

                var updateResult =
                    await userManager.UpdateAsync(admin);

                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(
                        ", ",
                        updateResult.Errors.Select(e => e.Description));

                    throw new Exception(
                        $"Failed to link Admin to Business User: {errors}");
                }
            }
        }
    }
}