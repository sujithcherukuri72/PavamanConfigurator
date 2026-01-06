using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

public class ConnectionSettings
{
    public ConnectionType Type { get; set; }
    
    // Serial settings
    public string? PortName { get; set; }
    public int BaudRate { get; set; } = 115200;
    
    // TCP settings
    public string? IpAddress { get; set; }
    public int Port { get; set; } = 5760;
    
    // Bluetooth settings
    public string? BluetoothDeviceAddress { get; set; }
    public string? BluetoothDeviceName { get; set; }
}
