using Asv.Mavlink;
using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PavanamDroneConfigurator.Infrastructure.MAVLink;

/// <summary>
/// Bluetooth MAVLink Connection using SPP (Serial Port Profile)
/// Modernized for .NET 9 using InTheHand.Net.Bluetooth
/// Production-ready cross-platform Bluetooth RFCOMM implementation
/// </summary>
public class BluetoothMavConnection : IDisposable
{
    private readonly ILogger _logger;
    private readonly Guid _sppServiceClassId = new Guid("00001101-0000-1000-8000-00805F9B34FB"); // SPP UUID
    private BluetoothClient? _bluetoothClient;
    private Stream? _stream;
    private AsvMavlinkWrapper? _mavlinkWrapper;
    private bool _disposed;
    private bool _isConnected;

    // Events matching existing connection architecture
    public event EventHandler<(byte SystemId, byte ComponentId)>? HeartbeatReceived;
    public event EventHandler<(string Name, float Value, ushort Index, ushort Count)>? ParamValueReceived;
    public event EventHandler<bool>? ConnectionStateChanged;

    public bool IsConnected => _isConnected;

    public BluetoothMavConnection(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Connect to Bluetooth device using SPP/RFCOMM
    /// Modern async implementation for .NET 9
    /// Throws on failure - no retries
    /// </summary>
    public async Task<bool> ConnectAsync(string deviceAddress)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BluetoothMavConnection));

        try
        {
            // Close any previous connection
            await CloseAsync();

            _logger.LogInformation("Connecting to Bluetooth device: {Address}", deviceAddress);

            // Parse Bluetooth address
            var address = BluetoothAddress.Parse(deviceAddress);

            // Create RFCOMM socket (SPP)
            _bluetoothClient = new BluetoothClient();

            // Blocking connect to SPP service
            _logger.LogDebug("Establishing RFCOMM connection to SPP service...");
            await Task.Run(() => _bluetoothClient.Connect(address, _sppServiceClassId));

            if (!_bluetoothClient.Connected)
            {
                throw new IOException("Bluetooth RFCOMM connection failed");
            }

            // Get the network stream (combined input/output)
            _stream = _bluetoothClient.GetStream();

            if (_stream == null)
            {
                throw new IOException("Failed to get Bluetooth stream");
            }

            _logger.LogInformation("Bluetooth SPP connection established");

            // Initialize MAVLink connection using ASV.Mavlink wrapper
            _mavlinkWrapper = new AsvMavlinkWrapper(_logger);
            _mavlinkWrapper.HeartbeatReceived += OnMavlinkHeartbeat;
            _mavlinkWrapper.ParamValueReceived += OnMavlinkParamValue;
            _mavlinkWrapper.Initialize(_stream, _stream);

            _isConnected = true;
            ConnectionStateChanged?.Invoke(this, true);

            _logger.LogInformation("Bluetooth MAVLink connection ready");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bluetooth connection failed");
            
            // Clean up on failure
            await CloseAsync();
            throw;
        }
    }

    /// <summary>
    /// Connect to Bluetooth device by name
    /// Discovers devices and connects to first match
    /// </summary>
    public async Task<bool> ConnectByNameAsync(string deviceName)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(BluetoothMavConnection));

        try
        {
            _logger.LogInformation("Discovering Bluetooth devices...");

            var client = new BluetoothClient();
            var devices = await Task.Run(() => client.DiscoverDevices().ToList());

            foreach (var device in devices)
            {
                if (device.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Found device: {Name} ({Address})", device.DeviceName, device.DeviceAddress);
                    return await ConnectAsync(device.DeviceAddress.ToString());
                }
            }

            throw new IOException($"Bluetooth device not found: {deviceName}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect by device name");
            throw;
        }
    }

    /// <summary>
    /// Close Bluetooth connection
    /// Suppresses exceptions during teardown
    /// </summary>
    public async Task CloseAsync()
    {
        if (!_isConnected)
            return;

        try
        {
            _logger.LogInformation("Closing Bluetooth connection");

            // Unsubscribe from events
            if (_mavlinkWrapper != null)
            {
                _mavlinkWrapper.HeartbeatReceived -= OnMavlinkHeartbeat;
                _mavlinkWrapper.ParamValueReceived -= OnMavlinkParamValue;
                _mavlinkWrapper.Dispose();
                _mavlinkWrapper = null;
            }

            // Close stream
            if (_stream != null)
            {
                await _stream.DisposeAsync();
                _stream = null;
            }

            // Close Bluetooth client
            _bluetoothClient?.Close();
            _bluetoothClient?.Dispose();
            _bluetoothClient = null;

            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);

            _logger.LogInformation("Bluetooth connection closed");
        }
        catch (Exception ex)
        {
            // Suppress exceptions during teardown
            _logger.LogDebug(ex, "Exception suppressed during Bluetooth close");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Send PARAM_REQUEST_LIST to drone
    /// Throws if connection is not active
    /// </summary>
    public async Task SendParamRequestListAsync(CancellationToken ct = default)
    {
        if (!_isConnected || _mavlinkWrapper == null)
            throw new InvalidOperationException("Bluetooth connection is not active");

        await _mavlinkWrapper.SendParamRequestListAsync(ct);
    }

    /// <summary>
    /// Send PARAM_REQUEST_READ to drone
    /// Throws if connection is not active
    /// </summary>
    public async Task SendParamRequestReadAsync(ushort paramIndex, CancellationToken ct = default)
    {
        if (!_isConnected || _mavlinkWrapper == null)
            throw new InvalidOperationException("Bluetooth connection is not active");

        await _mavlinkWrapper.SendParamRequestReadAsync(paramIndex, ct);
    }

    /// <summary>
    /// Send PARAM_SET to drone
    /// Throws if connection is not active
    /// </summary>
    public async Task SendParamSetAsync(string paramName, float paramValue, CancellationToken ct = default)
    {
        if (!_isConnected || _mavlinkWrapper == null)
            throw new InvalidOperationException("Bluetooth connection is not active");

        await _mavlinkWrapper.SendParamSetAsync(paramName, paramValue, ct);
    }

    private void OnMavlinkHeartbeat(object? sender, (byte SystemId, byte ComponentId) e)
    {
        HeartbeatReceived?.Invoke(this, e);
    }

    private void OnMavlinkParamValue(object? sender, (string Name, float Value, ushort Index, ushort Count) e)
    {
        ParamValueReceived?.Invoke(this, e);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        CloseAsync().GetAwaiter().GetResult();
        _disposed = true;
    }
}
