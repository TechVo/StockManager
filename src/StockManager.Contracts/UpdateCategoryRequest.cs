using System.ComponentModel.DataAnnotations;

namespace StockManager.Contracts;

/// <summary>JSON body of <c>PUT /api/categories/{code}</c>.</summary>
public sealed record UpdateCategoryRequest
{
    /// <summary>Longest name of a record.</summary>
    public const int MaxNameLength = 200;

    [Required]
    [StringLength(MaxNameLength)]
    public required string Name { get; init; }
}
