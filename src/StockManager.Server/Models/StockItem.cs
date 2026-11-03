namespace StockManager.Server.Models;

/// <summary>
/// Quantity of one product at one client. A row exists for every product
/// that shares at least one category with the client, and for no other product.
/// </summary>
public sealed class StockItem
{
    public int ClientId { get; set; }

    public Client Client { get; set; } = null!;

    public int ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int Quantity { get; set; }
}
