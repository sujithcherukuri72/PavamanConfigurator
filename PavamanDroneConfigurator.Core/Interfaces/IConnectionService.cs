using PavamanDroneConfigurator.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PavamanDroneConfigurator.Core.Interfaces;

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
    event EventHandler<HeartbeatDataEventArgs>? HeartbeatDataReceived;
    event EventHandler<StatusTextEventArgs>? StatusTextReceived;
    event EventHandler<RcChannelsEventArgs>? RcChannelsReceived;
    event EventHandler<CommandAckEventArgs>? CommandAckReceived;
    
    // MAVLink send methods for ParameterService to call
    void SendParamRequestList();
    void SendParamRequestRead(ushort paramIndex);
    void SendParamSet(ParameterWriteRequest request);
    
    // Motor test command (DO_MOTOR_TEST MAV_CMD = 209)
    void SendMotorTest(int motorInstance, int throttleType, float throttleValue, float timeout, int motorCount = 0, int testOrder = 0);
    
    // Calibration command (MAV_CMD_PREFLIGHT_CALIBRATION = 241)
    void SendPreflightCalibration(int gyro, int mag, int groundPressure, int airspeed, int accel);
    
    // Reboot command (MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN = 246)
    void SendPreflightReboot(int autopilot, int companion);
    
    // Arm/Disarm command (MAV_CMD_COMPONENT_ARM_DISARM = 400)
    void SendArmDisarm(bool arm, bool force = false);
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

// Event args for HEARTBEAT data
public class HeartbeatDataEventArgs : EventArgs
{
    public byte SystemId { get; set; }
    public byte ComponentId { get; set; }
    public uint CustomMode { get; set; }
    public byte VehicleType { get; set; }
    public byte Autopilot { get; set; }
    public byte BaseMode { get; set; }
    public bool IsArmed { get; set; }
}

// Event args for STATUSTEXT messages
public class StatusTextEventArgs : EventArgs
{
    public byte Severity { get; set; }
    public string Text { get; set; } = string.Empty;
}

// Event args for RC_CHANNELS messages
public class RcChannelsEventArgs : EventArgs
{
    public ushort Channel1 { get; set; }
    public ushort Channel2 { get; set; }
    public ushort Channel3 { get; set; }
    public ushort Channel4 { get; set; }
    public ushort Channel5 { get; set; }
    public ushort Channel6 { get; set; }
    public ushort Channel7 { get; set; }
    public ushort Channel8 { get; set; }
    public byte ChannelCount { get; set; }
    public byte Rssi { get; set; }
    
    public ushort GetChannel(int number) => number switch
    {
        1 => Channel1,
        2 => Channel2,
        3 => Channel3,
        4 => Channel4,
        5 => Channel5,
        6 => Channel6,
        7 => Channel7,
        8 => Channel8,
        _ => 0
    };
}

// Event args for COMMAND_ACK messages
public class CommandAckEventArgs : EventArgs
{
    public ushort Command { get; set; }
    public byte Result { get; set; }
    public bool IsSuccess => Result == 0; // MAV_RESULT_ACCEPTED
}
