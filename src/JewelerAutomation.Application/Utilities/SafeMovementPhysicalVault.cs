using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Utilities;

/// <summary>
/// Fiziki kasa (SafeMovements) toplamı: manuel hareketler + sepetten üretilen satırlar aynı tabloda;
/// defter (Ledger) ile sapma olmasın diye pano "brüt" değeri buradan türetilir.
/// </summary>
public static class SafeMovementPhysicalVault
{
    /// <summary>
    /// Kümülatif fiziki has etkisi. <see cref="SafeMovementType.ProfitRealization"/> kasa stokuna dahil edilmez.
    /// Sepet kaynaklı satırlarda <see cref="SafeMovement.HasGram"/> zaten işaretli (satışta negatif) tutulur.
    /// </summary>
    public static decimal GetSignedHasGramContribution(SafeMovement sm)
    {
        if (sm.MovementType == SafeMovementType.ProfitRealization)
            return 0m;

        if (sm.SourceTransactionId.HasValue)
            return sm.HasGram;

        return sm.MovementType switch
        {
            SafeMovementType.Income => sm.HasGram,
            SafeMovementType.Expense => -Math.Abs(sm.HasGram),
            SafeMovementType.Capital => sm.HasGram,
            SafeMovementType.Transfer => sm.HasGram,
            SafeMovementType.LinkingProfit => sm.HasGram,
            _ => 0m
        };
    }
}
