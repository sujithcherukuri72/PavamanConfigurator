using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

public interface ICalibrationService
{
    Task<bool> StartCalibrationAsync(CalibrationType type);
    Task<bool> CancelCalibrationAsync();
    CalibrationStateModel? CurrentState { get; }
    event EventHandler<CalibrationStateModel>? CalibrationStateChanged;
}
