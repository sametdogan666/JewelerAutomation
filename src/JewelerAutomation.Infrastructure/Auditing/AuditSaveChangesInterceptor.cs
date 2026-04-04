using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using JewelerAutomation.Application.Interfaces;
using JewelerAutomation.Core.Entities;
using JewelerAutomation.Core.Enums;
using JewelerAutomation.Infrastructure.Data;

namespace JewelerAutomation.Infrastructure.Auditing;

/// <summary>
/// Yalnızca SaveChanges sırasında çalışır; GET dashboard/summary gibi salt-okuma uçları bu interceptor’ı tetiklemez.
/// Önceki koleksiyon-değişimi hatalarını önlemek için <see cref="AppendAuditEntries"/> anlık entry listesi kullanır.
/// </summary>
public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> SensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(User.PasswordHash),
        "PasswordHash",
        "Password",
        "SecurityStamp",
        "ConcurrencyStamp"
    };

    /// <summary>Defter satırları toplu yeniden üretilir (startup); denetimi şişirmemek için hariç.</summary>
    private static readonly HashSet<string> ExcludedEntityNames = new(StringComparer.Ordinal)
    {
        nameof(LedgerEntry),
        nameof(AuditLog),
        nameof(GoldRate),
        nameof(ProductTemplate),
    };

    private readonly ICurrentUserService _currentUser;

    public AuditSaveChangesInterceptor(ICurrentUserService currentUser)
        => _currentUser = currentUser;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is AppDbContext db)
            AppendAuditEntries(db);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is AppDbContext db)
            AppendAuditEntries(db);
        return await base.SavingChangesAsync(eventData, result, cancellationToken).ConfigureAwait(false);
    }

    private void AppendAuditEntries(AppDbContext db)
    {
        // Snapshot first: adding AuditLog entries mutates the change tracker — foreach must not enumerate live collection.
        var entries = db.ChangeTracker.Entries().ToList();
        if (!entries.Any(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted))
            return;

        var userId = _currentUser.UserId;
        var userName = _currentUser.UserName;
        var now = DateTime.UtcNow;
        var pendingLogs = new List<AuditLog>();

        foreach (var entry in entries)
        {
            if (entry.Entity is AuditLog)
                continue;

            if (entry.Entity is GoldRate)
                continue;

            if (entry.State is EntityState.Detached or EntityState.Unchanged)
                continue;

            if (entry.Metadata.IsOwned())
                continue;

            var entityName = entry.Metadata.ClrType.Name;
            if (ExcludedEntityNames.Contains(entityName))
                continue;
            var entityId = FormatEntityId(entry);

            switch (entry.State)
            {
                case EntityState.Added:
                    pendingLogs.Add(CreateLog(userId, userName, now, AuditAction.Insert, entityName, entityId,
                        oldJson: null,
                        newJson: Serialize(CurrentSnapshot(entry))));
                    break;

                case EntityState.Modified:
                    if (TryBuildSoftDeleteAudit(entry, out var delOld, out var delNew))
                    {
                        pendingLogs.Add(CreateLog(userId, userName, now, AuditAction.Delete, entityName, entityId,
                            Serialize(delOld), Serialize(delNew)));
                        break;
                    }

                    var changedProps = entry.Properties
                        .Where(p => p.IsModified && !IsSensitive(p.Metadata.Name))
                        .ToList();

                    if (changedProps.Count == 0)
                        break;

                    var oldVals = new Dictionary<string, object?>(StringComparer.Ordinal);
                    var newVals = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var p in changedProps)
                    {
                        oldVals[p.Metadata.Name] = NormalizeValue(p.OriginalValue);
                        newVals[p.Metadata.Name] = NormalizeValue(p.CurrentValue);
                    }

                    pendingLogs.Add(CreateLog(userId, userName, now, AuditAction.Update, entityName, entityId,
                        Serialize(oldVals), Serialize(newVals)));
                    break;

                case EntityState.Deleted:
                    pendingLogs.Add(CreateLog(userId, userName, now, AuditAction.Delete, entityName, entityId,
                        Serialize(OriginalSnapshot(entry)),
                        newJson: null));
                    break;
            }
        }

        foreach (var log in pendingLogs)
            db.AuditLogs.Add(log);
    }

    private static bool TryBuildSoftDeleteAudit(
        EntityEntry entry,
        out Dictionary<string, object?> oldSnap,
        out Dictionary<string, object?> newSnap)
    {
        oldSnap = new Dictionary<string, object?>(StringComparer.Ordinal);
        newSnap = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (entry.Entity is not ISoftDelete || entry.State != EntityState.Modified)
            return false;

        var isDeletedProp = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(ISoftDelete.IsDeleted));
        if (isDeletedProp is not { IsModified: true })
            return false;

        var wasDeleted = isDeletedProp.OriginalValue as bool? ?? false;
        var isDeleted = isDeletedProp.CurrentValue as bool? ?? false;
        if (wasDeleted || !isDeleted)
            return false;

        oldSnap = OriginalSnapshot(entry);
        foreach (var p in entry.Properties.Where(p => p.IsModified && !IsSensitive(p.Metadata.Name)))
            newSnap[p.Metadata.Name] = NormalizeValue(p.CurrentValue);

        return true;
    }

    private static AuditLog CreateLog(
        Guid? userId,
        string? userName,
        DateTime timestamp,
        AuditAction action,
        string entityName,
        string entityId,
        string? oldJson,
        string? newJson)
        => new()
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldJson,
            NewValues = newJson,
            Timestamp = timestamp
        };

    private static string FormatEntityId(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key == null)
            return string.Empty;

        var parts = new List<string?>();
        foreach (var prop in key.Properties)
        {
            var pe = entry.Property(prop.Name);
            var v = entry.State == EntityState.Deleted ? pe.OriginalValue : pe.CurrentValue;
            parts.Add(v?.ToString());
        }

        return string.Join("|", parts);
    }

    private static Dictionary<string, object?> CurrentSnapshot(EntityEntry entry)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in entry.Properties)
        {
            if (IsSensitive(p.Metadata.Name))
                continue;
            d[p.Metadata.Name] = NormalizeValue(p.CurrentValue);
        }

        return d;
    }

    private static Dictionary<string, object?> OriginalSnapshot(EntityEntry entry)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var p in entry.Properties)
        {
            if (IsSensitive(p.Metadata.Name))
                continue;
            d[p.Metadata.Name] = NormalizeValue(p.OriginalValue);
        }

        return d;
    }

    private static bool IsSensitive(string propertyName)
        => SensitivePropertyNames.Contains(propertyName);

    private static object? NormalizeValue(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dt => (dt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                : dt.ToUniversalTime()).ToString("O"),
            DateTimeOffset dto => dto.ToUniversalTime().ToString("O"),
            Guid g => g.ToString("D"),
            byte[] => "[binary]",
            _ => value
        };
    }

    private static string? Serialize(Dictionary<string, object?>? dict)
    {
        if (dict == null || dict.Count == 0)
            return null;
        return JsonSerializer.Serialize(dict, JsonOptions);
    }
}
