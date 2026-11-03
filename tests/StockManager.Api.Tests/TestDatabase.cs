using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace StockManager.Api.Tests;

/// <summary>
/// Temporary folder with a database file and a seed file for one test.
/// Starts application hosts against these files, and more than one host can use the same database one after another.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    /// <summary>
    /// Small data set for the tests. Mouse has categories Electronics and Office. Alpha works with Electronics only,
    /// Beta with Electronics and Office, Gamma with Garden only.
    /// </summary>
    public const string DefaultSeed = """
        INSERT INTO Categories (Id, Name) VALUES (1, 'Electronics'), (2, 'Office'), (3, 'Garden');
        INSERT INTO Products (Id, Name) VALUES (1, 'Mouse'), (2, 'Paper'), (3, 'Hose');
        INSERT INTO Clients (Id, Name) VALUES (1, 'Alpha'), (2, 'Beta'), (3, 'Gamma');
        INSERT INTO CategoryProduct (CategoriesId, ProductsId) VALUES (1, 1), (2, 1), (2, 2), (3, 3);
        INSERT INTO CategoryClient (CategoriesId, ClientsId) VALUES (1, 1), (1, 2), (2, 2), (3, 3);
        INSERT INTO StockItems (ClientId, ProductId, Quantity) VALUES (1, 1, 5), (2, 1, 7), (2, 2, 0), (3, 3, 9);
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("stock-manager-tests-").FullName;

    /// <summary>Writes the seed file for the application hosts started by this instance.</summary>
    public TestDatabase(string seed = DefaultSeed)
    {
        File.WriteAllText(SeedPath, seed);
    }

    private string SeedPath => Path.Combine(_folder, "seed.sql");

    /// <summary>Replaces the seed file for the application hosts started later.</summary>
    public void ReplaceSeed(string seed)
    {
        File.WriteAllText(SeedPath, seed);
    }

    /// <summary>Starts a new application host against the database of this instance.</summary>
    public WebApplicationFactory<Program> StartHost() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // pooling is off, so that the database file can be deleted after the test
            builder.UseSetting("ConnectionStrings:Stock", $"Data Source={Path.Combine(_folder, "stock.db")};Pooling=False");
            builder.UseSetting("SeedDataPath", SeedPath);
        });

    /// <inheritdoc />
    public void Dispose()
    {
        Directory.Delete(_folder, recursive: true);
    }
}
