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
});

// Add services to the container.
builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// --- Analytics.Data Database Initialization (once per app run) ---
// This ensures the in-memory database schema is created when the application starts.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AnalyticsDbContext>();
    // Ensure the database is created. For in-memory, this effectively creates the tables.
    context.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// --- Session Middleware Registration ---
// UseSession must be placed after UseRouting and UseAuthentication/UseAuthorization
// if they are used, but before middleware that relies on session state.
app.UseSession();

// --- Custom Analytics Middleware Registration ---
// Place this before app.MapControllers() to ensure it captures all requests
app.UseMiddleware<RequestAnalyticsMiddleware>();

app.UseAuthorization(); // This order is typically important: Authentication -> Authorization -> Session -> Endpoints

app.MapControllers();

app.Run();
