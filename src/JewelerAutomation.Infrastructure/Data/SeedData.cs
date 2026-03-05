using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using JewelerAutomation.Application.Services;
using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Infrastructure.Data;

public static class SeedData
{
    /// <summary>
    /// Admin kullanıcısı (UserName: admin). Şifre hash'i BCrypt ile.
    /// Başlangıç şifresi: Admin123!
    /// </summary>
    public static async Task SeedAdminUserAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (await db.Users.AnyAsync(u => u.NormalizedUserName == "ADMIN").ConfigureAwait(false))
        {
            logger.LogInformation("Admin user already exists.");
            return;
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!");
        var admin = new User
        {
            UserName = "admin",
            NormalizedUserName = "ADMIN",
            PasswordHash = passwordHash,
            Role = "Admin",
            IsActive = true
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync().ConfigureAwait(false);
        logger.LogInformation("Admin user created. Default password: Admin123!");
    }

    /// <summary>
    /// Örnek cari ve kasa hareketleri. Sadece tablolar boşsa eklenir.
    /// </summary>
    public static async Task SeedSampleDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var accounting = scope.ServiceProvider.GetRequiredService<IAccountingService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        if (await db.Customers.AnyAsync().ConfigureAwait(false))
        {
            logger.LogInformation("Sample data already exists (Customers).");
            return;
        }

        var now = DateTime.UtcNow;
        var baseDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-2);

        // Örnek cariler
        var customers = new List<Customer>
        {
            new() { Name = "Mehmet Bülbül", Phone = "532 111 22 33", Type = CustomerType.Sahis, Description = "Şahıs borç/alacak" },
            new() { Name = "Mustafa Kaynakyeşil", Phone = "533 444 55 66", Type = CustomerType.Sahis },
            new() { Name = "Abdullah Bayır", Phone = "534 777 88 99", Type = CustomerType.Cari, Description = "Tedarikçi" },
            new() { Name = "Ahmet Doğan", Phone = "535 000 11 22", Type = CustomerType.Cari },
            new() { Name = "Emrullah Akbaş", Type = CustomerType.Cari },
            new() { Name = "Sadık Kaçamaz", Phone = "536 333 44 55", Type = CustomerType.Sahis },
        };
        foreach (var c in customers)
            db.Customers.Add(c);
        await db.SaveChangesAsync().ConfigureAwait(false);

        // Örnek kasa hareketleri (ana sermaye + birkaç gelir/gider)
        var safeMovements = new List<SafeMovement>
        {
            new()
            {
                TransactionDate = baseDate.AddDays(5),
                Gram = 1735.44m,
                Milyem = 1000m,
                HasGram = accounting.CalculateHasGram(1735.44m, 1000m),
                Description = "Açılış sermayesi",
                MovementType = SafeMovementType.Capital
            },
            new()
            {
                TransactionDate = baseDate.AddDays(15),
                Gram = 22.11m,
                Milyem = 1000m,
                HasGram = accounting.CalculateHasGram(22.11m, 1000m),
                Description = "Nakit giriş",
                MovementType = SafeMovementType.Income
            },
            new()
            {
                TransactionDate = baseDate.AddDays(28),
                Gram = 33.22m,
                Milyem = 1000m,
                HasGram = accounting.CalculateHasGram(33.22m, 1000m),
                Description = "Haftalık gelir",
                MovementType = SafeMovementType.Income
            },
            new()
            {
                TransactionDate = baseDate.AddDays(35),
                Gram = -398.44m,
                Milyem = 1000m,
                HasGram = accounting.CalculateHasGram(-398.44m, 1000m),
                Description = "Araba parası",
                MovementType = SafeMovementType.Expense
            },
            new()
            {
                TransactionDate = baseDate.AddMonths(1).AddDays(10),
                Gram = 34.38m,
                Milyem = 1000m,
                HasGram = accounting.CalculateHasGram(34.38m, 1000m),
                Description = "3 hafta gelir",
                MovementType = SafeMovementType.Income
            },
            new()
            {
                TransactionDate = baseDate.AddMonths(1).AddDays(18),
                Gram = -22.95m,
                Milyem = 1000m,
                HasGram = accounting.CalculateHasGram(-22.95m, 1000m),
                Description = "Aylık gider",
                MovementType = SafeMovementType.Expense
            },
        };
        foreach (var m in safeMovements)
            db.SafeMovements.Add(m);
        await db.SaveChangesAsync().ConfigureAwait(false);

        logger.LogInformation("Sample data seeded: {CustomerCount} customers, {SafeCount} safe movements.", customers.Count, safeMovements.Count);
    }
}
