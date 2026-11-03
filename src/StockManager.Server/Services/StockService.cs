using Microsoft.EntityFrameworkCore;
using StockManager.Contracts;
using StockManager.Server.Data;

namespace StockManager.Server.Services;

/// <summary>
/// Application logic behind the REST API. Reads the options for the filter menu and the details of records,
/// reads the grid rows that match a filter and changes stock quantities.
/// </summary>
public sealed class StockService
{
    private readonly AppDbContext _db;

    /// <summary>Receives the database context of the current request.</summary>
    public StockService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns all clients as filter options, with their codes.</summary>
    public Task<List<ClientDto>> GetClientsAsync(CancellationToken cancellationToken) =>
        _db.Clients.Select(client => new ClientDto(client.Id, client.Code, client.Name)).ToListAsync(cancellationToken);

    /// <summary>Returns all categories as filter options, with their codes.</summary>
    public Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        _db.Categories.Select(category => new CategoryDto(category.Id, category.Code, category.Name)).ToListAsync(cancellationToken);

    /// <summary>Returns all products as filter options, with their codes.</summary>
    public Task<List<ProductDto>> GetProductsAsync(CancellationToken cancellationToken) =>
        _db.Products.Select(product => new ProductDto(product.Id, product.Code, product.Name)).ToListAsync(cancellationToken);

    /// <summary>Returns the detail of a client, or null if the code does not exist.</summary>
    public Task<RecordDetailDto?> GetClientDetailAsync(string code, CancellationToken cancellationToken) =>
        _db.Clients
            .Where(client => client.Code == code)
            .Select(client => new RecordDetailDto(client.Id, client.Code, client.Name, client.Categories.Select(category => category.Name).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Returns the detail of a category, or null if the code does not exist.</summary>
    public Task<RecordDetailDto?> GetCategoryDetailAsync(string code, CancellationToken cancellationToken) =>
        _db.Categories
            .Where(category => category.Code == code)
            .Select(category => new RecordDetailDto(category.Id, category.Code, category.Name, new List<string>()))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Returns the detail of a product, or null if the code does not exist.</summary>
    public Task<RecordDetailDto?> GetProductDetailAsync(string code, CancellationToken cancellationToken) =>
        _db.Products
            .Where(product => product.Code == code)
            .Select(product => new RecordDetailDto(product.Id, product.Code, product.Name, product.Categories.Select(category => category.Name).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Returns the grid rows that match the filter.</summary>
    public async Task<List<StockRowDto>> GetStockAsync(StockFilter filter, CancellationToken cancellationToken)
    {
        // start with all rows
        var items = _db.StockItems.AsQueryable();

        // limit the rows by client
        if (filter.ClientIds.Length > 0)
        {
            items = items.Where(item => filter.ClientIds.Contains(item.ClientId));
        }

        // limit the rows by product
        if (filter.ProductIds.Length > 0)
        {
            items = items.Where(item => filter.ProductIds.Contains(item.ProductId));
        }

        // a category counts only if the client works with it, the same rule as for the shown categories
        if (filter.CategoryIds.Length > 0)
        {
            items = items.Where(item => item.Product.Categories.Any(category =>
                filter.CategoryIds.Contains(category.Id) && category.Clients.Any(client => client.Id == item.ClientId)));
        }

        // load the categories too, because SQLite cannot compute their intersection inside the row query
        var rows = await items
            .Include(item => item.Client).ThenInclude(client => client.Categories)
            .Include(item => item.Product).ThenInclude(product => product.Categories)
            .AsSplitQuery()
            .AsNoTrackingWithIdentityResolution()
            .ToListAsync(cancellationToken);

        // the page sorts the rows, because SQLite does not sort Czech characters correctly
        return rows
            .Select(item => new StockRowDto(
                item.ClientId,
                item.Client.Name,
                item.ProductId,
                item.Product.Name,
                item.Product.Categories.Intersect(item.Client.Categories).Select(category => category.Name).ToList(),
                item.Quantity))
            .ToList();
    }

    /// <summary>Sets the quantity of a product at a client. Returns false if the client does not have this product.</summary>
    public async Task<bool> UpdateQuantityAsync(int clientId, int productId, int quantity, CancellationToken cancellationToken)
    {
        // update directly in the database, no row is changed if the client does not have this product
        var updatedRows = await _db.StockItems
            .Where(item => item.ClientId == clientId && item.ProductId == productId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Quantity, quantity), cancellationToken);

        return updatedRows > 0;
    }
}
