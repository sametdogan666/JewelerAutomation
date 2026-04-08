namespace JewelerAutomation.Core.Entities;

/// <summary>Şahıs emanet sepet modu (yalnızca <see cref="Transaction.IsSahisEmanet"/>).</summary>
public enum SahisEmanetMode
{
    None = 0,
    /// <summary>Nakit kasaya girer; fiziki altın değişmez; şahıs altın alacaklı.</summary>
    EmanetSatis = 1,
    /// <summary>Fiziki kasaya altın girer; nakit yok; şahıs altın alacaklı.</summary>
    EmanetAlis = 2
}
