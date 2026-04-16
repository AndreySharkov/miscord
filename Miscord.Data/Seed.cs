using Microsoft.AspNetCore.Identity;
using Miscord.Data.Models;

namespace Miscord.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, AppDbContext context)
        {
            // 1. Create the Admin Role (Rubric Requirement)
            if (!await roleManager.RoleExistsAsync("Admin"))
            {
                await roleManager.CreateAsync(new IdentityRole("Admin"));
            }

            // 2. Create a default User Role
            if (!await roleManager.RoleExistsAsync("User"))
            {
                await roleManager.CreateAsync(new IdentityRole("User"));
            }

            // 3. Create the Admin Account (Rubric Requirement)
            var adminEmail = "admin@miscord.com";
            var adminUser = await userManager.FindByEmailAsync(adminEmail);
            
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    Nickname = "MiscordAdmin", // Your custom property!
                    EmailConfirmed = true
                };
                
                // Creates the user with a default password
                await userManager.CreateAsync(adminUser, "AdminPass123!");
                
                // Assigns them to the Admin role
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }

            // 4. Seed a Default Server and Channel (Rubric Requirement)
            if (!context.Servers.Any())
            {
                var defaultServer = new Server
                {
                    // Assuming you have a Name property
                    Name = "Miscord Global Lounge", 
                    OwnerId = adminUser.Id,
                    Channels = new List<Channel>
                    {
                        new Channel { Name = "general" },
                        new Channel { Name = "announcements" }
                    }
                };

                context.Servers.Add(defaultServer);
                await context.SaveChangesAsync();
            }
        }
    }
}