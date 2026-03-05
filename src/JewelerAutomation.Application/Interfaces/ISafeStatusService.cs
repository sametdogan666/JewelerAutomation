namespace JewelerAutomation.Application.Interfaces;

public interface ISafeStatusService
{
    Task<SafeStatus> GetSafeStatusAsync(CancellationToken cancellationToken = default);
}

public record SafeStatus(
    decimal GoldBalance,
    decimal CashBalance,
    decimal ExpectedGold,
    decimal ActualGold,
    decimal GoldShortage
);
