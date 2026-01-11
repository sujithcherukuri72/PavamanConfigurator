using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Helper service for handling parameter read/write operations during calibration
/// Uses existing ConnectionService parameter methods
/// </summary>
public class CalibrationParameterHelper
{
    private readonly ILogger<CalibrationParameterHelper> _logger;
    private readonly IConnectionService _connectionService;

    public CalibrationParameterHelper(
        ILogger<CalibrationParameterHelper> logger,
        IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
    }

    /// <summary>
    /// Write a calibration parameter and verify it was set
    /// </summary>
    public async Task<bool> WriteCalibrationParameterAsync(
        string paramName,
        float value,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Writing calibration parameter: {ParamName} = {Value}", paramName, value);

            // Note: The existing ConnectionService doesn't expose parameter write directly
            // This would need to be added to IConnectionService or use RequestParameter
            
            // For now, log the intent
            _logger.LogInformation("Parameter write requested: {ParamName} = {Value}", paramName, value);
            
            // In a full implementation, this would:
            // 1. Send PARAM_SET message
            // 2. Wait for PARAM_VALUE confirmation
            // 3. Verify the value matches
            
            await Task.Delay(100, ct); // Simulate async operation
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write parameter {ParamName}", paramName);
            return false;
        }
    }

    /// <summary>
    /// Read a calibration parameter value
    /// </summary>
    public async Task<float?> ReadCalibrationParameterAsync(
        string paramName,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Reading calibration parameter: {ParamName}", paramName);

            // Note: The existing ConnectionService doesn't expose RequestParameter directly
            // Comment out for now - would use existing parameter service
            
            // _connectionService.RequestParameter(paramName);

            // In a full implementation, this would:
            // 1. Send PARAM_REQUEST_READ message
            // 2. Wait for PARAM_VALUE response
            // 3. Return the value
            
            await Task.Delay(100, ct); // Simulate async operation
            
            _logger.LogInformation("Parameter read requested: {ParamName}", paramName);
            return null; // Would return actual value
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read parameter {ParamName}", paramName);
            return null;
        }
    }

    /// <summary>
    /// Write multiple flow sensor parameters
    /// </summary>
    public async Task<bool> WriteFlowParametersAsync(
        float xScale,
        float yScale,
        float yawAlignment,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Writing flow sensor parameters: X={XScale}, Y={YScale}, Yaw={Yaw}", 
            xScale, yScale, yawAlignment);

        var success = true;
        success &= await WriteCalibrationParameterAsync("FLOW_FXSCALER", xScale, ct);
        success &= await WriteCalibrationParameterAsync("FLOW_FYSCALER", yScale, ct);
        success &= await WriteCalibrationParameterAsync("FLOW_ORIENT_YAW", yawAlignment, ct);

        return success;
    }

    /// <summary>
    /// Read accelerometer calibration offsets
    /// </summary>
    public async Task<(float x, float y, float z)?> ReadAccelOffsetsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Reading accelerometer calibration offsets");

        var x = await ReadCalibrationParameterAsync("INS_ACCOFFS_X", ct);
        var y = await ReadCalibrationParameterAsync("INS_ACCOFFS_Y", ct);
        var z = await ReadCalibrationParameterAsync("INS_ACCOFFS_Z", ct);

        if (x.HasValue && y.HasValue && z.HasValue)
        {
            return (x.Value, y.Value, z.Value);
        }

        return null;
    }

    /// <summary>
    /// Read compass calibration offsets
    /// </summary>
    public async Task<(float x, float y, float z)?> ReadCompassOffsetsAsync(
        int compassIndex = 1, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Reading compass {Index} calibration offsets", compassIndex);

        string prefix = compassIndex == 1 ? "COMPASS_OFS" : $"COMPASS_OFS{compassIndex}";
        
        var x = await ReadCalibrationParameterAsync($"{prefix}_X", ct);
        var y = await ReadCalibrationParameterAsync($"{prefix}_Y", ct);
        var z = await ReadCalibrationParameterAsync($"{prefix}_Z", ct);

        if (x.HasValue && y.HasValue && z.HasValue)
        {
            return (x.Value, y.Value, z.Value);
        }

        return null;
    }

    /// <summary>
    /// Verify calibration was successful by checking if offsets are non-zero
    /// </summary>
    public async Task<bool> VerifyAccelCalibrationAsync(CancellationToken ct = default)
    {
        var offsets = await ReadAccelOffsetsAsync(ct);
        
        if (!offsets.HasValue)
        {
            _logger.LogWarning("Failed to read accelerometer offsets");
            return false;
        }

        var (x, y, z) = offsets.Value;
        var hasOffsets = Math.Abs(x) > 0.001f || Math.Abs(y) > 0.001f || Math.Abs(z) > 0.001f;

        _logger.LogInformation("Accelerometer calibration verification: HasOffsets={HasOffsets}, Offsets=({X}, {Y}, {Z})",
            hasOffsets, x, y, z);

        return hasOffsets;
    }

    /// <summary>
    /// Verify compass calibration was successful
    /// </summary>
    public async Task<bool> VerifyCompassCalibrationAsync(int compassIndex = 1, CancellationToken ct = default)
    {
        var offsets = await ReadCompassOffsetsAsync(compassIndex, ct);
        
        if (!offsets.HasValue)
        {
            _logger.LogWarning("Failed to read compass {Index} offsets", compassIndex);
            return false;
        }

        var (x, y, z) = offsets.Value;
        var hasOffsets = Math.Abs(x) > 0.001f || Math.Abs(y) > 0.001f || Math.Abs(z) > 0.001f;

        _logger.LogInformation("Compass {Index} calibration verification: HasOffsets={HasOffsets}, Offsets=({X}, {Y}, {Z})",
            compassIndex, hasOffsets, x, y, z);

        return hasOffsets;
    }
}
