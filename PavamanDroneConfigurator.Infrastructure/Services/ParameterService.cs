using Microsoft.Extensions.Logging;
using pavamanDroneConfigurator.Core.Interfaces;
using pavamanDroneConfigurator.Core.Models;
using System.Collections.Concurrent;

namespace pavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Parameter service that downloads parameters from drone like Mission Planner.
/// Uses aggressive retry strategy for missing parameters.
/// </summary>
public class ParameterService : IParameterService
{
    private readonly ILogger<ParameterService> _logger;
    private readonly IConnectionService _connectionService;
    private readonly ConcurrentDictionary<string, DroneParameter> _parameters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<DroneParameter>> _pendingWrites = new();
    private readonly HashSet<int> _receivedIndices = new();
    private readonly object _lock = new();
    
    private TaskCompletionSource<bool>? _downloadComplete;
    private CancellationTokenSource? _downloadCts;
    private ushort? _expectedCount;
    private volatile bool _downloading;
    private volatile bool _downloadDone;
    private int _received;

    public event EventHandler<string>? ParameterUpdated;
    public event EventHandler? ParameterDownloadStarted;
    public event EventHandler<bool>? ParameterDownloadCompleted;
    public event EventHandler? ParameterDownloadProgressChanged;

    public bool IsParameterDownloadInProgress => _downloading;
    public bool IsParameterDownloadComplete => _downloadDone;
    public int ReceivedParameterCount => _received;
    public int? ExpectedParameterCount => _expectedCount;

    public ParameterService(ILogger<ParameterService> logger, IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
        _connectionService.ParamValueReceived += OnParamReceived;
        _connectionService.ConnectionStateChanged += OnConnectionChanged;
    }

    private void OnConnectionChanged(object? sender, bool connected)
    {
        if (!connected) Reset();
    }

    private void OnParamReceived(object? sender, MavlinkParamValueEventArgs e)
    {
        // Store parameter with value from drone
        var param = new DroneParameter
        {
            Name = e.Parameter.Name,
            Value = e.Parameter.Value,
            Description = $"Index: {e.ParamIndex}"
        };
        
        _parameters[param.Name] = param;
        
        _logger.LogDebug("Received param: {Name} = {Value} [{Index}/{Count}]", 
            param.Name, param.Value, e.ParamIndex, e.ParamCount);

        bool isNew;
        lock (_lock)
        {
            if (!_expectedCount.HasValue && e.ParamCount > 0)
            {
                _expectedCount = e.ParamCount;
                _logger.LogInformation("Total parameter count from drone: {Count}", e.ParamCount);
            }

            isNew = _receivedIndices.Add(e.ParamIndex);
            _received = _receivedIndices.Count;

            // Check completion
            if (_expectedCount.HasValue && _received >= _expectedCount.Value)
            {
                _downloadComplete?.TrySetResult(true);
            }
        }

        if (isNew)
        {
            ParameterUpdated?.Invoke(this, param.Name);
            
            // Update progress every 50 params or near completion
            if (_received % 50 == 0 || (_expectedCount.HasValue && _received >= _expectedCount.Value - 5))
            {
                ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // Handle pending write confirmations
        if (_pendingWrites.TryRemove(param.Name, out var tcs))
        {
            tcs.TrySetResult(param);
        }
    }

    public Task<List<DroneParameter>> GetAllParametersAsync()
    {
        var list = _parameters.Values.OrderBy(p => p.Name).ToList();
        _logger.LogInformation("GetAllParametersAsync returning {Count} parameters", list.Count);
        return Task.FromResult(list);
    }

    public Task<DroneParameter?> GetParameterAsync(string name)
    {
        _parameters.TryGetValue(name, out var param);
        return Task.FromResult(param);
    }

    public async Task<bool> SetParameterAsync(string name, float value)
    {
        if (!_connectionService.IsConnected) return false;

        var tcs = new TaskCompletionSource<DroneParameter>();
        _pendingWrites[name] = tcs;

        _connectionService.SendParamSet(new ParameterWriteRequest(name, value));

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(3000));
        _pendingWrites.TryRemove(name, out _);

        return completed == tcs.Task;
    }

    public async Task RefreshParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot refresh: not connected");
            return;
        }

        // Stop any existing download
        _downloadCts?.Cancel();
        
        // Reset state
        _parameters.Clear();
        lock (_lock)
        {
            _receivedIndices.Clear();
            _expectedCount = null;
        }
        _received = 0;
        _downloading = true;
        _downloadDone = false;
        _downloadComplete = new TaskCompletionSource<bool>();
        _downloadCts = new CancellationTokenSource();

        _logger.LogInformation("=== Starting parameter download from drone ===");
        
        ParameterDownloadStarted?.Invoke(this, EventArgs.Empty);
        ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);

        try
        {
            var ct = _downloadCts.Token;

            // Send initial PARAM_REQUEST_LIST
            _logger.LogInformation("Sending PARAM_REQUEST_LIST...");
            _connectionService.SendParamRequestList();

            // Wait for first parameters to arrive
            await Task.Delay(3000, ct);
            
            _logger.LogInformation("After initial wait: received {Count} parameters", _received);

            // Retry loop - Mission Planner style
            for (int retry = 0; retry < 10 && !ct.IsCancellationRequested; retry++)
            {
                int expected;
                int received;
                List<int> missing;
                bool hasExpected;
                bool isComplete;

                lock (_lock)
                {
                    hasExpected = _expectedCount.HasValue;
                    expected = _expectedCount ?? 0;
                    received = _receivedIndices.Count;
                    isComplete = hasExpected && received >= expected;
                    
                    if (hasExpected && !isComplete)
                    {
                        missing = Enumerable.Range(0, expected)
                            .Where(i => !_receivedIndices.Contains(i))
                            .ToList();
                    }
                    else
                    {
                        missing = new List<int>();
                    }
                }

                if (isComplete)
                {
                    _logger.LogInformation("All {Count} parameters received from drone!", expected);
                    break;
                }

                if (!hasExpected)
                {
                    // No response yet - resend request
                    _logger.LogWarning("No parameters received yet, resending PARAM_REQUEST_LIST (attempt {N}/10)", retry + 1);
                    _connectionService.SendParamRequestList();
                    await Task.Delay(3000, ct);
                    continue;
                }

                if (missing.Count == 0)
                {
                    _logger.LogInformation("Download complete: {Received}/{Expected}", received, expected);
                    break;
                }

                _logger.LogInformation("Retry {N}: {Received}/{Expected} params, requesting {Missing} missing", 
                    retry + 1, received, expected, missing.Count);

                // Request missing parameters in small chunks
                foreach (var chunk in missing.Chunk(5))
                {
                    if (ct.IsCancellationRequested) break;

                    foreach (var idx in chunk)
                    {
                        _connectionService.SendParamRequestRead((ushort)idx);
                    }
                    await Task.Delay(200, ct); // Give time for responses
                }

                // Wait for responses
                await Task.Delay(2000, ct);
                
                // Update progress
                ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Parameter download cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during parameter download");
        }

        // Finalize
        _downloading = false;
        _downloadDone = _received > 0;

        _logger.LogInformation("=== Parameter download finished: {Received} parameters from drone ===", _received);
        
        // Log first few parameters as proof
        var sample = _parameters.Values.Take(5).ToList();
        foreach (var p in sample)
        {
            _logger.LogInformation("  Sample param: {Name} = {Value}", p.Name, p.Value);
        }

        ParameterDownloadCompleted?.Invoke(this, _downloadDone);
        ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
        _logger.LogInformation("ParameterService Reset called");
        _downloadCts?.Cancel();
        _parameters.Clear();
        lock (_lock)
        {
            _receivedIndices.Clear();
            _expectedCount = null;
        }
        _received = 0;
        _downloading = false;
        _downloadDone = false;
        _downloadComplete?.TrySetCanceled();
        _downloadComplete = null;

        foreach (var tcs in _pendingWrites.Values)
            tcs.TrySetCanceled();
        _pendingWrites.Clear();

        ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
    }
}
