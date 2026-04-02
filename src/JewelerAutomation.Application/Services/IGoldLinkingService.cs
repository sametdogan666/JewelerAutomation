using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Services;

public record FifoLinkingSimulationResult(
    decimal TargetAmountGram,
    decimal TargetPricePerGram,
    decimal EstimatedProfitTl,
    decimal OpenHasPositionGram,
    bool SufficientOpenPosition
);

public record LinkingProcessResultDto(
    Guid Id,
    DateTime LinkingDate,
    decimal TargetAmount,
    decimal TargetPrice,
    decimal TotalProfit,
    Guid? SafeMovementId,
    string? Notes
);

public record LinkingProcessListItemDto(
    Guid Id,
    DateTime LinkingDate,
    decimal TargetAmount,
    decimal TargetPrice,
    decimal TotalProfit,
    Guid? SafeMovementId,
    string? Notes
);

public interface IGoldLinkingService
{
    /// <summary>Satış işleminden sonra FIFO için GoldTransaction satırları oluşturur.</summary>
    Task RegisterSaleGoldPositionsAsync(Transaction transaction, CancellationToken cancellationToken = default);

    /// <summary>İşleme ait tüm GoldTransaction kayıtlarını kaldırır (kısmi bağlantı yoksa).</summary>
    Task RemoveGoldTransactionsForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<bool> HasPartialLinkForTransactionAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <param name="periodStart">Dönem başı (opsiyonel); ikisi de doluysa sadece bu aralıktaki satışlar.</param>
    Task<decimal> GetOpenHasPositionAsync(
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        CancellationToken cancellationToken = default);

    Task<FifoLinkingSimulationResult> SimulateFifoLinkingAsync(
        decimal targetAmountGram,
        decimal targetPricePerGram,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        CancellationToken cancellationToken = default);

    Task<LinkingProcessResultDto> ProcessPartialLinkingAsync(
        decimal targetAmountGram,
        decimal targetPricePerGram,
        string? notes,
        DateTime? periodStart = null,
        DateTime? periodEnd = null,
        CancellationToken cancellationToken = default);

    Task CancelLinkingAsync(Guid linkingProcessId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LinkingProcessListItemDto>> GetLinkingHistoryAsync(CancellationToken cancellationToken = default);
}
