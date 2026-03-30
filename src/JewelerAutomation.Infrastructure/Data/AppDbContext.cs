using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerMovement> CustomerMovements => Set<CustomerMovement>();
    public DbSet<CustomerTransaction> CustomerTransactions => Set<CustomerTransaction>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<SafeMovement> SafeMovements => Set<SafeMovement>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<CashPeggingLog> CashPeggingLogs => Set<CashPeggingLog>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<CashToGoldConversion> CashToGoldConversions => Set<CashToGoldConversion>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var correlationIdsToDelete = new HashSet<Guid>();

        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = DateTime.UtcNow;

            Guid? corrId = entry.Entity switch
            {
                CashPeggingLog p when p.CorrelationId != Guid.Empty => p.CorrelationId,
                Transaction t => t.CorrelationId,
                SafeMovement m => m.CorrelationId,
                LedgerEntry le => le.CorrelationId,
                _ => null
            };

            if (corrId.HasValue && corrId.Value != Guid.Empty)
                correlationIdsToDelete.Add(corrId.Value);
        }

        if (correlationIdsToDelete.Count > 0)
            await CascadeSoftDeleteByCorrelationAsync(correlationIdsToDelete, cancellationToken);

        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task CascadeSoftDeleteByCorrelationAsync(
        IReadOnlySet<Guid> correlationIds, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        var linkedPeggingLogs = await CashPeggingLogs
            .IgnoreQueryFilters()
            .Where(p => correlationIds.Contains(p.CorrelationId)
                        && !p.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var p in linkedPeggingLogs)
        {
            p.IsDeleted = true;
            p.DeletedAt = now;
        }

        var linkedTransactions = await Transactions
            .IgnoreQueryFilters()
            .Where(t => t.CorrelationId.HasValue
                        && correlationIds.Contains(t.CorrelationId.Value)
                        && !t.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var tx in linkedTransactions)
        {
            tx.IsDeleted = true;
            tx.DeletedAt = now;
        }

        var linkedMovements = await SafeMovements
            .IgnoreQueryFilters()
            .Where(m => m.CorrelationId.HasValue
                        && correlationIds.Contains(m.CorrelationId.Value)
                        && !m.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var mv in linkedMovements)
        {
            mv.IsDeleted = true;
            mv.DeletedAt = now;
        }

        var linkedLedger = await LedgerEntries
            .IgnoreQueryFilters()
            .Where(le => le.CorrelationId.HasValue
                         && correlationIds.Contains(le.CorrelationId.Value)
                         && !le.IsDeleted)
            .ToListAsync(cancellationToken);

        foreach (var le in linkedLedger)
        {
            le.IsDeleted = true;
            le.DeletedAt = now;
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplySoftDeleteFilters(modelBuilder);

        const int precision = 18;
        const int scale = 6;

        modelBuilder.Entity<User>(e =>
        {
            e.HasIndex(x => x.NormalizedUserName).IsUnique();
            e.Property(x => x.UserName).HasMaxLength(256);
            e.Property(x => x.NormalizedUserName).HasMaxLength(256);
            e.Property(x => x.Role).HasMaxLength(64);
        });

        modelBuilder.Entity<Customer>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(256);
            e.Property(x => x.Phone).HasMaxLength(64);
        });

        modelBuilder.Entity<CustomerMovement>(e =>
        {
            e.HasOne(x => x.Customer).WithMany(x => x.Movements).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Quantity).HasPrecision(precision, scale);
            e.Property(x => x.Milyem).HasPrecision(precision, scale);
            e.Property(x => x.UnitLabour).HasPrecision(precision, scale);
            e.Property(x => x.TotalLabour).HasPrecision(precision, scale);
            e.Property(x => x.HasGram).HasPrecision(precision, scale);
            e.Property(x => x.MilyemLabour).HasPrecision(precision, scale);
        });

        modelBuilder.Entity<CustomerTransaction>(e =>
        {
            e.HasOne(x => x.Customer).WithMany(x => x.AccountTransactions).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.GoldGram).HasPrecision(precision, scale);
            e.Property(x => x.GoldMilyem).HasPrecision(precision, scale);
            e.Property(x => x.GoldHas).HasPrecision(precision, scale);
            e.Property(x => x.CashAmount).HasPrecision(precision, scale);
            e.Property(x => x.Description).HasMaxLength(512);
        });

        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Items).WithOne(x => x.Transaction).HasForeignKey(x => x.TransactionId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Quantity).HasPrecision(precision, scale);
            e.Property(x => x.Milyem).HasPrecision(precision, scale);
            e.Property(x => x.TotalLabour).HasPrecision(precision, scale);
            e.Property(x => x.HasGram).HasPrecision(precision, scale);
            e.Property(x => x.Price).HasPrecision(precision, scale);
            e.Property(x => x.MilyemLabour).HasPrecision(precision, scale);
            e.Property(x => x.UnitLabour).HasPrecision(precision, scale);
            e.Property(x => x.NetHasGram).HasPrecision(precision, scale);
            e.Property(x => x.NetCashAmount).HasPrecision(precision, scale);
            e.HasIndex(x => x.CorrelationId);
        });

        modelBuilder.Entity<TransactionItem>(e =>
        {
            e.Property(x => x.Quantity).HasPrecision(precision, scale);
            e.Property(x => x.Milyem).HasPrecision(precision, scale);
            e.Property(x => x.TotalLabour).HasPrecision(precision, scale);
            e.Property(x => x.HasGram).HasPrecision(precision, scale);
            e.Property(x => x.Price).HasPrecision(precision, scale);
            e.Property(x => x.MilyemLabour).HasPrecision(precision, scale);
            e.Property(x => x.UnitLabour).HasPrecision(precision, scale);
        });

        modelBuilder.Entity<SafeMovement>(e =>
        {
            e.Property(x => x.Gram).HasPrecision(precision, scale);
            e.Property(x => x.Milyem).HasPrecision(precision, scale);
            e.Property(x => x.HasGram).HasPrecision(precision, scale);
            e.HasIndex(x => x.CorrelationId);
        });

        modelBuilder.Entity<Inventory>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.TotalQuantity).HasPrecision(precision, scale);
            e.Property(x => x.Milyem).HasPrecision(precision, scale);
            e.Property(x => x.TotalHasGram).HasPrecision(precision, scale);
        });

        modelBuilder.Entity<CashPeggingLog>(e =>
        {
            e.Property(x => x.CashAmount).HasPrecision(precision, scale);
            e.Property(x => x.GoldPricePerGram).HasPrecision(precision, scale);
            e.Property(x => x.EquivalentHasGram).HasPrecision(precision, scale);
            e.Property(x => x.PhysicalGoldAtTime).HasPrecision(precision, scale);
            e.Property(x => x.TotalCapitalHasGram).HasPrecision(precision, scale);
            e.Property(x => x.TransactionProfitHasGram).HasPrecision(precision, scale);
            e.Property(x => x.ExchangeRateProfitHasGram).HasPrecision(precision, scale);
            e.Property(x => x.NetProfitHasGram).HasPrecision(precision, scale);
            e.Property(x => x.Notes).HasMaxLength(1024);
            e.HasIndex(x => x.PeggingDate);
            e.HasIndex(x => x.CorrelationId);
        });

        modelBuilder.Entity<LedgerEntry>(e =>
        {
            e.Property(x => x.GoldHasAmount).HasPrecision(precision, scale);
            e.Property(x => x.CashAmount).HasPrecision(precision, scale);
            e.Property(x => x.Description).HasMaxLength(512);
            e.HasIndex(x => x.TransactionDate);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
            e.HasIndex(x => x.CorrelationId);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CashToGoldConversion>(e =>
        {
            e.Property(x => x.CashAmount).HasPrecision(precision, scale);
            e.Property(x => x.HasPrice).HasPrecision(precision, scale);
            e.Property(x => x.ConvertedGoldHas).HasPrecision(precision, scale);
            e.Property(x => x.Description).HasMaxLength(512);
            e.HasIndex(x => x.TransactionDate);
            e.HasIndex(x => x.CustomerId);
            e.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ApplySoftDeleteFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                continue;

            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDelete.IsDeleted));
            var condition = Expression.Equal(property, Expression.Constant(false));
            var lambda = Expression.Lambda(condition, parameter);

            entityType.SetQueryFilter(lambda);
        }
    }
}
