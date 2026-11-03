namespace StockManager.Contracts;

/// <summary>One option in a filter menu: a client, a category or a product.</summary>
public sealed record LookupDto(int Id, string Name);
