using JewelerAutomation.Application.Interfaces;

namespace JewelerAutomation.Application.Services;

public class SafeStatusService : ISafeStatusService
{
    private readonly IPeggingService _pegging;

    public SafeStatusService(IPeggingService pegging)
    {
        _pegging = pegging;
    }

    public Task<SafeStatus> GetSafeStatusAsync(CancellationToken cancellationToken = default)
        => _pegging.ComputeDashboardSafeStatusAsync(cancellationToken);
}
