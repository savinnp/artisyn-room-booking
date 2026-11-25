using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace RoomBooking.Web.Tests;

/// <summary>Runs the real app in memory against its own temporary SQLite file.</summary>
public sealed class RoomBookingAppFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath =
        Path.Combine(Path.GetTempPath(), $"roombooking-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:RoomBooking", $"Data Source={databasePath}");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Pooled connections keep the file open on Windows.
        SqliteConnection.ClearAllPools();
        File.Delete(databasePath);
    }
}
