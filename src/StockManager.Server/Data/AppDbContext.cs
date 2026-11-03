using Microsoft.EntityFrameworkCore;
using StockManager.Contracts;
using StockManager.Server.Models;

namespace StockManager.Server.Data;

/// <summary>
/// EF Core access to the SQLite database file. Defines the clients, categories and products,
/// their many-to-many relations and the stock of each client.
/// </summary>
public sealed class AppDbContext : DbContext
{
    private const string CodeColumn = "Code";

    /// <summary>Receives the options configured in the DI setup.</summary>
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockItem> StockItems => Set<StockItem>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // a code identifies its row, so the Code column of every table is unique, also in tables added later
        var entityTypesWithCode = modelBuilder.Model.GetEntityTypes().Where(type => type.FindProperty(CodeColumn) is not null);
        foreach (var entityType in entityTypesWithCode.ToList())
        {
            modelBuilder.Entity(entityType.ClrType).HasIndex(CodeColumn).IsUnique();
        }

        // the database generates each code from the id, which is never used again, so a code cannot be duplicated or changed
        modelBuilder.Entity<Client>().Property(client => client.Code).HasComputedColumnSql(GetCodeSql(RecordCode.ClientPrefix), stored: false);
        modelBuilder.Entity<Category>().Property(category => category.Code).HasComputedColumnSql(GetCodeSql(RecordCode.CategoryPrefix), stored: false);
        modelBuilder.Entity<Product>().Property(product => product.Code).HasComputedColumnSql(GetCodeSql(RecordCode.ProductPrefix), stored: false);

        // names are unique, because the filter menu shows only the name
        modelBuilder.Entity<Client>().HasIndex(client => client.Name).IsUnique();
        modelBuilder.Entity<Category>().HasIndex(category => category.Name).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(product => product.Name).IsUnique();

        // each client has one quantity per product
        modelBuilder.Entity<StockItem>().HasKey(item => new { item.ClientId, item.ProductId });
    }

    /// <summary>Returns the SQL expression that generates a code from the prefix and the id.</summary>
    private static string GetCodeSql(string prefix) => $"'{prefix}' || printf('%06d', \"Id\")";
}
