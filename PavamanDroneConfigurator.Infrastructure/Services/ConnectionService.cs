using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.Infrastructure.MAVLink;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service for managing drone connections via Serial, TCP, or Bluetooth.
/// Handles MAVLink communication and parameter transfer.
/// </summary>
public sealed class ConnectionService : IConnectionService, IDisposable
{
    private readonly ILogger<ConnectionService> _logger;
    private readonly object _lock = new();

    private SerialPort? _serialPort;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private BluetoothMavConnection? _bluetoothConnection;
    private AsvMavlinkWrapper? _mavlink;
    
    private Stream? _inputStream;
    private Stream? _outputStream;
    
    private bool _isConnected;
    private bool _disposed;
    private ConnectionType _currentConnectionType;

    private readonly System.Timers.Timer _portScanTimer;
    private List<SerialPortInfo> _cachedPorts = new();

    public bool IsConnected => _isConnected;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<IEnumerable<SerialPortInfo>>? AvailableSerialPortsChanged;
    public event EventHandler<MavlinkParamValueEventArgs>? ParamValueReceived;
    public event EventHandler? HeartbeatReceived;

    public ConnectionService(ILogger<ConnectionService> logger)
    {
        _logger = logger;

        _portScanTimer = new System.Timers.Timer(3000);
        _portScanTimer.Elapsed += (_, _) => ScanSerialPorts();
        _portScanTimer.Start();
        
        ScanSerialPorts();
    }

    public IEnumerable<SerialPortInfo> GetAvailableSerialPorts()
    {
        lock (_lock)
        {
            return _cachedPorts.ToList();
        }
    }

    private void ScanSerialPorts()
    {
        try
        {
            var ports = new List<SerialPortInfo>();
            var portNames = SerialPort.GetPortNames();

            foreach (var portName in portNames)
            {
                var description = GetPortDescription(portName);
                ports.Add(new SerialPortInfo 
                { 
                    PortName = portName, 
                    FriendlyName = description 
                });
            }

            lock (_lock)
            {
                if (!ports.SequenceEqual(_cachedPorts, new SerialPortInfoComparer()))
                {
                    _cachedPorts = ports;
                    AvailableSerialPortsChanged?.Invoke(this, ports);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error scanning serial ports");
        }
    }

    private static string GetPortDescription(string portName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'");
            
            foreach (var obj in searcher.Get())
            {
                var caption = obj["Caption"]?.ToString();
                if (caption != null && caption.Contains(portName))
                {
                    return caption;
                }
            }
        }
        catch
        {
            // WMI not available
        }
        
        return portName;
    }

    public async Task<IEnumerable<BluetoothDeviceInfo>> GetAvailableBluetoothDevicesAsync()
    {
        var devices = new List<BluetoothDeviceInfo>();
        
        try
        {
            var connection = new BluetoothMavConnection(_logger);
            devices = (await connection.DiscoverDevicesAsync()).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error scanning Bluetooth devices");
        }
        
        return devices;
    }

    public async Task<bool> ConnectAsync(ConnectionSettings settings)
    {
        if (_isConnected)
        {
            await DisconnectAsync();
        }

        try
        {
            _currentConnectionType = settings.Type;

            switch (settings.Type)
            {
                case ConnectionType.Serial:
                    return await ConnectSerialAsync(settings);
                case ConnectionType.Tcp:
                    return await ConnectTcpAsync(settings);
                case ConnectionType.Bluetooth:
                    return await ConnectBluetoothAsync(settings);
                default:
                    _logger.LogError("Unsupported connection type: {Type}", settings.Type);
                    return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection failed");
            await DisconnectAsync();
            return false;
        }
    }

    private async Task<bool> ConnectSerialAsync(ConnectionSettings settings)
    {
        _logger.LogInformation("Connecting to serial port {Port} at {Baud} baud", 
            settings.PortName, settings.BaudRate);

        _serialPort = new SerialPort(settings.PortName, settings.BaudRate)
        {
            ReadTimeout = 5000,
            WriteTimeout = 5000,
            DtrEnable = true,
            RtsEnable = true
        };

        _serialPort.Open();
        
        _inputStream = _serialPort.BaseStream;
        _outputStream = _serialPort.BaseStream;

        InitializeMavlink();
        
        // Wait for heartbeat
        var heartbeatReceived = await WaitForHeartbeatAsync(TimeSpan.FromSeconds(10));
        
        if (heartbeatReceived)
        {
            SetConnected(true);
            _logger.LogInformation("Serial connection established");
            return true;
        }

        _logger.LogWarning("No heartbeat received on serial connection");
        await DisconnectAsync();
        return false;
    }

    private async Task<bool> ConnectTcpAsync(ConnectionSettings settings)
    {
        _logger.LogInformation("Connecting to TCP {Host}:{Port}", 
            settings.IpAddress, settings.Port);

        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(settings.IpAddress ?? "127.0.0.1", settings.Port);
        _networkStream = _tcpClient.GetStream();
        
        _inputStream = _networkStream;
        _outputStream = _networkStream;

        InitializeMavlink();
        
        var heartbeatReceived = await WaitForHeartbeatAsync(TimeSpan.FromSeconds(10));
        
        if (heartbeatReceived)
        {
            SetConnected(true);
            _logger.LogInformation("TCP connection established");
            return true;
        }

        _logger.LogWarning("No heartbeat received on TCP connection");
        await DisconnectAsync();
        return false;
    }

    private async Task<bool> ConnectBluetoothAsync(ConnectionSettings settings)
    {
        _logger.LogInformation("Connecting to Bluetooth device {Address}", settings.BluetoothDeviceAddress);

        _bluetoothConnection = new BluetoothMavConnection(_logger);
        var success = await _bluetoothConnection.ConnectAsync(settings.BluetoothDeviceAddress ?? string.Empty);

        if (!success)
        {
            _logger.LogWarning("Failed to connect to Bluetooth device");
            return false;
        }

        // For Bluetooth, we use the BluetoothMavConnection's internal MAVLink handling
        _bluetoothConnection.HeartbeatReceived += (s, e) => HeartbeatReceived?.Invoke(this, EventArgs.Empty);
        _bluetoothConnection.ParamValueReceived += (s, e) =>
        {
            var param = new DroneParameter { Name = e.Name, Value = e.Value };
            ParamValueReceived?.Invoke(this, new MavlinkParamValueEventArgs(param, e.Index, e.Count));
        };
        
        var heartbeatReceived = await WaitForHeartbeatAsync(TimeSpan.FromSeconds(15));
        
        if (heartbeatReceived)
        {
            SetConnected(true);
            _logger.LogInformation("Bluetooth connection established");
            return true;
        }

        _logger.LogWarning("No heartbeat received on Bluetooth connection");
        await DisconnectAsync();
        return false;
    }

    private void InitializeMavlink()
    {
        if (_inputStream == null || _outputStream == null)
        {
            _logger.LogError("Cannot initialize MAVLink - streams not available");
            return;
        }

        _mavlink = new AsvMavlinkWrapper(_logger);
        _mavlink.HeartbeatReceived += OnMavlinkHeartbeat;
        _mavlink.ParamValueReceived += OnMavlinkParamValue;
        _mavlink.Initialize(_inputStream, _outputStream);
    }

    private void OnMavlinkHeartbeat(object? sender, (byte SystemId, byte ComponentId) e)
    {
        HeartbeatReceived?.Invoke(this, EventArgs.Empty);
    }

    private void OnMavlinkParamValue(object? sender, (string Name, float Value, ushort Index, ushort Count) e)
    {
        var param = new DroneParameter
        {
            Name = e.Name,
            Value = e.Value
        };
        
        ParamValueReceived?.Invoke(this, new MavlinkParamValueEventArgs(param, e.Index, e.Count));
    }

    private async Task<bool> WaitForHeartbeatAsync(TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<bool>();
        using var cts = new CancellationTokenSource(timeout);
        
        void OnHeartbeat(object? s, EventArgs e)
        {
            tcs.TrySetResult(true);
        }

        HeartbeatReceived += OnHeartbeat;
        cts.Token.Register(() => tcs.TrySetResult(false));

        try
        {
            return await tcs.Task;
        }
        finally
        {
            HeartbeatReceived -= OnHeartbeat;
        }
    }

    public Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnecting...");

        try
        {
            if (_mavlink != null)
            {
                _mavlink.HeartbeatReceived -= OnMavlinkHeartbeat;
                _mavlink.ParamValueReceived -= OnMavlinkParamValue;
                _mavlink.Dispose();
                _mavlink = null;
            }

            _bluetoothConnection?.Dispose();
            _bluetoothConnection = null;

            _networkStream?.Dispose();
            _networkStream = null;

            _tcpClient?.Dispose();
            _tcpClient = null;

            _serialPort?.Dispose();
            _serialPort = null;

            _inputStream = null;
            _outputStream = null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during disconnect cleanup");
        }

        SetConnected(false);
        _logger.LogInformation("Disconnected");
        
        return Task.CompletedTask;
    }

    public Stream? GetTransportStream() => _inputStream;

    public void SendParamRequestList()
    {
        if (_currentConnectionType == ConnectionType.Bluetooth && _bluetoothConnection != null)
        {
            _ = _bluetoothConnection.SendParamRequestListAsync();
            return;
        }

        if (_mavlink == null)
        {
            _logger.LogWarning("Cannot send PARAM_REQUEST_LIST - not connected");
            return;
        }

        _ = _mavlink.SendParamRequestListAsync();
    }

    public void SendParamRequestRead(ushort paramIndex)
    {
        if (_currentConnectionType == ConnectionType.Bluetooth && _bluetoothConnection != null)
        {
            _ = _bluetoothConnection.SendParamRequestReadAsync(paramIndex);
            return;
        }

        if (_mavlink == null)
        {
            _logger.LogWarning("Cannot send PARAM_REQUEST_READ - not connected");
            return;
        }

        _ = _mavlink.SendParamRequestReadAsync(paramIndex);
    }

    public void SendParamSet(ParameterWriteRequest request)
    {
        if (_currentConnectionType == ConnectionType.Bluetooth && _bluetoothConnection != null)
        {
            _ = _bluetoothConnection.SendParamSetAsync(request.Name, request.Value);
            return;
        }

        if (_mavlink == null)
        {
            _logger.LogWarning("Cannot send PARAM_SET - not connected");
            return;
        }

        _ = _mavlink.SendParamSetAsync(request.Name, request.Value);
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            ConnectionStateChanged?.Invoke(this, connected);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _portScanTimer.Stop();
        _portScanTimer.Dispose();

        DisconnectAsync().Wait(TimeSpan.FromSeconds(2));
    }

    private class SerialPortInfoComparer : IEqualityComparer<SerialPortInfo>
    {
        public bool Equals(SerialPortInfo? x, SerialPortInfo? y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            return x.PortName == y.PortName && x.FriendlyName == y.FriendlyName;
        }

        public int GetHashCode(SerialPortInfo obj)
        {
            return HashCode.Combine(obj.PortName, obj.FriendlyName);
        }
    }
}
