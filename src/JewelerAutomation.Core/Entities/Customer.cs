namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Cari veya Şahıs (BorçSorgulama) - Excel'deki Cariler ve BorçSorgulama sayfalarına karşılık gelir.
/// </summary>
public class Customer : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    /// <summary> Cari = tedarikçi/müşteri, Sahis = şahıs borç/alacak </summary>
    public CustomerType Type { get; set; } = CustomerType.Cari;
    public string? Description { get; set; }

    /// <summary>False = pasif (silinmiş); finansal geçmişi olan cariler fiziksel silinmez.</summary>
    public bool IsActive { get; set; } = true;

    public ICollection<CustomerMovement> Movements { get; set; } = new List<CustomerMovement>();
    public ICollection<CustomerTransaction> AccountTransactions { get; set; } = new List<CustomerTransaction>();
}

public enum CustomerType
{
    Cari = 0,
    Sahis = 1
}
