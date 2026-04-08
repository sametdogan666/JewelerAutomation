namespace JewelerAutomation.Application.Interfaces;

/// <summary>Cari/şahıs hesap defteri bakiyesi (kasa defterinden ayrı).</summary>
public record CustomerBookBalances(
    decimal GoldHasGram,
    decimal CashTry,
    decimal CashUsd,
    decimal CashEur,
    decimal CashGbp);
