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
        {
          "categories": [
            { "code": "KAT_000001", "name": "Electronics" },
            { "code": "KAT_000002", "name": "Office" },
            { "code": "KAT_000003", "name": "Garden" }
          ],
          "products": [
            { "code": "PROD_000001", "name": "Mouse", "categories": ["KAT_000001", "KAT_000002"] },
            { "code": "PROD_000002", "name": "Paper", "categories": ["KAT_000002"] },
            { "code": "PROD_000003", "name": "Hose", "categories": ["KAT_000003"] }
          ],
          "clients": [
            { "name": "Alpha", "categories": ["KAT_000001"], "stock": { "PROD_000001": 5 } },
            { "name": "Beta", "categories": ["KAT_000001", "KAT_000002"], "stock": { "PROD_000001": 7 } },
            { "name": "Gamma", "categories": ["KAT_000003"], "stock": { "PROD_000003": 9 } }
          ]
        }
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("stock-manager-tests-").FullName;

    /// <summary>Writes the seed file for the application hosts started by this instance.</summary>
    public TestDatabase(string seed = DefaultSeed)
    {
        File.WriteAllText(SeedPath, seed);
    }

    private string SeedPath => Path.Combine(_folder, "seed.json");

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
