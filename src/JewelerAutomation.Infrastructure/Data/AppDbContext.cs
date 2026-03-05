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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // decimal(18,6) for all financial/weight columns
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
        });

        modelBuilder.Entity<Inventory>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(64);
            e.Property(x => x.TotalQuantity).HasPrecision(precision, scale);
            e.Property(x => x.Milyem).HasPrecision(precision, scale);
            e.Property(x => x.TotalHasGram).HasPrecision(precision, scale);
        });
    }
}
