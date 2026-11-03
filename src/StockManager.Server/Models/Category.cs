namespace StockManager.Server.Models;

/// <summary>A product category. Connects products with the clients that work with them.</summary>
public sealed class Category
{
    public int Id { get; set; }

    /// <summary>Stable reference to the category in the form <c>KAT_000001</c>. The database generates it from the id and it cannot be changed.</summary>
    public string Code { get; private set; } = null!;

    public required string Name { get; set; }

    public ICollection<Client> Clients { get; } = [];

    public ICollection<Product> Products { get; } = [];
}
