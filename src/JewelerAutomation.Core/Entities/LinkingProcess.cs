namespace JewelerAutomation.Core.Entities;

/// <summary>
/// Parçalı FIFO nakit bağlama işlemi özeti.
/// </summary>
public class LinkingProcess : BaseEntity
{
    public DateTime LinkingDate { get; set; }

    /// <summary>Bağlanan toplam has gram.</summary>
    public decimal TargetAmount { get; set; }

    /// <summary>Bağlama anındaki has fiyatı (TL/gram).</summary>
    public decimal TargetPrice { get; set; }

    /// <summary>Toplam tahmini kâr (TL).</summary>
    public decimal TotalProfit { get; set; }

    /// <summary>Kârın kasa hareketi (Nakit Bağlama Kârı); kâr yoksa null.</summary>
    public Guid? SafeMovementId { get; set; }
    public SafeMovement? SafeMovement { get; set; }

    public string? Notes { get; set; }

    public ICollection<LinkingDetail> Details { get; set; } = new List<LinkingDetail>();
}
