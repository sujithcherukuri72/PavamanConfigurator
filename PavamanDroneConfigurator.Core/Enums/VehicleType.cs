namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// ArduPilot vehicle types for parameter metadata selection.
/// </summary>
public enum VehicleType
{
    /// <summary>
    /// Multicopter (quadcopter, hexacopter, octocopter, etc.)
    /// Uses apm.pdef.xml parameter definitions.
    /// </summary>
    Copter = 0,

    /// <summary>
    /// Fixed-wing aircraft.
    /// Uses ArduPlane.pdef.xml parameter definitions.
    /// </summary>
    Plane = 1,

    /// <summary>
    /// Ground rover vehicle.
    /// Uses APMrover2.pdef.xml parameter definitions.
    /// </summary>
    Rover = 2,

    /// <summary>
    /// Submarine or ROV (Remotely Operated Vehicle).
    /// Uses ArduSub.pdef.xml parameter definitions.
    /// </summary>
    Sub = 3,

    /// <summary>
    /// Antenna tracker for following vehicles.
    /// Uses AntennaTracker.pdef.xml parameter definitions.
    /// </summary>
    Tracker = 4,

    /// <summary>
    /// Unknown or not yet detected vehicle type.
    /// Falls back to Copter parameters.
    /// </summary>
    Unknown = 99
}
