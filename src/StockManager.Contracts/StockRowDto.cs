namespace StockManager.Contracts;

/// <summary>
/// One grid row: the stock of one product at one client.
/// <see cref="Categories"/> contains only the categories of the product that the client works with.
/// </summary>
public sealed record StockRowDto(
    int ClientId,
    string ClientName,
    int ProductId,
    string ProductName,
    IReadOnlyList<string> Categories,
    int Quantity);
