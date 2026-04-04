namespace JewelerAutomation.Infrastructure.GoldRates;

/// <summary>
/// Harem birincil kaynağı için basit devre kesici: ardışık hatalardan sonra kısa süre atlanır.
/// </summary>
public interface IGoldRateCircuitBreaker
{
    bool IsHaremOpen { get; }

    void RecordHaremSuccess();

    void RecordHaremFailure();
}

public sealed class GoldRateCircuitBreaker : IGoldRateCircuitBreaker
{
    private readonly object _sync = new();
    private int _failures;
    private DateTime _openUntilUtc = DateTime.MinValue;

    private const int FailureThreshold = 3;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromMinutes(2);

    public bool IsHaremOpen
    {
        get
        {
            lock (_sync)
                return DateTime.UtcNow < _openUntilUtc;
        }
    }

    public void RecordHaremSuccess()
    {
        lock (_sync)
        {
            _failures = 0;
            _openUntilUtc = DateTime.MinValue;
        }
    }

    public void RecordHaremFailure()
    {
        lock (_sync)
        {
            _failures++;
            if (_failures >= FailureThreshold)
                _openUntilUtc = DateTime.UtcNow.Add(OpenDuration);
        }
    }
}
