using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Mavlink.V2.Common;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service for reading and writing MAVLink parameters with verification
/// </summary>
public class MavlinkParameterService
{
    private readonly ILogger<MavlinkParameterService> _logger;
    private readonly IMavlinkTransport _transport;

    public MavlinkParameterService(ILogger<MavlinkParameterService> logger, IMavlinkTransport transport)
    {
        _logger = logger;
        _transport = transport;
    }

    /// <summary>
    /// Write a parameter using PARAM_SET and verify with PARAM_VALUE
    /// </summary>
    public async Task<bool> WriteParameterAsync(
        string paramName,
        float value,
        byte targetSystem = 1,
        byte targetComponent = 1,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Writing parameter {ParamName} = {Value}", paramName, value);

            // Send PARAM_SET
            var paramSet = new ParamSetPayload
            {
                TargetSystem = targetSystem,
                TargetComponent = targetComponent,
                ParamId = paramName,
                ParamValue = value,
                ParamType = MavParamType.MavParamTypeReal32
            };

            await _transport.SendMessageAsync(paramSet, ct);

            // Wait for PARAM_VALUE confirmation
            var confirmed = await _transport.OnMessageReceived
                .OfType<ParamValuePayload>()
                .Where(p => p.ParamId == paramName)
                .Select(p =>
                {
                    _logger.LogInformation("Parameter {ParamName} confirmed: {Value}", paramName, p.ParamValue);
                    return Math.Abs(p.ParamValue - value) < 0.0001f;
                })
                .Timeout(TimeSpan.FromSeconds(5))
                .FirstOrDefaultAsync()
                .ToTask(ct);

            if (!confirmed)
            {
                _logger.LogWarning("Parameter {ParamName} write failed or value mismatch", paramName);
            }

            return confirmed;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout waiting for PARAM_VALUE for {ParamName}", paramName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing parameter {ParamName}", paramName);
            return false;
        }
    }

    /// <summary>
    /// Read a parameter value using PARAM_REQUEST_READ
    /// </summary>
    public async Task<float?> ReadParameterAsync(
        string paramName,
        byte targetSystem = 1,
        byte targetComponent = 1,
        CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Reading parameter {ParamName}", paramName);

            // Send PARAM_REQUEST_READ
            var paramRequest = new ParamRequestReadPayload
            {
                TargetSystem = targetSystem,
                TargetComponent = targetComponent,
                ParamId = paramName,
                ParamIndex = -1
            };

            await _transport.SendMessageAsync(paramRequest, ct);

            // Wait for PARAM_VALUE response
            var value = await _transport.OnMessageReceived
                .OfType<ParamValuePayload>()
                .Where(p => p.ParamId == paramName)
                .Select(p =>
                {
                    _logger.LogInformation("Parameter {ParamName} read: {Value}", paramName, p.ParamValue);
                    return (float?)p.ParamValue;
                })
                .Timeout(TimeSpan.FromSeconds(5))
                .FirstOrDefaultAsync()
                .ToTask(ct);

            return value;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout waiting for PARAM_VALUE for {ParamName}", paramName);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading parameter {ParamName}", paramName);
            return null;
        }
    }
}
