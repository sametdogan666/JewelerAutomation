namespace JewelerAutomation.Core.Entities;

/// <summary>İşlem kaydı türü (liste ve defter ayrımı).</summary>
public enum TransactionKind
{
    /// <summary>Altın sepeti / fatura.</summary>
    StandardBasket = 0,

    /// <summary>TL karşılığı saf döviz alış-satış (Borsa modu).</summary>
    ForexExchange = 1
}
