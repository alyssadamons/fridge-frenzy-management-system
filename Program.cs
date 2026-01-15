using E_Commerce.Areas.Dashboard.Data;
using E_Commerce.Client;
using E_Commerce.Dashboard.Services;
using E_Commerce.Data;
using E_Commerce.Models;
using E_Commerce.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ===== Add DbContexts =====

// ApplicationDbContext for Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// DashboardDbContext for app data
builder.Services.AddDbContext<DashboardDbContext>(options =>
    options.UseSqlServer(connectionString));

// ===== Services Registration =====
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<ILoggingService, EnhancedLoggingService>(); 

// ===== Identity Configuration =====
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Username/Email rules
builder.Services.Configure<IdentityOptions>(options =>
{
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789@.-_";
    options.User.RequireUniqueEmail = true;
});

// ===== Email Sender (Dummy for dev) =====
builder.Services.AddTransient<IEmailSender, DummyEmailSender>();

// ===== MVC + Razor Pages =====
builder.Services.AddControllersWithViews().AddRazorRuntimeCompilation();
builder.Services.AddRazorPages();
builder.Services.AddHttpContextAccessor();

// ===== SESSION CONFIGURATION =====
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(5); // 5 minute timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    // IMPORTANT: This makes session expire when browser closes
    options.Cookie.MaxAge = null;
});

// ===== AUTHENTICATION COOKIE CONFIGURATION =====
builder.Services.ConfigureApplicationCookie(options =>
{
    // Session expires after 5 minutes of inactivity
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

    // Reset timer on activity (sliding expiration)
    options.SlidingExpiration = true;

    // CRITICAL: Cookie expires when browser closes
    options.Cookie.MaxAge = null;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);

    // Redirect paths
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Home/AccessDenied";

    // Security settings
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;

    // IMPORTANT: Session cookie (not persistent)
    options.Cookie.IsEssential = true;
});
// ===== PayPal Client configuration =====
builder.Services.AddSingleton(x => new PayPalClient(
    builder.Configuration["PayPalOptions:Mode"],
    builder.Configuration["PayPalOptions:ClientId"],
    builder.Configuration["PayPalOptions:SecretKey"]
));

// ===== QuestPDF License =====
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// ===== Apply Migrations & Seed Roles/Admin =====
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    // Seed roles and admin user
    await CreateRolesAndAdminUser(services);
}

// ===== Configure HTTP Pipeline =====
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSession();
app.UseHttpsRedirection();

// ===== Static Files Configuration =====
app.UseStaticFiles();
//app.UseStaticFiles(new StaticFileOptions
//{
//    FileProvider = new PhysicalFileProvider(
//        Path.Combine(builder.Environment.WebRootPath, "img")),
//    RequestPath = "/img"
//});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;

    if (response.StatusCode == 403) // Forbidden
    {
        response.Redirect("/Home/AccessDenied");
    }
});

// ===== Route Mapping =====

// Dashboard Area Routes
app.MapControllerRoute(
    name: "Dashboard",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");

// Default Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();

// ===== Role Creation Methods =====
async Task CreateRolesAndAdminUser(IServiceProvider services)
{
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    // Define all roles needed for the application
    string[] roleNames = {
        "Admin",
        "User",
        "Sales",
        "CustomerManager",
        "Technician",
        "Employee"
    };

    // Create roles if they don't exist
    foreach (var roleName in roleNames)
    {
        var roleExist = await roleManager.RoleExistsAsync(roleName);
        if (!roleExist)
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
            Console.WriteLine($"Created role: {roleName}");
        }
    }

    // Create admin user if it doesn't exist
    var adminEmail = "admin@fridgefrenzy.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);

    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,
            Owner = "Admin User",
            CompanyName = "FridgeFrenzy Admin"
        };

        var result = await userManager.CreateAsync(adminUser, "Admin123!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
            Console.WriteLine("? Admin user created successfully with Admin role");
        }
        else
        {
            Console.WriteLine("? Failed to create admin user:");
            foreach (var error in result.Errors)
            {
                Console.WriteLine($" - {error.Description}");
            }
        }
    }
    else
    {
        // Check current roles and ensure ONLY Admin role
        var currentRoles = await userManager.GetRolesAsync(adminUser);
        Console.WriteLine($"? Current roles for {adminEmail}: {string.Join(", ", currentRoles)}");

        var isInAdminRole = await userManager.IsInRoleAsync(adminUser, "Admin");

        if (!isInAdminRole || currentRoles.Any(r => r != "Admin"))
        {
            // Remove ALL existing roles first
            if (currentRoles.Any())
            {
                var removeResult = await userManager.RemoveFromRolesAsync(adminUser, currentRoles);
                if (removeResult.Succeeded)
                {
                    Console.WriteLine($"? Removed existing roles: {string.Join(", ", currentRoles)}");
                }
            }

            // Add ONLY Admin role
            var addResult = await userManager.AddToRoleAsync(adminUser, "Admin");
            if (addResult.Succeeded)
            {
                Console.WriteLine("? Fixed admin user - now has ONLY Admin role");
            }
            else
            {
                Console.WriteLine("? Failed to add Admin role:");
                foreach (var error in addResult.Errors)
                {
                    Console.WriteLine($" - {error.Description}");
                }
            }
        }
        else
        {
            Console.WriteLine("? Admin user already has correct Admin role");
        }
    }

    // Debug: Verify the final role assignment
    var finalRoles = await userManager.GetRolesAsync(adminUser);
    Console.WriteLine($"? Final roles for {adminEmail}: {string.Join(", ", finalRoles)}");
}