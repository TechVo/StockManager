namespace StockManager.Server.Models;

/// <summary>A client with its own stock, limited to the categories the client works with.</summary>
public sealed class Client
{
    public int Id { get; set; }

    /// <summary>Stable reference to the client. The database generates it from the id and it cannot be changed.</summary>
    public string Code { get; private set; } = null!;

    public required string Name { get; set; }

    public ICollection<Category> Categories { get; } = [];
}
