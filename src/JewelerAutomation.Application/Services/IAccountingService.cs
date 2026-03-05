namespace JewelerAutomation.Application.Services;

/// <summary>
/// Excel'deki formüllere dayalı kuyumculuk hesaplamaları (Has Gram, Milyem, İşçilik).
/// Tüm sayısal sonuçlar decimal(18,6) hassasiyetinde tutulmalıdır.
/// </summary>
public interface IAccountingService
{
    /// <summary>
    /// Toplam İşçilik = ±(Adet * Birimİşçilik * 0.01).
    /// Excel: F = -(D*E*0.01) veya (D*E*0.01)
    /// </summary>
    decimal CalculateTotalLabour(int pieceCount, decimal unitLabour, bool subtract = true);

    /// <summary>
    /// Has Gram (satış, işçilik dahil): (Miktar * Milyem / 1000) + Toplamİşçilik.
    /// Excel: G = (B*C*0.001)+F
    /// </summary>
    decimal CalculateHasGramWithLabour(decimal quantity, decimal milyem, decimal totalLabour);

    /// <summary>
    /// Has Gram (sade, işçiliksiz): Miktar * Milyem / 1000.
    /// Excel: Alış Has O = (M*N*0.001); Kasa D = B*C*0.001; Cari/Şahıs GR-HAS = (B*C*0.001)
    /// </summary>
    decimal CalculateHasGram(decimal quantity, decimal milyem);

    /// <summary>
    /// Milyem İşçilik (916 üzeri fazlalık gram): Milyem > 916 ise (Milyem - 916) * Miktar * 0.001, değilse 0.
    /// Excel: J = IF(C>916, (C-916)*B*0.001, 0)
    /// </summary>
    decimal CalculateMilyemLabour(decimal quantity, decimal milyem, bool onlyWhenAlindi = false);

    /// <summary>
    /// Cari/Şahıs hareket yönü: Miktar > 0 => Verildi, Miktar < 0 => Alındı, HasGram = 0 => Boş.
    /// Excel: I = IF(G=0,"BOŞ", IF(B>0,"VERİLDİ","ALINDI"))
    /// </summary>
    Core.Entities.MovementDirection GetMovementDirection(decimal quantity, decimal hasGram);
}
