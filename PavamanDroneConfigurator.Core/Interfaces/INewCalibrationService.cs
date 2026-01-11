using System.Threading;
using System.Threading.Tasks;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Calibration service interface for sensor calibration operations
/// Implements the backend logic for the sensor calibration UI
/// </summary>
public interface INewCalibrationService
{
    /// <summary>
    /// Start calibration for a specific sensor category
    /// </summary>
    /// <param name="category">The sensor category to calibrate</param>
    /// <param name="ct">Cancellation token</param>
    Task StartCalibrationAsync(SensorCategory category, CancellationToken ct);
    
    /// <summary>
    /// Advance to the next step in the calibration process
    /// </summary>
    /// <param name="category">The sensor category being calibrated</param>
    /// <param name="ct">Cancellation token</param>
    Task NextStepAsync(SensorCategory category, CancellationToken ct);
    
    /// <summary>
    /// Abort the current calibration process
    /// </summary>
    /// <param name="category">The sensor category to abort</param>
    /// <param name="ct">Cancellation token</param>
    Task AbortCalibrationAsync(SensorCategory category, CancellationToken ct);
    
    /// <summary>
    /// Commit calibration results and persist parameters
    /// </summary>
    /// <param name="category">The sensor category to commit</param>
    /// <param name="ct">Cancellation token</param>
    Task CommitCalibrationAsync(SensorCategory category, CancellationToken ct);
    
    /// <summary>
    /// Get the current category state
    /// </summary>
    /// <param name="category">The sensor category</param>
    Category GetCategoryState(SensorCategory category);
    
    /// <summary>
    /// Reboot the drone after calibration
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task RebootDroneAsync(CancellationToken ct);
}
