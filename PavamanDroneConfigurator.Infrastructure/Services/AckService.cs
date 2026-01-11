using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Mavlink.V2.Common;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service for waiting for MAVLink COMMAND_ACK messages with timeout and retry support
/// </summary>
public class AckService
{
    private readonly ILogger<AckService> _logger;
    private readonly IMavlinkTransport _transport;

    public AckService(ILogger<AckService> logger, IMavlinkTransport transport)
    {
        _logger = logger;
        _transport = transport;
    }

    /// <summary>
    /// Wait for a COMMAND_ACK for a specific command
    /// </summary>
    /// <param name="commandId">The MAVLink command ID</param>
    /// <param name="timeout">Timeout duration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if ACK received successfully, false otherwise</returns>
    public async Task<bool> WaitForAckAsync(int commandId, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            _logger.LogDebug("Waiting for ACK for command {CommandId} with timeout {Timeout}", commandId, timeout);

            var ackReceived = await _transport.OnMessageReceived
                .OfType<CommandAckPayload>()
                .Where(ack => ack.Command == (MavCmd)commandId)
                .Select(ack =>
                {
                    _logger.LogInformation("Received ACK for command {CommandId}: Result={Result}", 
                        commandId, ack.Result);
                    return ack.Result == MavResult.MavResultAccepted;
                })
                .Timeout(timeout)
                .FirstOrDefaultAsync()
                .ToTask(ct);

            return ackReceived;
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Timeout waiting for ACK for command {CommandId}", commandId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for ACK for command {CommandId}", commandId);
            return false;
        }
    }

    /// <summary>
    /// Wait for ACK with automatic retries
    /// </summary>
    public async Task<bool> WaitForAckWithRetryAsync(
        int commandId,
        Func<Task> sendCommand,
        int maxRetries,
        TimeSpan timeout,
        CancellationToken ct)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogInformation("Retry attempt {Attempt}/{MaxRetries} for command {CommandId}", 
                    attempt, maxRetries, commandId);
                await Task.Delay(1000 * attempt, ct); // Exponential backoff
            }

            await sendCommand();
            
            var ackReceived = await WaitForAckAsync(commandId, timeout, ct);
            if (ackReceived)
            {
                return true;
            }
        }

        _logger.LogError("Failed to receive ACK for command {CommandId} after {MaxRetries} retries", 
            commandId, maxRetries);
        return false;
    }
}
