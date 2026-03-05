namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Stok kartı / envanter. Toplam Has Gram veya ürün bazlı stok takibi.
/// Bakiye: girişler - çıkışlar (Transaction/SafeMovement ile güncellenebilir).
/// </summary>
public class Inventory : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
    /// <summary> Toplam gram (stok miktarı) - decimal(18,6) </summary>
    public decimal TotalQuantity { get; set; }
    /// <summary> Ortalama veya sabit milyem - decimal(18,6) </summary>
    public decimal Milyem { get; set; }
    /// <summary> Toplam Has Gram = TotalQuantity * Milyem / 1000 (veya hareketlerden hesaplanır) - decimal(18,6) </summary>
    public decimal TotalHasGram { get; set; }
    public DateTime? LastMovementAt { get; set; }
}
