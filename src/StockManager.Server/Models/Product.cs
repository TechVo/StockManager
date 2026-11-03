namespace StockManager.Server.Models;

/// <summary>A product shared by all clients. It can belong to more than one category.</summary>
public sealed class Product
{
    public int Id { get; set; }

    /// <summary>Stable reference to the product. The database generates it from the id and it cannot be changed.</summary>
    public string Code { get; private set; } = null!;

    public required string Name { get; set; }

    public ICollection<Category> Categories { get; } = [];
}
