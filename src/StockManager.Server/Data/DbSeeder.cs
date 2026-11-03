using System.Text.Json;
using StockManager.Contracts;
using StockManager.Server.Models;

namespace StockManager.Server.Data;

/// <summary>
/// Fills the database from a JSON file. The file gives a code to each category and product, and the other records refer to them by this code.
/// A category or a product gets the id from its code, so that the database generates exactly this code.
/// Clients get their ids and codes from the database in the order of the file.
/// Every product that shares a category with a client gets a stock row, with quantity 0 unless the file gives another value.
/// Invalid references in the file throw an exception, so that the application does not start with wrong data.
/// </summary>
public static class DbSeeder
{
    /// <summary>Reads the seed file and saves all its records in one transaction.</summary>
    public static async Task SeedAsync(AppDbContext db, string path)
    {
        // read the file
        await using var file = File.OpenRead(path);
        var seed = await JsonSerializer.DeserializeAsync<SeedData>(file, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Seed file '{path}' is empty.");

        // create the categories and the products in them
        var categories = seed.Categories.ToDictionary(category => category.Code, category => new Category { Id = GetId(category.Code, RecordCode.CategoryPrefix), Name = category.Name });
        var products = seed.Products.ToDictionary(product => product.Code, product => CreateProduct(product, categories));

        // add all records in the order of the file, also those that no client uses
        db.AddRange(categories.Values);
        db.AddRange(products.Values);

        // create the clients with their stock
        foreach (var client in seed.Clients)
        {
            // the client needs its categories before its stock can be created
            var entity = CreateClient(client, categories);
            db.Add(entity);
            db.AddRange(CreateStock(client, entity, products));
        }

        // save everything in one transaction
        await db.SaveChangesAsync();
    }

    /// <summary>Creates a product assigned to its categories.</summary>
    private static Product CreateProduct(SeedProduct seed, Dictionary<string, Category> categories)
    {
        var product = new Product { Id = GetId(seed.Code, RecordCode.ProductPrefix), Name = seed.Name };

        // connect the categories from the file
        foreach (var code in seed.Categories)
        {
            product.Categories.Add(FindCategory(code, categories));
        }

        return product;
    }

    /// <summary>Creates a client assigned to the categories it works with.</summary>
    private static Client CreateClient(SeedClient seed, Dictionary<string, Category> categories)
    {
        var client = new Client { Name = seed.Name };

        // connect the categories from the file
        foreach (var code in seed.Categories)
        {
            client.Categories.Add(FindCategory(code, categories));
        }

        return client;
    }

    /// <summary>Creates a stock row for each product of the client and checks the quantities in the file.</summary>
    private static List<StockItem> CreateStock(SeedClient seed, Client client, Dictionary<string, Product> products)
    {
        // the client sees only products from its categories
        var visibleProducts = products.Where(product => product.Value.Categories.Intersect(client.Categories).Any()).ToList();

        // a quantity for any other product is an error in the file
        var visibleCodes = visibleProducts.Select(product => product.Key).ToHashSet();
        var invalidCode = seed.Stock.Keys.FirstOrDefault(code => !visibleCodes.Contains(code));
        if (invalidCode is not null)
        {
            throw new InvalidOperationException($"Client '{seed.Name}' has stock of '{invalidCode}', which is not a product code in its categories.");
        }

        // a product without a quantity in the file has quantity 0
        return visibleProducts
            .Select(product => new StockItem { Client = client, Product = product.Value, Quantity = seed.Stock.GetValueOrDefault(product.Key) })
            .ToList();
    }

    /// <summary>Returns the id from which the database generates this code, or throws if the code has the wrong form.</summary>
    private static int GetId(string code, string prefix)
    {
        // the code must be exactly what the database generates from the number in it
        var number = code.StartsWith(prefix, StringComparison.Ordinal) ? code[prefix.Length..] : "";
        if (!int.TryParse(number, out var id) || id <= 0 || $"{prefix}{id:D6}" != code)
        {
            throw new InvalidOperationException($"Seed file has code '{code}', which is not in the form {prefix}000001.");
        }

        return id;
    }

    /// <summary>Returns the category with the given code, or throws if the file does not define it.</summary>
    private static Category FindCategory(string code, Dictionary<string, Category> categories) =>
        categories.TryGetValue(code, out var category)
            ? category
            : throw new InvalidOperationException($"Seed file refers to unknown category code '{code}'.");

    /// <summary>Content of the whole seed file.</summary>
    private sealed record SeedData(List<SeedCategory> Categories, List<SeedProduct> Products, List<SeedClient> Clients);

    /// <summary>One category in the seed file.</summary>
    private sealed record SeedCategory(string Code, string Name);

    /// <summary>One product in the seed file. Categories contains category codes.</summary>
    private sealed record SeedProduct(string Code, string Name, List<string> Categories);

    /// <summary>One client in the seed file. Categories contains category codes and stock contains quantities by product code.</summary>
    private sealed record SeedClient(string Name, List<string> Categories, Dictionary<string, int> Stock);
}
