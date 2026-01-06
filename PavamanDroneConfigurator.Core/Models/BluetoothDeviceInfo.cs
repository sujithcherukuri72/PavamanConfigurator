namespace pavamanDroneConfigurator.Core.Models;

public class BluetoothDeviceInfo
{
    public required string DeviceAddress { get; set; }
    public required string DeviceName { get; set; }
    public bool IsConnected { get; set; }
    public bool IsPaired { get; set; }
}
