using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RoomBooking.Web.Api;
using RoomBooking.Web.Bookings;
using RoomBooking.Web.Components;
using RoomBooking.Web.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddProblemDetails();

// The connection string is read when the first DbContext is created, so tests can override it.
builder.Services.AddDbContextFactory<RoomBookingDbContext>((services, options) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var environment = services.GetRequiredService<IWebHostEnvironment>();
    options.UseSqlite(ResolveConnectionString(configuration, environment));
});
builder.Services.AddScoped<BookingService>();

var app = builder.Build();

// Create the SQLite file, schema and seed rooms on first start.
await using (var db = await app.Services.GetRequiredService<IDbContextFactory<RoomBookingDbContext>>().CreateDbContextAsync())
{
    await db.Database.EnsureCreatedAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// API errors without a body (e.g. malformed JSON) get a ProblemDetails body;
// unknown UI pages show the Blazor "not found" page.
app.UseWhen(context => IsApiRequest(context), api => api.UseStatusCodePages());
app.UseWhen(context => !IsApiRequest(context), ui => ui.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));

app.UseAntiforgery();

app.MapStaticAssets();
app.MapBookingApi();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsApiRequest(HttpContext context) => context.Request.Path.StartsWithSegments("/api");

// A relative database path is resolved against the content root, not the current directory.
static string ResolveConnectionString(IConfiguration configuration, IWebHostEnvironment environment)
{
    var builder = new SqliteConnectionStringBuilder(
        configuration.GetConnectionString("RoomBooking") ?? "Data Source=roombooking.db");

    if (builder.DataSource != ":memory:" && !Path.IsPathRooted(builder.DataSource))
    {
        builder.DataSource = Path.Combine(environment.ContentRootPath, builder.DataSource);
    }

    return builder.ToString();
}
