using ABCRetailers.Services;
using Microsoft.AspNetCore.Authentication.Cookies; // Added for auth
using Microsoft.AspNetCore.Authorization;
using System.Globalization;

namespace ABCRetailers
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container
            builder.Services.AddControllersWithViews();

            // Add logging
            builder.Services.AddLogging();

            // --- Authentication & Authorization Services ---
            // 1. Add Authentication services
            builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.HttpOnly = true;
                    options.ExpireTimeSpan = TimeSpan.FromDays(7);
                    options.LoginPath = "/Account/Login";  // Redirect here if user is not logged in
                    options.AccessDeniedPath = "/Account/AccessDenied"; // Redirect here if logged in but no permission
                    options.SlidingExpiration = true;
                });

            // 2. Add Authorization services
            builder.Services.AddAuthorization(options =>
            {
                // This policy ensures that all endpoints require authentication by default
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            });

            // --- Session Services (for Shopping Cart) ---
            builder.Services.AddDistributedMemoryCache(); // Adds a default in-memory cache for session
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); // Session lifetime
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
            });

            // Required for services that need access to the HttpContext (like CartService)
            builder.Services.AddHttpContextAccessor();

            // --- Application Service Registrations ---
            builder.Services.AddScoped<ICartService, CartService>();
            builder.Services.AddScoped<IAuthService, AuthService>();

            // Register SqlDatabaseService. Using AddSingleton for connection efficiency
            // You can change to AddScoped if you prefer
            builder.Services.AddSingleton<ISqlDatabaseService, SqlDatabaseService>();

            // Register Azure Storage Service with proper lifecycle
            builder.Services.AddSingleton<IAzureStorageService>(provider =>
            {
                var config = provider.GetRequiredService<IConfiguration>();
                var logger = provider.GetRequiredService<ILogger<AzureStorageService>>();
                var service = new AzureStorageService(config, logger);

                // Initialize asynchronously but don't block startup
                _ = service.InitializeStorageAsync();

                return service;
            });

            builder.Services.AddScoped<IAzureFunctionService, AzureFunctionService>();

            // Add HttpClient for calling Azure Functions
            builder.Services.AddHttpClient("AzureFunctions", client =>
            {
                var baseUrl = builder.Configuration["AzureFunctions:BaseUrl"] ?? "http://localhost:7071/api";
                client.BaseAddress = new Uri(baseUrl);

                var functionKey = builder.Configuration["AzureFunctions:FunctionKey"];
                if (!string.IsNullOrEmpty(functionKey))
                {
                    client.DefaultRequestHeaders.Add("x-functions-key", functionKey);
                }
            });

            var app = builder.Build();

            // Set culture for decimal handling
            var culture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting(); // Routing must come first

            // --- Auth & Session Middleware (ORDER IS CRITICAL) ---
            app.UseAuthentication(); // 1. Who are you? (Identifies user from cookie)
            app.UseAuthorization();  // 2. Are you allowed? (Checks permissions)
            app.UseSession();        // 3. Load the session data (for the cart)

            // Map controller routes after all middleware is set up
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            // Ensure storage is initialized before running
            using (var scope = app.Services.CreateScope())
            {
                var storageService = scope.ServiceProvider.GetRequiredService<IAzureStorageService>();
                await storageService.InitializeStorageAsync();
            }

            await app.RunAsync();
        }
    }
}