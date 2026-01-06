using Microsoft.Extensions.Logging;
using pavamanDroneConfigurator.Core.Enums;
using pavamanDroneConfigurator.Core.Interfaces;
using pavamanDroneConfigurator.Core.Models;
using pavamanDroneConfigurator.Infrastructure.MAVLink;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InTheHand.Net.Sockets;

namespace pavamanDroneConfigurator.Infrastructure.Services;

public class ConnectionService : IConnectionService, IDisposable
{
    private readonly ILogger<ConnectionService> _logger;
    private readonly SynchronizationContext? _synchronizationContext;
    private SerialPort? _serialPort;
    private TcpClient? _tcpClient;
    private BluetoothMavConnection? _bluetoothConnection;
    private bool _isConnected;
    private System.Timers.Timer? _heartbeatTimer;
    private CancellationTokenSource? _serialPortWatcherCts;
    private Task? _serialPortWatcherTask;
    private DateTime _lastHeartbeat = DateTime.MinValue;
    private const int HeartbeatTimeoutMs = 5000;
    private const int SerialPortWatcherIntervalMs = 1000;
    private static readonly Regex ComPortRegex = new(@"\((COM\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private TaskCompletionSource<bool>? _firstHeartbeatTcs;
    private readonly SemaphoreSlim _disconnectLock = new(1, 1);
    private ConnectionType? _activeConnectionType;
    private string? _activeSerialPortName;
    private SerialPortInfo[] _availablePorts = Array.Empty<SerialPortInfo>();

    private byte _targetSystemId;
    private byte _targetComponentId;
    private AsvMavlinkWrapper? _asvWrapper;

    public bool IsConnected => _isConnected;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<IEnumerable<SerialPortInfo>>? AvailableSerialPortsChanged;
    
    // MAVLink message events for ParameterService to subscribe to
    public event EventHandler<MavlinkParamValueEventArgs>? ParamValueReceived;
    public event EventHandler? HeartbeatReceived;

    public ConnectionService(ILogger<ConnectionService> logger)
    {
        _logger = logger;
        // Capture the synchronization context (UI thread) at construction time
        _synchronizationContext = SynchronizationContext.Current;
        _availablePorts = EnumerateSerialPorts();
        StartSerialPortWatcher();
    }

    /// <summary>
    /// Safely invoke an action on the UI thread if a synchronization context is available
    /// </summary>
    private void InvokeOnUIThread(Action action)
    {
        if (_synchronizationContext != null)
        {
            _synchronizationContext.Post(_ => action(), null);
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Safely raise ConnectionStateChanged event on UI thread
    /// </summary>
    private void RaiseConnectionStateChanged(bool connected)
    {
        InvokeOnUIThread(() => ConnectionStateChanged?.Invoke(this, connected));
    }

    /// <summary>
    /// Safely raise HeartbeatReceived event on UI thread
    /// </summary>
    private void RaiseHeartbeatReceived()
    {
        InvokeOnUIThread(() => HeartbeatReceived?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    /// Safely raise ParamValueReceived event on UI thread
    /// </summary>
    private void RaiseParamValueReceived(MavlinkParamValueEventArgs args)
    {
        InvokeOnUIThread(() => ParamValueReceived?.Invoke(this, args));
    }

    /// <summary>
    /// Safely raise AvailableSerialPortsChanged event on UI thread
    /// </summary>
    private void RaiseAvailableSerialPortsChanged(IEnumerable<SerialPortInfo> ports)
    {
        InvokeOnUIThread(() => AvailableSerialPortsChanged?.Invoke(this, ports));
    }

    public async Task<bool> ConnectAsync(ConnectionSettings settings)
    {
        try
        {
            await DisconnectAsync();
            _logger.LogInformation("Connecting via {Type}...", settings.Type);

            // Create heartbeat task completion source BEFORE opening transport
            // This prevents race condition where heartbeat arrives before TCS is ready
            _firstHeartbeatTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            bool transportOpened = settings.Type switch
            {
                ConnectionType.Tcp => await ConnectTcpAsync(settings),
                ConnectionType.Serial => await ConnectSerialAsync(settings),
                ConnectionType.Bluetooth => await ConnectBluetoothAsync(settings),
                _ => throw new ArgumentException($"Unsupported connection type: {settings.Type}")
            };

            if (!transportOpened)
            {
                _firstHeartbeatTcs = null;
                return false;
            }

            _activeConnectionType = settings.Type;
            
            // Wait for first heartbeat with timeout
            _logger.LogInformation("Waiting for heartbeat...");
            var completed = await Task.WhenAny(_firstHeartbeatTcs.Task, Task.Delay(HeartbeatTimeoutMs));
            var heartbeatReceived = completed == _firstHeartbeatTcs.Task && _firstHeartbeatTcs.Task.Result;
            
            if (!heartbeatReceived)
            {
                _logger.LogWarning("No heartbeat received within {Timeout}ms. Disconnecting.", HeartbeatTimeoutMs);
                await DisconnectAsync();
                return false;
            }

            _logger.LogInformation("Connection established successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect");
            await DisconnectAsync();
            return false;
        }
    }

    private async Task<bool> ConnectTcpAsync(ConnectionSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.IpAddress))
            {
                _logger.LogError("IP Address is required for TCP connection");
                return false;
            }

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(settings.IpAddress, settings.Port);
            if (!_tcpClient.Connected)
            {
                return false;
            }

            var stream = _tcpClient.GetStream();
            InitializeAsvWrapper(stream, stream);

            _logger.LogInformation("TCP connection established to {IpAddress}:{Port}", settings.IpAddress, settings.Port);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish TCP connection");
            await DisposeAsvAsync();
            _tcpClient?.Dispose();
            _tcpClient = null;
            return false;
        }
    }

    private Task<bool> ConnectSerialAsync(ConnectionSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.PortName))
            {
                _logger.LogError("Port name is required for Serial connection");
                return Task.FromResult(false);
            }

            _serialPort = new SerialPort(settings.PortName, settings.BaudRate)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            _serialPort.Open();
            _activeSerialPortName = settings.PortName;

            var stream = _serialPort.BaseStream;
            InitializeAsvWrapper(stream, stream);

            _logger.LogInformation("Serial connection established on {PortName} at {BaudRate} baud", settings.PortName, settings.BaudRate);
            return Task.FromResult(_serialPort.IsOpen);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish Serial connection");
            _serialPort?.Dispose();
            _serialPort = null;
            _activeSerialPortName = null;
            return Task.FromResult(false);
        }
    }

    private void InitializeAsvWrapper(Stream input, Stream output)
    {
        DisposeAsvAsync().GetAwaiter().GetResult();

        _asvWrapper = new AsvMavlinkWrapper(_logger);
        
        // Subscribe to MAVLink events
        _asvWrapper.HeartbeatReceived += OnAsvHeartbeatReceived;
        _asvWrapper.ParamValueReceived += OnAsvParamValueReceived;

        _asvWrapper.Initialize(input, output);
    }

    private void OnAsvHeartbeatReceived(object? sender, (byte SystemId, byte ComponentId) e)
    {
        // Ignore invalid heartbeats
        if (e.SystemId == 0)
        {
            return;
        }

        _targetSystemId = e.SystemId;
        _targetComponentId = e.ComponentId;
        _lastHeartbeat = DateTime.UtcNow;
        _firstHeartbeatTcs?.TrySetResult(true);

        if (!_isConnected)
        {
            _isConnected = true;
            StartHeartbeatMonitoring();
            // Raise event on UI thread to avoid cross-thread exception
            RaiseConnectionStateChanged(true);
            _logger.LogInformation("Heartbeat received from sysid {SystemId}, compid {ComponentId}. Connection established.", e.SystemId, e.ComponentId);
        }
        
        // Raise event on UI thread
        RaiseHeartbeatReceived();
    }

    private void OnAsvParamValueReceived(object? sender, (string Name, float Value, ushort Index, ushort Count) e)
    {
        var parameter = new DroneParameter
        {
            Name = e.Name,
            Value = e.Value
        };
        
        // Raise event on UI thread
        RaiseParamValueReceived(new MavlinkParamValueEventArgs(parameter, e.Index, e.Count));
    }

    private async Task DisposeAsvAsync()
    {
        try
        {
            if (_asvWrapper != null)
            {
                // Unsubscribe from events
                _asvWrapper.HeartbeatReceived -= OnAsvHeartbeatReceived;
                _asvWrapper.ParamValueReceived -= OnAsvParamValueReceived;
                
                // Dispose the wrapper
                _asvWrapper.Dispose();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing Asv wrapper");
        }
        finally
        {
            _asvWrapper = null;
        }
        
        await Task.CompletedTask;
    }

    private async Task<bool> ConnectBluetoothAsync(ConnectionSettings settings)
    {
        try
        {
            if (string.IsNullOrEmpty(settings.BluetoothDeviceAddress) && string.IsNullOrEmpty(settings.BluetoothDeviceName))
            {
                _logger.LogError("Bluetooth device address or name is required");
                return false;
            }

            _bluetoothConnection = new BluetoothMavConnection(_logger);
            
            // Subscribe to Bluetooth events
            _bluetoothConnection.HeartbeatReceived += OnBluetoothHeartbeat;
            _bluetoothConnection.ParamValueReceived += OnBluetoothParamValue;
            _bluetoothConnection.ConnectionStateChanged += OnBluetoothConnectionStateChanged;

            // Connect by address or name
            bool connected;
            if (!string.IsNullOrEmpty(settings.BluetoothDeviceAddress))
            {
                connected = await _bluetoothConnection.ConnectAsync(settings.BluetoothDeviceAddress);
            }
            else
            {
                connected = await _bluetoothConnection.ConnectByNameAsync(settings.BluetoothDeviceName!);
            }

            _logger.LogInformation("Bluetooth connection established");
            return connected;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to establish Bluetooth connection");
            _bluetoothConnection?.Dispose();
            _bluetoothConnection = null;
            return false;
        }
    }

    private void OnBluetoothHeartbeat(object? sender, (byte SystemId, byte ComponentId) e)
    {
        OnAsvHeartbeatReceived(sender, e);
    }

    private void OnBluetoothParamValue(object? sender, (string Name, float Value, ushort Index, ushort Count) e)
    {
        OnAsvParamValueReceived(sender, e);
    }

    private void OnBluetoothConnectionStateChanged(object? sender, bool connected)
    {
        if (!connected)
        {
            _ = DisconnectAsync();
        }
    }

    private void StartHeartbeatMonitoring()
    {
        StopHeartbeatMonitoring();

        _heartbeatTimer = new System.Timers.Timer(1000);
        _heartbeatTimer.Elapsed += async (s, e) =>
        {
            if (_lastHeartbeat == DateTime.MinValue)
            {
                return;
            }

            var timeSinceLastHeartbeat = DateTime.UtcNow - _lastHeartbeat;
            if (timeSinceLastHeartbeat.TotalMilliseconds > HeartbeatTimeoutMs)
            {
                _logger.LogWarning("Heartbeat timeout detected. Auto-disconnecting...");
                try
                {
                    await DisconnectAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during auto-disconnect");
                }
            }
        };
        _heartbeatTimer.Start();
    }

    private void StopHeartbeatMonitoring()
    {
        if (_heartbeatTimer != null)
        {
            _heartbeatTimer.Stop();
            _heartbeatTimer.Dispose();
            _heartbeatTimer = null;
        }
    }

    public Task DisconnectAsync() => DisconnectInternalAsync(false);

    private async Task DisconnectInternalAsync(bool calledFromReceiveLoop)
    {
        await _disconnectLock.WaitAsync();
        try
        {
            _firstHeartbeatTcs?.TrySetResult(false);

            StopHeartbeatMonitoring();

            // Dispose ASV wrapper
            await DisposeAsvAsync();

            if (_serialPort != null)
            {
                try
                {
                    if (_serialPort.IsOpen)
                    {
                        _serialPort.Close();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing serial port");
                }

                _serialPort.Dispose();
                _serialPort = null;
            }

            if (_tcpClient != null)
            {
                try
                {
                    _tcpClient.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing TCP client");
                }

                _tcpClient.Dispose();
                _tcpClient = null;
            }

            if (_bluetoothConnection != null)
            {
                try
                {
                    _bluetoothConnection.HeartbeatReceived -= OnBluetoothHeartbeat;
                    _bluetoothConnection.ParamValueReceived -= OnBluetoothParamValue;
                    _bluetoothConnection.ConnectionStateChanged -= OnBluetoothConnectionStateChanged;
                    await _bluetoothConnection.CloseAsync();
                    _bluetoothConnection.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while closing Bluetooth connection");
                }

                _bluetoothConnection = null;
            }

            _activeConnectionType = null;
            _activeSerialPortName = null;
            ResetParameterTracking();

            bool wasConnected = _isConnected;
            _isConnected = false;
            _lastHeartbeat = DateTime.MinValue;

            if (wasConnected)
            {
                // Raise event on UI thread
                RaiseConnectionStateChanged(false);
            }
        }
        finally
        {
            _disconnectLock.Release();
        }
    }

    public IEnumerable<SerialPortInfo> GetAvailableSerialPorts() => _availablePorts;

    public async Task<IEnumerable<Core.Models.BluetoothDeviceInfo>> GetAvailableBluetoothDevicesAsync()
    {
        try
        {
            _logger.LogInformation("Discovering Bluetooth devices...");
            
            var client = new BluetoothClient();
            var devices = await Task.Run(() => client.DiscoverDevices().ToList());

            return devices.Select(d => new Core.Models.BluetoothDeviceInfo
            {
                DeviceAddress = d.DeviceAddress.ToString(),
                DeviceName = d.DeviceName,
                IsConnected = d.Connected,
                IsPaired = d.Authenticated
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover Bluetooth devices");
            return Array.Empty<Core.Models.BluetoothDeviceInfo>();
        }
    }

    private void StartSerialPortWatcher()
    {
        _serialPortWatcherCts = new CancellationTokenSource();
        _serialPortWatcherTask = Task.Run(() => SerialPortWatcherLoopAsync(_serialPortWatcherCts.Token));
    }

    private void StopSerialPortWatcher()
    {
        if (_serialPortWatcherCts != null)
        {
            _serialPortWatcherCts.Cancel();
            _serialPortWatcherCts.Dispose();
            _serialPortWatcherCts = null;
            _serialPortWatcherTask = null;
        }
    }

    private async Task UpdateSerialPortsSafeAsync()
    {
        try
        {
            await UpdateSerialPortsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh serial port list");
        }
    }

    private async Task SerialPortWatcherLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await UpdateSerialPortsSafeAsync().ConfigureAwait(false);
            try
            {
                await Task.Delay(SerialPortWatcherIntervalMs, token);
            }
            catch (TaskCanceledException)
            {
                // expected on shutdown
            }
        }
    }

    private async Task UpdateSerialPortsAsync()
    {
        var ports = EnumerateSerialPorts();
        if (!PortsEqual(_availablePorts, ports))
        {
            _availablePorts = ports;
            // Raise event on UI thread
            RaiseAvailableSerialPortsChanged(_availablePorts);
        }

        if (_serialPort != null &&
            _serialPort.IsOpen &&
            _activeSerialPortName != null &&
            !_availablePorts.Any(p => string.Equals(p.PortName, _activeSerialPortName, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Active serial port {Port} disappeared. Disconnecting.", _activeSerialPortName);
            await DisconnectAsync().ConfigureAwait(false);
        }
    }

    public Stream? GetTransportStream()
    {
        return _activeConnectionType == ConnectionType.Tcp
            ? _tcpClient?.GetStream()
            : _serialPort?.BaseStream;
    }

    private SerialPortInfo[] EnumerateSerialPorts()
    {
        if (!OperatingSystem.IsWindows())
        {
            return SerialPort.GetPortNames()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(port => new SerialPortInfo
                {
                    PortName = port,
                    FriendlyName = port,
                    InterfaceType = "Serial"
                })
                .ToArray();
        }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%)'");
            var ports = new List<SerialPortInfo>();
            foreach (var portObject in searcher.Get())
            {
                var name = portObject["Name"]?.ToString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var portName = ExtractPortName(name);
                if (string.IsNullOrWhiteSpace(portName))
                {
                    continue;
                }

                ports.Add(new SerialPortInfo
                {
                    PortName = portName,
                    FriendlyName = ExtractFriendlyName(name, portName),
                    InterfaceType = DetectInterfaceType(name)
                });
            }

            if (ports.Count == 0)
            {
                return SerialPort.GetPortNames()
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .Select(port => new SerialPortInfo
                    {
                        PortName = port,
                        FriendlyName = port,
                        InterfaceType = "Serial"
                    })
                    .ToArray();
            }

            return ports
                .OrderBy(p => p.PortName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enumerate serial ports via WMI. Falling back to SerialPort.GetPortNames.");
            return SerialPort.GetPortNames()
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(port => new SerialPortInfo
                {
                    PortName = port,
                    FriendlyName = port,
                    InterfaceType = "Serial"
                })
                .ToArray();
        }
    }

    private static string ExtractPortName(string deviceName)
    {
        var match = ComPortRegex.Match(deviceName);
        return match.Success ? match.Groups[1].Value.ToUpperInvariant() : string.Empty;
    }

    private static string ExtractFriendlyName(string deviceName, string portName)
    {
        var portToken = $"({portName})";
        var friendly = deviceName.Replace(portToken, string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(friendly) ? deviceName : friendly;
    }

    private static string DetectInterfaceType(string name)
    {
        if (name.Contains("SLCAN", StringComparison.OrdinalIgnoreCase))
        {
            return "SLCAN";
        }

        if (name.Contains("Cube", StringComparison.OrdinalIgnoreCase) || name.Contains("PX4", StringComparison.OrdinalIgnoreCase))
        {
            return "MAVLink";
        }

        return "Serial";
    }

    private static bool PortsEqual(IReadOnlyList<SerialPortInfo> existing, IReadOnlyList<SerialPortInfo> updated)
    {
        if (existing.Count != updated.Count)
        {
            return false;
        }

        for (int i = 0; i < existing.Count; i++)
        {
            if (!string.Equals(existing[i].PortName, updated[i].PortName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing[i].FriendlyName, updated[i].FriendlyName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existing[i].InterfaceType, updated[i].InterfaceType, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    public void SendParamRequestList()
    {
        if (_activeConnectionType == ConnectionType.Bluetooth)
        {
            _ = _bluetoothConnection?.SendParamRequestListAsync();
            return;
        }

        if (_asvWrapper != null)
        {
            _ = _asvWrapper.SendParamRequestListAsync();
            return;
        }

        _logger.LogWarning("Cannot send PARAM_REQUEST_LIST - no active connection");
    }

    public void SendParamRequestRead(ushort paramIndex)
    {
        if (_activeConnectionType == ConnectionType.Bluetooth)
        {
            _ = _bluetoothConnection?.SendParamRequestReadAsync(paramIndex);
            return;
        }

        if (_asvWrapper != null)
        {
            _ = _asvWrapper.SendParamRequestReadAsync(paramIndex);
            return;
        }

        _logger.LogWarning("Cannot send PARAM_REQUEST_READ - no active connection");
    }

    public void SendParamSet(ParameterWriteRequest request)
    {
        if (_activeConnectionType == ConnectionType.Bluetooth)
        {
            _ = _bluetoothConnection?.SendParamSetAsync(request.Name, request.Value);
            return;
        }

        if (_asvWrapper != null)
        {
            _ = _asvWrapper.SendParamSetAsync(request.Name, request.Value);
            return;
        }

        _logger.LogWarning("Cannot send PARAM_SET - no active connection");
    }

    private void ResetParameterTracking()
    {
        _targetSystemId = 0;
        _targetComponentId = 0;
    }

    public void Dispose()
    {
        StopHeartbeatMonitoring();
        StopSerialPortWatcher();
        _serialPort?.Dispose();
        _tcpClient?.Dispose();
        _ = DisposeAsvAsync();
        _disconnectLock.Dispose();
    }
}
