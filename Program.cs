using BUA_project.Models;
using Microsoft.AspNetCore.Identity;
using BUA_project.Services;

namespace BUA_project
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            builder.Services.AddDbContext<Entity>();

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<Entity>()
                .AddDefaultTokenProviders();

            builder.Services.AddHttpClient<FuelPredictionService>(client =>
            {
                client.BaseAddress = new Uri(
                    "https://devhub-fleet-booking-ml-production.up.railway.app/");
            });

            var app = builder.Build();


            // =========================================
            // Seed Identity Data
            // =========================================

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                await IdentitySeed.SeedAsync(services);
            }


            // Configure the HTTP request pipeline.

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapStaticAssets();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}")
                .WithStaticAssets();

            app.Run();



            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
            }
            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Account}/{action=Login}/{id?}")
                .WithStaticAssets();

            app.Run();
        }
    }
}

