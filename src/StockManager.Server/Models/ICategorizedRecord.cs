namespace StockManager.Server.Models;

/// <summary>A record with assigned categories, which decide what stock rows it has: a client or a product.</summary>
public interface ICategorizedRecord
{
    string Code { get; }

    string Name { get; set; }

    ICollection<Category> Categories { get; }
}
