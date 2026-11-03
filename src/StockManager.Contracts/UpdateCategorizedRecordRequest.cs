using System.ComponentModel.DataAnnotations;

namespace StockManager.Contracts;

/// <summary>
/// JSON body of <c>PUT /api/clients/{code}</c> and <c>PUT /api/products/{code}</c>.
/// <see cref="CategoryCodes"/> replaces all categories of the record.
/// </summary>
public sealed record UpdateCategorizedRecordRequest
{
    [Required]
    [StringLength(UpdateCategoryRequest.MaxNameLength)]
    public required string Name { get; init; }

    [Required]
    public required IReadOnlyList<string> CategoryCodes { get; init; }
}
