using JewelerAutomation.Core.Entities;

namespace JewelerAutomation.Application.Interfaces;

public interface IPeggingService
{
    /// <summary>
    /// Brüt kasa ve net pozisyon dahil pano metrikleri (silinmiş kayıtlar hariç — EF filtreleri).
    /// </summary>
    Task<SafeStatus> ComputeDashboardSafeStatusAsync(CancellationToken cancellationToken = default);

    Task<UnifiedPeggingSimulationDto> SimulateUnifiedAsync(
        UnifiedPeggingSimulateRequest request,
        CancellationToken cancellationToken = default);

    Task<CashPeggingLog> CreateHybridPeggingAsync(
        DateTime periodStart,
        DateTime periodEnd,
        decimal goldPricePerGram,
        string? notes,
        Guid? userId,
        decimal? pegCashFromSafe,
        decimal? pegHasGram,
        CancellationToken cancellationToken = default);
}

public record UnifiedPeggingSimulateRequest(
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal GoldPricePerGram,
    decimal? PegCashFromSafe,
    decimal? PegHasGram,
    decimal? FifoTargetAmountGram);

public record UnifiedPeggingSimulationDto(
    PeggingSimulationResult Hybrid,
    FifoLinkingSimulationResult? Fifo,
    decimal OpenHasPositionInPeriodGram,
    decimal EstimatedOpenHasAfterHybridGram);
