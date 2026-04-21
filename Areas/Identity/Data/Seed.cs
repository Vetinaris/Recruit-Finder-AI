using Microsoft.AspNetCore.Identity;
using Recruit_Finder_AI.Models;

namespace Recruit_Finder_AI.Areas.Identity.Data
{
    public class Seed
    {
        public static async Task SeedData(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roleNames = { "ADMIN", "MODERATOR", "USER" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            string adminEmail = "admin@recruit.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    PasswordExpiration = DateTime.UtcNow.AddYears(1)
                };

                var result = await userManager.CreateAsync(admin, "Admin123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "ADMIN");
                }
            }

            string modEmail = "mod@recruit.com";
            if (await userManager.FindByEmailAsync(modEmail) == null)
            {
                var moderator = new ApplicationUser
                {
                    UserName = modEmail,
                    Email = modEmail,
                    EmailConfirmed = true,
                    PasswordExpiration = DateTime.UtcNow.AddYears(1)
                };

                var result = await userManager.CreateAsync(moderator, "Mod123!");
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(moderator, "MODERATOR");
                }
            }
        }
    }
}