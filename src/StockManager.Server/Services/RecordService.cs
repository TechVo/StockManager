using Microsoft.EntityFrameworkCore;
using StockManager.Contracts;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

/// <summary>Result type of a change of a record.</summary>
public enum RecordUpdateStatus
{
    Updated,
    NotFound,
    Invalid,
    Conflict,
}

/// <summary>Result of a change of a record. The message is shown to the user, so it is in Czech.</summary>
public sealed record RecordUpdateResult(RecordUpdateStatus Status, string? Message = null);

/// <summary>
/// Application logic behind the detail pages. Finds clients, categories and products by code and changes them.
/// The categories of a client or a product decide what stock rows exist, so a change of categories also adds and removes
/// stock rows. A change that would hide a stock row with a quantity is rejected, so that no quantity is lost without notice.
/// </summary>
public sealed class RecordService
{
    private readonly AppDbContext _db;

    /// <summary>Receives the database context of the current request.</summary>
    public RecordService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the detail of a client, or null if the code does not exist.</summary>
    public Task<RecordDetailDto?> GetClientAsync(string code, CancellationToken cancellationToken) =>
        GetCategorizedRecordAsync(_db.Clients, code, cancellationToken);

    /// <summary>Returns the detail of a product, or null if the code does not exist.</summary>
    public Task<RecordDetailDto?> GetProductAsync(string code, CancellationToken cancellationToken) =>
        GetCategorizedRecordAsync(_db.Products, code, cancellationToken);

    /// <summary>Returns the detail of a category, or null if the code does not exist.</summary>
    public Task<RecordDetailDto?> GetCategoryAsync(string code, CancellationToken cancellationToken) =>
        _db.Categories
            .Where(category => category.Code == code)
            .Select(category => new RecordDetailDto(category.Code, category.Name, new List<string>()))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Changes the name of a category.</summary>
    public Task<RecordUpdateResult> UpdateCategoryAsync(string code, UpdateCategoryRequest request, CancellationToken cancellationToken) =>
        SaveInTransactionAsync(async () =>
        {
            // the code must exist
            var category = await _db.Categories.FirstOrDefaultAsync(category => category.Code == code, cancellationToken);
            if (category is null)
            {
                return new RecordUpdateResult(RecordUpdateStatus.NotFound);
            }

            // the name must stay unique
            var name = request.Name.Trim();
            if (await _db.Categories.AnyAsync(other => other.Name == name && other.Code != code, cancellationToken))
            {
                return new RecordUpdateResult(RecordUpdateStatus.Conflict, DuplicateNameMessage);
            }

            category.Name = name;

            return new RecordUpdateResult(RecordUpdateStatus.Updated);
        }, cancellationToken);

    /// <summary>Changes the name and the categories of a client, and its stock rows to match the categories.</summary>
    public Task<RecordUpdateResult> UpdateClientAsync(string code, UpdateCategorizedRecordRequest request, CancellationToken cancellationToken) =>
        UpdateCategorizedRecordAsync(_db.Clients, code, request, async (client, categoryIds) => new StockChange(
            await _db.StockItems.Include(item => item.Product).Where(item => item.ClientId == client.Id).ToListAsync(cancellationToken),
            await _db.Products.Where(product => product.Categories.Any(category => categoryIds.Contains(category.Id))).Select(product => product.Id).ToListAsync(cancellationToken),
            item => item.ProductId,
            productId => new StockItem { ClientId = client.Id, ProductId = productId },
            item => item.Product.Name), cancellationToken);

    /// <summary>Changes the name and the categories of a product, and its stock rows to match the categories.</summary>
    public Task<RecordUpdateResult> UpdateProductAsync(string code, UpdateCategorizedRecordRequest request, CancellationToken cancellationToken) =>
        UpdateCategorizedRecordAsync(_db.Products, code, request, async (product, categoryIds) => new StockChange(
            await _db.StockItems.Include(item => item.Client).Where(item => item.ProductId == product.Id).ToListAsync(cancellationToken),
            await _db.Clients.Where(client => client.Categories.Any(category => categoryIds.Contains(category.Id))).Select(client => client.Id).ToListAsync(cancellationToken),
            item => item.ClientId,
            clientId => new StockItem { ClientId = clientId, ProductId = product.Id },
            item => item.Client.Name), cancellationToken);

    private const string DuplicateNameMessage = "Tento název už má jiný záznam.";

    /// <summary>Returns the detail of a client or a product, or null if the code does not exist.</summary>
    private static Task<RecordDetailDto?> GetCategorizedRecordAsync<T>(IQueryable<T> records, string code, CancellationToken cancellationToken)
        where T : class, ICategorizedRecord =>
        records
            .Where(record => record.Code == code)
            .Select(record => new RecordDetailDto(record.Code, record.Name, record.Categories.Select(category => category.Code).ToList()))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Applies the change shared by clients and products: the name, the categories and the stock rows.</summary>
    private Task<RecordUpdateResult> UpdateCategorizedRecordAsync<T>(
        IQueryable<T> records,
        string code,
        UpdateCategorizedRecordRequest request,
        Func<T, List<int>, Task<StockChange>> loadStockChange,
        CancellationToken cancellationToken)
        where T : class, ICategorizedRecord =>
        SaveInTransactionAsync(async () =>
        {
            // the code must exist
            var record = await records.Include(record => record.Categories).FirstOrDefaultAsync(record => record.Code == code, cancellationToken);
            if (record is null)
            {
                return new RecordUpdateResult(RecordUpdateStatus.NotFound);
            }

            // the name must stay unique
            var name = request.Name.Trim();
            if (await records.AnyAsync(other => other.Name == name && other.Code != code, cancellationToken))
            {
                return new RecordUpdateResult(RecordUpdateStatus.Conflict, DuplicateNameMessage);
            }

            // all categories must exist
            var categoryCodes = request.CategoryCodes.Distinct().ToList();
            var categories = await _db.Categories.Where(category => categoryCodes.Contains(category.Code)).ToListAsync(cancellationToken);
            if (categories.Count != categoryCodes.Count)
            {
                return new RecordUpdateResult(RecordUpdateStatus.Invalid, "Některá z kategorií neexistuje.");
            }

            // find the stock rows that the new categories hide
            var change = await loadStockChange(record, categories.Select(category => category.Id).ToList());
            var visibleIds = change.VisibleIds.ToHashSet();
            var hiddenItems = change.Items.Where(item => !visibleIds.Contains(change.GetOtherId(item))).ToList();

            // a quantity must not disappear without notice
            var itemsWithQuantity = hiddenItems.Where(item => item.Quantity > 0).Select(item => $"{change.Describe(item)} ({item.Quantity} ks)").ToList();
            if (itemsWithQuantity.Count > 0)
            {
                return new RecordUpdateResult(
                    RecordUpdateStatus.Conflict,
                    $"Změna by skryla sklad, který není prázdný: {string.Join(", ", itemsWithQuantity)}. Nejdřív vynulujte počty kusů.");
            }

            // remove the hidden rows and add a row with quantity 0 for each newly visible one
            var existingIds = change.Items.Select(change.GetOtherId).ToHashSet();
            _db.StockItems.RemoveRange(hiddenItems);
            _db.StockItems.AddRange(visibleIds.Where(id => !existingIds.Contains(id)).Select(change.Create));

            // change the record itself
            record.Name = name;
            record.Categories.Clear();
            foreach (var category in categories)
            {
                record.Categories.Add(category);
            }

            return new RecordUpdateResult(RecordUpdateStatus.Updated);
        }, cancellationToken);

    /// <summary>Runs the checks and the change in one transaction, and saves only a successful change.</summary>
    private async Task<RecordUpdateResult> SaveInTransactionAsync(Func<Task<RecordUpdateResult>> change, CancellationToken cancellationToken)
    {
        // the write lock is held from the first check, so that no other request can change the checked data
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var result = await change();

        // disposing the transaction without a commit changes nothing
        if (result.Status == RecordUpdateStatus.Updated)
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Data for the change of stock rows of one client or product. Items are its existing rows and VisibleIds are the records
    /// on the other side of the row (products of a client, clients of a product) that it shares a category with after the change.
    /// </summary>
    private sealed record StockChange(
        List<StockItem> Items,
        List<int> VisibleIds,
        Func<StockItem, int> GetOtherId,
        Func<int, StockItem> Create,
        Func<StockItem, string> Describe);
}
