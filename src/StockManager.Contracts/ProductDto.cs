namespace StockManager.Contracts;

/// <summary>
/// One product returned by <c>GET /api/products</c>. <see cref="Code"/> is the stable reference to the product
/// and does not change when <see cref="Name"/> changes.
/// </summary>
public sealed record ProductDto(int Id, string Code, string Name);
