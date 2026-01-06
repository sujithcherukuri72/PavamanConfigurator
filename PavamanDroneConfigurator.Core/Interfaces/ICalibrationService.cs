using pavamanDroneConfigurator.Core.Enums;
using pavamanDroneConfigurator.Core.Models;

namespace pavamanDroneConfigurator.Core.Interfaces;

public interface ICalibrationService
{
    Task<bool> StartCalibrationAsync(CalibrationType type);
    Task<bool> CancelCalibrationAsync();
    CalibrationStateModel? CurrentState { get; }
    event EventHandler<CalibrationStateModel>? CalibrationStateChanged;
}
