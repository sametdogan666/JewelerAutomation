namespace JewelerAutomation.Application.Options;

/// <summary>
/// Last-resort rates when live API and persisted daily rates cannot supply a HAS mid (e.g. empty DB).
/// Logged via ILogger only — never written to audit tables.
/// </summary>
public sealed class GoldRateFallbackOptions
{
    public const string SectionName = "GoldRateFallback";

    /// <summary>TL per gram HAS mid used for dashboard math when no other source exists.</summary>
    public decimal DefaultHasTryPerGramMid { get; set; } = 2500m;

    public decimal DefaultUsdTryMid { get; set; } = 33m;

    /// <summary>When false, dashboard leaves rates null/zeros instead of injecting defaults.</summary>
    public bool UseDefaultsWhenUnavailable { get; set; } = true;
}
