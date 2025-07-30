// Incheon.WebApi/Program.cs
using Analytics.Data.Context;
using Analytics.Data.Services;
using Incheon.WebApi.Middleware;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Analytics.Data Library Configuration ---
// Create a shared in-memory SQLite connection.
// This connection must remain open for the duration of the application
// for the in-memory database to persist its data throughout the app's lifetime.
// When the app restarts, this connection is new, and thus the in-memory DB is pristine.
var inMemorySqliteConnection = new SqliteConnection("DataSource=InMemoryAnalyticsDb;Mode=Memory;Cache=Shared");
inMemorySqliteConnection.Open(); // Open the connection immediately

// Register AnalyticsDbContext with the shared in-memory SQLite connection
builder.Services.AddDbContext<AnalyticsDbContext>(options =>
    options.UseSqlite(inMemorySqliteConnection));

// Register the AnalyticsService
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

// --- Memory Cache Configuration ---
// Add memory cache for analytics caching
builder.Services.AddMemoryCache();

// --- Session Configuration ---
// Add distributed memory cache. This is required by AddSession for its default DistributedSessionStore.
builder.Services.AddDistributedMemoryCache();

// Add session services to the DI container.
// You might want to configure options like idle timeout or cookie settings here.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Set a reasonable idle timeout
    options.Cookie.HttpOnly = true; // Make the session cookie inaccessible to client-side script
    options.Cookie.IsEssential = true; // Mark the session cookie as essential for GDPR compliance
    options.Cookie.SameSite = SameSiteMode.Lax; // Add SameSite policy
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Use secure cookies when appropriate
});

// --- CORS Configuration ---
// Define a CORS policy that allows everything for development.
// In a production environment, you should restrict this to known origins.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policyBuilder =>
        {
            policyBuilder.AllowAnyOrigin()    // Allow requests from any origin
                   .AllowAnyMethod()    // Allow any HTTP method (GET, POST, PUT, DELETE, etc.)
                   .AllowAnyHeader();   // Allow any HTTP header
        });

    // Example of a more restrictive production policy
    options.AddPolicy("Production",
        policyBuilder =>
        {
            policyBuilder.WithOrigins("https://yourdomain.com", "https://www.yourdomain.com")
                   .WithMethods("GET", "POST", "PUT", "DELETE")
                   .WithHeaders("Content-Type", "Authorization")
                   .AllowCredentials();
        });
});

// --- Health Checks ---
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AnalyticsDbContext>("analytics_database");

// --- Logging Configuration ---
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure logging levels
if (builder.Environment.IsDevelopment())
{
    builder.Logging.SetMinimumLevel(LogLevel.Debug);
}
else
{
    builder.Logging.SetMinimumLevel(LogLevel.Information);
}

// Add services to the container.
builder.Services.AddControllers();

// Configure JSON options
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.SerializerOptions.WriteIndented = true;
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// --- Analytics.Data Database Initialization (once per app run) ---
// This ensures the in-memory database schema is created when the application starts.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AnalyticsDbContext>();
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Ensure the database is created. For in-memory, this effectively creates the tables.
        context.Database.EnsureCreated();
        logger.LogInformation("Analytics database initialized successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize analytics database");
        throw;
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts(); // Add HSTS for production
}

// --- Security Headers Middleware (for production) ---
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });
}

app.UseHttpsRedirection();

// --- CORS Middleware Registration ---
// UseCors must be placed before UseRouting and UseAuthorization.
var corsPolicy = app.Environment.IsDevelopment() ? "AllowAll" : "Production";
app.UseCors(corsPolicy);

// --- Session Middleware Registration ---
// UseSession must be placed after UseRouting and UseAuthentication/UseAuthorization
// if they are used, but before middleware that relies on session state.
app.UseSession();

// --- Health Checks ---
app.MapHealthChecks("/health");

// --- Custom Analytics Middleware Registration ---
// Place this before app.MapControllers() to ensure it captures all requests
app.UseMiddleware<RequestAnalyticsMiddleware>();

app.UseAuthorization(); // This order is typically important: Authentication -> Authorization -> Session -> Endpoints

app.MapControllers();

// --- Graceful Shutdown ---
var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application is shutting down...");
    
    // Close the SQLite connection gracefully
    inMemorySqliteConnection?.Close();
    inMemorySqliteConnection?.Dispose();
});

app.Run();