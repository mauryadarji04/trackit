using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TrackIt.Infrastructure;
using TrackIt.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    // Every POST needs an anti-forgery token (the form tag helper adds it automatically).
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// Registers EF Core, Identity, Dapper repositories, services and integrations.
builder.Services.AddTrackIt(builder.Configuration);

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Account/Login";
    o.AccessDeniedPath = "/Account/AccessDenied";
    o.SlidingExpiration = true;
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Re-check the security stamp every minute so a deactivated user is signed out quickly.
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddScoped<DbSeeder>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

// Create roles (and demo data in Development) on startup.
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
}

app.Run();
