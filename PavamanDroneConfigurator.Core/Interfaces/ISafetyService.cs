using pavamanDroneConfigurator.Core.Models;

namespace pavamanDroneConfigurator.Core.Interfaces;

public interface ISafetyService
{
    Task<SafetySettings?> GetSafetySettingsAsync();
    Task<bool> UpdateSafetySettingsAsync(SafetySettings settings);
}
