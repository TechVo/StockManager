namespace StockManager.Server.Models;

/// <summary>A client with its own stock, limited to the categories the client works with.</summary>
public sealed class Client : ICategorizedRecord
{
    public int Id { get; set; }

    /// <summary>Stable reference to the client in the form <c>KLI_000001</c>. The database generates it from the id and it cannot be changed.</summary>
    public string Code { get; private set; } = null!;

    public required string Name { get; set; }

    public ICollection<Category> Categories { get; } = [];
}
