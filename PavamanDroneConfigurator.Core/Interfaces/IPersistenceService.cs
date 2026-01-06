using pavamanDroneConfigurator.Core.Models;

namespace pavamanDroneConfigurator.Core.Interfaces;

public interface IPersistenceService
{
    Task<bool> SaveProfileAsync(string profileName, Dictionary<string, object> data);
    Task<Dictionary<string, object>?> LoadProfileAsync(string profileName);
    Task<List<string>> GetProfileNamesAsync();
}
