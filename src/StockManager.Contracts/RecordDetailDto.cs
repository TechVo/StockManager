namespace StockManager.Contracts;

/// <summary>
/// Detail of a client, a category or a product. <see cref="Id"/> selects the stock rows of the record,
/// and <see cref="Categories"/> contains its categories and is empty for a category.
/// </summary>
public sealed record RecordDetailDto(int Id, string Code, string Name, IReadOnlyList<CategoryDto> Categories);
