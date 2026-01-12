using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Provider for derived/computed channels from raw log data.
/// Examples: VibeMag, GroundSpeed, BatteryPower
/// </summary>
public interface IDerivedChannelProvider
{
    /// <summary>
    /// Gets all available derived channel definitions.
    /// </summary>
    IReadOnlyList<DerivedChannelDefinition> GetDerivedChannels();

    /// <summary>
    /// Computes a derived channel's values.
    /// </summary>
    /// <param name="channelKey">Derived channel key (e.g., "DERIVED.VibeMag")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Computed data points</returns>
    Task<List<LogDataPoint>> ComputeChannelAsync(
        string channelKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a channel key is a derived channel.
    /// </summary>
    bool IsDerivedChannel(string channelKey);

    /// <summary>
    /// Gets the source fields required for a derived channel.
    /// </summary>
    IReadOnlyList<string> GetSourceFields(string channelKey);
}

/// <summary>
/// Definition of a derived channel.
/// </summary>
public class DerivedChannelDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Category { get; set; } = "Derived";
    public IReadOnlyList<string> SourceFields { get; set; } = Array.Empty<string>();
    public Func<Dictionary<string, double>, double>? ComputeFunction { get; set; }
}
