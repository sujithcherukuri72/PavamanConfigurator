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
    private readonly System.Timers.Timer _heartbeatMonitorTimer;
    private DateTime _lastHeartbeatTime = DateTime.MinValue;
    private const int HEARTBEAT_TIMEOUT_SECONDS = 5;
    
    private List<SerialPortInfo> _cachedPorts = new();

    public bool IsConnected => _isConnected;

    public event EventHandler<bool>? ConnectionStateChanged;
    public event EventHandler<IEnumerable<SerialPortInfo>>? AvailableSerialPortsChanged;
    public event EventHandler<MavlinkParamValueEventArgs>? ParamValueReceived;
    public event EventHandler? HeartbeatReceived;
    public event EventHandler<HeartbeatDataEventArgs>? HeartbeatDataReceived;
    public event EventHandler<StatusTextEventArgs>? StatusTextReceived;
    public event EventHandler<RcChannelsEventArgs>? RcChannelsReceived;
    public event EventHandler<CommandAckEventArgs>? CommandAckReceived;

    public ConnectionService(ILogger<ConnectionService> logger)
    {
        _logger = logger;

        _portScanTimer = new System.Timers.Timer(3000);
        _portScanTimer.Elapsed += (_, _) => ScanSerialPorts();
        _portScanTimer.Start();
        
        // Heartbeat monitor to detect connection loss
        _heartbeatMonitorTimer = new System.Timers.Timer(2000);
        _heartbeatMonitorTimer.Elapsed += OnHeartbeatMonitorElapsed;
        
        ScanSerialPorts();
    }

    private void OnHeartbeatMonitorElapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isConnected) return;
        
        var timeSinceLastHeartbeat = DateTime.UtcNow - _lastHeartbeatTime;
        if (timeSinceLastHeartbeat.TotalSeconds > HEARTBEAT_TIMEOUT_SECONDS)
        {
            _logger.LogWarning("Heartbeat timeout - connection lost (no heartbeat for {Seconds}s)", 
                timeSinceLastHeartbeat.TotalSeconds);
            
            // Connection lost - disconnect and notify
            _ = Task.Run(async () =>
            {
                await DisconnectAsync();
            });
        }
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
            StartHeartbeatMonitor();
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
            StartHeartbeatMonitor();
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
        
        try
        {
            var success = await _bluetoothConnection.ConnectAsync(settings.BluetoothDeviceAddress ?? string.Empty);

            if (!success)
            {
                _logger.LogWarning("Failed to connect to Bluetooth device");
                _bluetoothConnection.Dispose();
                _bluetoothConnection = null;
                return false;
            }

            // For Bluetooth, we use the BluetoothMavConnection's internal MAVLink handling
            _bluetoothConnection.HeartbeatReceived += (s, e) => 
            {
                _lastHeartbeatTime = DateTime.UtcNow;
                HeartbeatReceived?.Invoke(this, EventArgs.Empty);
            };
            _bluetoothConnection.ParamValueReceived += (s, e) =>
            {
                var param = new DroneParameter { Name = e.Name, Value = e.Value };
                ParamValueReceived?.Invoke(this, new MavlinkParamValueEventArgs(param, e.Index, e.Count));
            };
            
            var heartbeatReceived = await WaitForHeartbeatAsync(TimeSpan.FromSeconds(15));
            
            if (heartbeatReceived)
            {
                SetConnected(true);
                StartHeartbeatMonitor();
                _logger.LogInformation("Bluetooth connection established");
                return true;
            }

            _logger.LogWarning("No heartbeat received on Bluetooth connection");
            _bluetoothConnection.Dispose();
            _bluetoothConnection = null;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bluetooth connection failed");
            _bluetoothConnection?.Dispose();
            _bluetoothConnection = null;
            throw;
        }
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
        _mavlink.HeartbeatDataReceived += OnMavlinkHeartbeatData;
        _mavlink.StatusTextReceived += OnMavlinkStatusText;
        _mavlink.RcChannelsReceived += OnMavlinkRcChannels;
        _mavlink.CommandAckReceived += OnMavlinkCommandAck;
        _mavlink.Initialize(_inputStream, _outputStream);
    }

    private void OnMavlinkHeartbeat(object? sender, (byte SystemId, byte ComponentId) e)
    {
        _lastHeartbeatTime = DateTime.UtcNow;
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

    private void OnMavlinkHeartbeatData(object? sender, HeartbeatData e)
    {
        HeartbeatDataReceived?.Invoke(this, new HeartbeatDataEventArgs
        {
            SystemId = e.SystemId,
            ComponentId = e.ComponentId,
            CustomMode = e.CustomMode,
            VehicleType = e.VehicleType,
            Autopilot = e.Autopilot,
            BaseMode = e.BaseMode,
            IsArmed = e.IsArmed
        });
    }

    private void OnMavlinkStatusText(object? sender, (byte Severity, string Text) e)
    {
        _logger.LogInformation("StatusText [{Severity}]: {Text}", e.Severity, e.Text);
        StatusTextReceived?.Invoke(this, new StatusTextEventArgs
        {
            Severity = e.Severity,
            Text = e.Text
        });
    }

    private void OnMavlinkRcChannels(object? sender, RcChannelsData e)
    {
        RcChannelsReceived?.Invoke(this, new RcChannelsEventArgs
        {
            Channel1 = e.Channel1,
            Channel2 = e.Channel2,
            Channel3 = e.Channel3,
            Channel4 = e.Channel4,
            Channel5 = e.Channel5,
            Channel6 = e.Channel6,
            Channel7 = e.Channel7,
            Channel8 = e.Channel8,
            ChannelCount = e.ChannelCount,
            Rssi = e.Rssi
        });
    }

    private void OnMavlinkCommandAck(object? sender, (ushort Command, byte Result) e)
    {
        CommandAckReceived?.Invoke(this, new CommandAckEventArgs
        {
            Command = e.Command,
            Result = e.Result
        });
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

    private void StartHeartbeatMonitor()
    {
        _lastHeartbeatTime = DateTime.UtcNow;
        _heartbeatMonitorTimer.Start();
        _logger.LogDebug("Heartbeat monitor started");
    }

    private void StopHeartbeatMonitor()
    {
        _heartbeatMonitorTimer.Stop();
        _logger.LogDebug("Heartbeat monitor stopped");
    }

    public Task DisconnectAsync()
    {
        _logger.LogInformation("Disconnecting...");

        StopHeartbeatMonitor();

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

    public void SendMotorTest(int motorInstance, int throttleType, float throttleValue, float timeout, int motorCount = 0, int testOrder = 0)
    {
        if (_currentConnectionType == ConnectionType.Bluetooth && _bluetoothConnection != null)
        {
            _ = _bluetoothConnection.SendMotorTestAsync(motorInstance, throttleType, throttleValue, timeout, motorCount, testOrder);
            return;
        }

        if (_mavlink == null)
        {
            _logger.LogWarning("Cannot send DO_MOTOR_TEST - not connected");
            return;
        }

        _ = _mavlink.SendMotorTestAsync(motorInstance, throttleType, throttleValue, timeout, motorCount, testOrder);
    }

    public void SendPreflightCalibration(int gyro, int mag, int groundPressure, int airspeed, int accel)
    {
        if (_currentConnectionType == ConnectionType.Bluetooth && _bluetoothConnection != null)
        {
            _ = _bluetoothConnection.SendPreflightCalibrationAsync(gyro, mag, groundPressure, airspeed, accel);
            return;
        }

        if (_mavlink == null)
        {
            _logger.LogWarning("Cannot send MAV_CMD_PREFLIGHT_CALIBRATION - not connected");
            return;
        }

        _ = _mavlink.SendPreflightCalibrationAsync(gyro, mag, groundPressure, airspeed, accel);
    }

    public void SendPreflightReboot(int autopilot, int companion)
    {
        if (_currentConnectionType == ConnectionType.Bluetooth && _bluetoothConnection != null)
        {
            _ = _bluetoothConnection.SendPreflightRebootAsync(autopilot, companion);
            return;
        }

        if (_mavlink == null)
        {
            _logger.LogWarning("Cannot send MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN - not connected");
            return;
        }

        _ = _mavlink.SendPreflightRebootAsync(autopilot, companion);
    }

    public void SendArmDisarm(bool arm, bool force = false)
    {
        if (_currentConnectionType == ConnectionType.Bluetooth && _bluetoothConnection != null)
        {
            _ = _bluetoothConnection.SendArmDisarmAsync(arm, force);
            return;
        }

        if (_mavlink == null)
        {
            _logger.LogWarning("Cannot send MAV_CMD_COMPONENT_ARM_DISARM - not connected");
            return;
        }

        _ = _mavlink.SendArmDisarmAsync(arm, force);
    }

    private void SetConnected(bool connected)
    {
        if (_isConnected != connected)
        {
            _isConnected = connected;
            _logger.LogInformation("Connection state changed: {Connected}", connected);
            ConnectionStateChanged?.Invoke(this, connected);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _portScanTimer.Stop();
        _portScanTimer.Dispose();
        
        _heartbeatMonitorTimer.Stop();
        _heartbeatMonitorTimer.Dispose();

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
