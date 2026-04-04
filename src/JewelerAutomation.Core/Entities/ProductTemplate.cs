namespace JewelerAutomation.Core.Entities;

/// <summary>Sepet satırında hızlı seçim için ürün şablonu (22 ayar bilezik vb.).</summary>
public class ProductTemplate : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Satış satırında şablon seçilince uygulanacak milyem (ör. 0,923).</summary>
    public decimal MilyemSatis { get; set; }

    /// <summary>Alış satırında şablon seçilince uygulanacak milyem (ör. 0,916).</summary>
    public decimal MilyemAlis { get; set; }

    /// <summary>Varsayılan ağırlık (gr); şablon seçildiğinde sepet satırına yazılır (ör. Çeyrek 1,75).</summary>
    public decimal DefaultGram { get; set; }

    /// <summary>Varsayılan birim işçilik (TL).</summary>
    public decimal DefaultLaborPrice { get; set; }

    public string? Category { get; set; }
}
