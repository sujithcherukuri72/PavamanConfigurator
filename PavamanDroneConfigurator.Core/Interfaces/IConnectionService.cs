using PavanamDroneConfigurator.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PavanamDroneConfigurator.Core.Interfaces;

public interface IConnectionService
{
    Task<bool> ConnectAsync(ConnectionSettings settings);
    Task DisconnectAsync();
    bool IsConnected { get; }
    event EventHandler<bool>? ConnectionStateChanged;
    
    // Serial port methods
    IEnumerable<SerialPortInfo> GetAvailableSerialPorts();
    event EventHandler<IEnumerable<SerialPortInfo>>? AvailableSerialPortsChanged;
    
    // Bluetooth methods
    Task<IEnumerable<BluetoothDeviceInfo>> GetAvailableBluetoothDevicesAsync();
    
    Stream? GetTransportStream();
    
    // MAVLink message events
    event EventHandler<MavlinkParamValueEventArgs>? ParamValueReceived;
    event EventHandler? HeartbeatReceived;
    
    // MAVLink send methods for ParameterService to call
    void SendParamRequestList();
    void SendParamRequestRead(ushort paramIndex);
    void SendParamSet(ParameterWriteRequest request);
}

// Event args for PARAM_VALUE messages
public class MavlinkParamValueEventArgs : EventArgs
{
    public DroneParameter Parameter { get; }
    public ushort ParamIndex { get; }
    public ushort ParamCount { get; }

    public MavlinkParamValueEventArgs(DroneParameter parameter, ushort paramIndex, ushort paramCount)
    {
        Parameter = parameter;
        ParamIndex = paramIndex;
        ParamCount = paramCount;
    }
}
