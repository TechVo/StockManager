using Microsoft.EntityFrameworkCore;

namespace StockManager.Server.Data;

/// <summary>
/// Prepares the database at application startup. Creates the database file when it does not exist
/// and fills an empty database with test data. An existing database keeps its data between runs.
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>Creates and seeds the database. With <paramref name="reset"/> it deletes the existing database first.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app, bool reset)
    {
        // the database context is scoped, and no request exists yet at startup
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // start from nothing when the user asks for a reset
        if (reset)
        {
            await db.Database.EnsureDeletedAsync();
        }

        // create the database file if it does not exist
        await db.Database.EnsureCreatedAsync();

        // keep existing data, seed only an empty database
        if (await db.Clients.AnyAsync())
        {
            return;
        }

        // load the test data from the prepared file
        var seedPath = Path.Combine(app.Environment.ContentRootPath, app.Configuration["SeedDataPath"]!);
        await DbSeeder.SeedAsync(db, seedPath);
    }
}
