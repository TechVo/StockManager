namespace StockManager.Contracts;

/// <summary>
/// Detail of a client, a category or a product. <see cref="CategoryCodes"/> contains the assigned categories
/// and is empty for a category, because a category has no categories.
/// </summary>
public sealed record RecordDetailDto(string Code, string Name, IReadOnlyList<string> CategoryCodes);
