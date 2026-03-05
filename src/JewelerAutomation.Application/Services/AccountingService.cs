using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

/// <summary>
/// Excel formüllerine birebir uyumlu kuyumculuk hesaplama servisi.
/// Hassasiyet: decimal(18,6) - tüm çarpım/bölümler bu hassasiyette.
/// </summary>
public class AccountingService : IAccountingService
{
    private const decimal MilyemFactor = 0.001m;   // / 1000
    private const decimal LabourFactor = 0.01m;     // %1 birim işçilik çarpanı
    private const decimal MilyemThreshold = 916m;   // 916 üstü fazlalık hesabı

    /// <inheritdoc />
    public decimal CalculateTotalLabour(int pieceCount, decimal unitLabour, bool subtract = true)
    {
        var labour = (decimal)pieceCount * unitLabour * LabourFactor;
        return subtract ? -labour : labour;
    }

    /// <inheritdoc />
    public decimal CalculateHasGramWithLabour(decimal quantity, decimal milyem, decimal totalLabour)
    {
        var hasFromPurity = quantity * milyem * MilyemFactor;
        return Math.Round(hasFromPurity + totalLabour, 6);
    }

    /// <inheritdoc />
    public decimal CalculateHasGram(decimal quantity, decimal milyem)
    {
        return Math.Round(quantity * milyem * MilyemFactor, 6);
    }

    /// <inheritdoc />
    public decimal CalculateMilyemLabour(decimal quantity, decimal milyem, bool onlyWhenAlindi = false)
    {
        if (milyem <= MilyemThreshold) return 0;
        return Math.Round((milyem - MilyemThreshold) * quantity * MilyemFactor, 6);
    }

    /// <inheritdoc />
    public MovementDirection GetMovementDirection(decimal quantity, decimal hasGram)
    {
        if (hasGram == 0) return MovementDirection.Empty;
        return quantity > 0 ? MovementDirection.Verildi : MovementDirection.Alindi;
    }
}
