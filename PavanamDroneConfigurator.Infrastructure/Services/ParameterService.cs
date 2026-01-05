using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;
using System.Collections.Concurrent;

namespace PavanamDroneConfigurator.Infrastructure.Services;

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
        var param = e.Parameter;
        _parameters[param.Name] = param;

        bool isNew;
        lock (_lock)
        {
            if (!_expectedCount.HasValue && e.ParamCount > 0)
            {
                _expectedCount = e.ParamCount;
                _logger.LogInformation("Total parameter count: {Count}", e.ParamCount);
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
            
            // Update progress every 100 params
            if (_received % 100 == 0 || (_expectedCount.HasValue && _received >= _expectedCount.Value))
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
        return Task.FromResult(_parameters.Values.OrderBy(p => p.Name).ToList());
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

        ParameterDownloadStarted?.Invoke(this, EventArgs.Empty);
        ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);

        _logger.LogInformation("Starting parameter download...");

        try
        {
            var ct = _downloadCts.Token;

            // Send initial PARAM_REQUEST_LIST
            _connectionService.SendParamRequestList();

            // Wait up to 2 seconds for first response
            await Task.Delay(2000, ct);

            // Retry loop like Mission Planner
            for (int retry = 0; retry < 5 && !ct.IsCancellationRequested; retry++)
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
                    _logger.LogInformation("All {Count} parameters received", expected);
                    break;
                }

                if (!hasExpected)
                {
                    // No response yet - resend request
                    _logger.LogWarning("No parameters received, resending PARAM_REQUEST_LIST (attempt {N})", retry + 1);
                    _connectionService.SendParamRequestList();
                    await Task.Delay(2000, ct);
                    continue;
                }

                _logger.LogInformation("Retry {N}: {Received}/{Expected}, requesting {Missing} missing", 
                    retry + 1, received, expected, missing.Count);

                // Request missing parameters in chunks
                foreach (var chunk in missing.Chunk(10))
                {
                    if (ct.IsCancellationRequested) break;

                    foreach (var idx in chunk)
                    {
                        _connectionService.SendParamRequestRead((ushort)idx);
                    }
                    await Task.Delay(100, ct); // Brief pause between chunks
                }

                // Wait for responses
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during parameter download");
        }

        // Finalize
        _downloading = false;
        _downloadDone = _received > 0;

        _logger.LogInformation("Parameter download finished: {Received}/{Expected}", 
            _received, _expectedCount ?? 0);

        ParameterDownloadCompleted?.Invoke(this, _downloadDone);
        ParameterDownloadProgressChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Reset()
    {
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
