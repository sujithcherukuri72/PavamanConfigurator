using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

public interface IAirframeService
{
    Task<AirframeSettings?> GetAirframeSettingsAsync();
    Task<bool> UpdateAirframeSettingsAsync(AirframeSettings settings);
}
