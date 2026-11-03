namespace StockManager.Contracts;

/// <summary>
/// One category returned by <c>GET /api/categories</c>. <see cref="Code"/> is the stable reference to the category
/// and does not change when <see cref="Name"/> changes.
/// </summary>
public sealed record CategoryDto(int Id, string Code, string Name);
