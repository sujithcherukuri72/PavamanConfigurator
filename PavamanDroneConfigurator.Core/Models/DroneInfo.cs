namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Represents drone identification and version information.
/// Contains data typically displayed in a "Drone Details" view.
/// </summary>
public class DroneInfo
{
    /// <summary>
    /// Unique drone identifier (typically from BRD_SERIAL_NUM parameter or UID)
    /// </summary>
    public string DroneId { get; set; } = string.Empty;

    /// <summary>
    /// Flight Controller ID (hardware identifier)
    /// </summary>
    public string FcId { get; set; } = string.Empty;

    /// <summary>
    /// Firmware version string (e.g., "4.4.4")
    /// </summary>
    public string FirmwareVersion { get; set; } = string.Empty;

    /// <summary>
    /// Code checksum (firmware verification hash)
    /// </summary>
    public string CodeChecksum { get; set; } = string.Empty;

    /// <summary>
    /// Data checksum (configuration verification hash)
    /// </summary>
    public string DataChecksum { get; set; } = string.Empty;

    /// <summary>
    /// Vehicle type from heartbeat (e.g., "Quadcopter", "Hexacopter")
    /// </summary>
    public string VehicleType { get; set; } = string.Empty;

    /// <summary>
    /// Autopilot type (e.g., "ArduPilot", "PX4")
    /// </summary>
    public string AutopilotType { get; set; } = string.Empty;

    /// <summary>
    /// MAVLink system ID
    /// </summary>
    public byte SystemId { get; set; }

    /// <summary>
    /// MAVLink component ID
    /// </summary>
    public byte ComponentId { get; set; }

    /// <summary>
    /// Board type/name (e.g., "Pixhawk 4", "CubeOrange")
    /// </summary>
    public string BoardType { get; set; } = string.Empty;

    /// <summary>
    /// Git hash or build identifier
    /// </summary>
    public string GitHash { get; set; } = string.Empty;

    /// <summary>
    /// Whether the drone is currently armed
    /// </summary>
    public bool IsArmed { get; set; }

    /// <summary>
    /// Current flight mode name
    /// </summary>
    public string FlightMode { get; set; } = string.Empty;
}
