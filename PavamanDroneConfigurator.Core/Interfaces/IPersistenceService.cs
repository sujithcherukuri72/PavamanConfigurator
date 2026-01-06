using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

public interface IPersistenceService
{
    Task<bool> SaveProfileAsync(string profileName, Dictionary<string, object> data);
    Task<Dictionary<string, object>?> LoadProfileAsync(string profileName);
    Task<List<string>> GetProfileNamesAsync();
}
