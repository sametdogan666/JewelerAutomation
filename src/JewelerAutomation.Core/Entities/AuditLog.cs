using JewelerAutomation.Core.Enums;

namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Değişiklik denetimi. ISoftDelete kullanılmaz; doğrudan kalıcı kayıt.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Anonim veya arka plan işlemleri için null olabilir.</summary>
    public Guid? UserId { get; set; }

    /// <summary>JWT / sistem bağlamından.</summary>
    public string? UserName { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>CLR tip adı (örn. TransactionItem).</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>Birincil anahtar(lar); bileşik anahtarlarda "|" ile birleştirilir.</summary>
    public string EntityId { get; set; } = string.Empty;

    /// <summary>JSON: alan adı → eski değer.</summary>
    public string? OldValues { get; set; }

    /// <summary>JSON: alan adı → yeni değer.</summary>
    public string? NewValues { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
