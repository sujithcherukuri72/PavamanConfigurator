using pavamanDroneConfigurator.Core.Models;

namespace pavamanDroneConfigurator.Core.Interfaces;

public interface IAirframeService
{
    Task<AirframeSettings?> GetAirframeSettingsAsync();
    Task<bool> UpdateAirframeSettingsAsync(AirframeSettings settings);
}
