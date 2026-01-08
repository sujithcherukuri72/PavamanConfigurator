using PavamanDroneConfigurator.Core.Enums;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Configuration settings for a single serial port.
/// Maps to ArduPilot SERIALx_PROTOCOL and SERIALx_BAUD parameters.
/// </summary>
public class SerialPortConfig
{
    /// <summary>
    /// Port index (0-7)
    /// </summary>
    public SerialPortIndex PortIndex { get; set; }

    /// <summary>
    /// Display name for the port (e.g., "TELEM1", "GPS", etc.)
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Physical port label on the flight controller
    /// </summary>
    public string PortLabel { get; set; } = string.Empty;

    /// <summary>
    /// Protocol assigned to this port (SERIALx_PROTOCOL)
    /// </summary>
    public SerialProtocol Protocol { get; set; } = SerialProtocol.None;

    /// <summary>
    /// Baud rate for this port (SERIALx_BAUD)
    /// </summary>
    public SerialBaudRate BaudRate { get; set; } = SerialBaudRate.Baud57600;

    /// <summary>
    /// Serial options bitmask (SERIALx_OPTIONS)
    /// </summary>
    public SerialOptions Options { get; set; } = SerialOptions.None;

    /// <summary>
    /// Whether this port is available on the current hardware
    /// </summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>
    /// Description of what this port is typically used for
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets the ArduPilot parameter name for protocol
    /// </summary>
    public string ProtocolParameterName => $"SERIAL{(int)PortIndex}_PROTOCOL";

    /// <summary>
    /// Gets the ArduPilot parameter name for baud rate
    /// </summary>
    public string BaudParameterName => $"SERIAL{(int)PortIndex}_BAUD";

    /// <summary>
    /// Gets the ArduPilot parameter name for options
    /// </summary>
    public string OptionsParameterName => $"SERIAL{(int)PortIndex}_OPTIONS";
}

/// <summary>
/// Complete serial configuration for all ports on the flight controller.
/// </summary>
public class SerialConfiguration
{
    /// <summary>
    /// Serial0 configuration - Usually Console/USB
    /// </summary>
    public SerialPortConfig Serial0 { get; set; } = new()
    {
        PortIndex = SerialPortIndex.Serial0,
        DisplayName = "Console/USB",
        PortLabel = "Serial0",
        Protocol = SerialProtocol.MAVLink2,
        BaudRate = SerialBaudRate.Baud115200,
        Description = "USB connection for ground station"
    };

    /// <summary>
    /// Serial1 configuration - TELEM1
    /// </summary>
    public SerialPortConfig Serial1 { get; set; } = new()
    {
        PortIndex = SerialPortIndex.Serial1,
        DisplayName = "TELEM1",
        PortLabel = "Serial1",
        Protocol = SerialProtocol.MAVLink2,
        BaudRate = SerialBaudRate.Baud57600,
        Description = "Primary telemetry radio"
    };

    /// <summary>
    /// Serial2 configuration - TELEM2
    /// </summary>
    public SerialPortConfig Serial2 { get; set; } = new()
    {
        PortIndex = SerialPortIndex.Serial2,
        DisplayName = "TELEM2",
        PortLabel = "Serial2",
        Protocol = SerialProtocol.MAVLink2,
        BaudRate = SerialBaudRate.Baud57600,
        Description = "Secondary telemetry/companion computer"
    };

    /// <summary>
    /// Serial3 configuration - GPS
    /// </summary>
    public SerialPortConfig Serial3 { get; set; } = new()
    {
        PortIndex = SerialPortIndex.Serial3,
        DisplayName = "GPS",
        PortLabel = "Serial3",
        Protocol = SerialProtocol.GPS,
        BaudRate = SerialBaudRate.Baud38400,
        Description = "Primary GPS module"
    };

    /// <summary>
    /// Serial4 configuration - GPS2/Additional
    /// </summary>
    public SerialPortConfig Serial4 { get; set; } = new()
    {
        PortIndex = SerialPortIndex.Serial4,
        DisplayName = "GPS2",
        PortLabel = "Serial4",
        Protocol = SerialProtocol.GPS,
        BaudRate = SerialBaudRate.Baud38400,
        Description = "Secondary GPS module"
    };

    /// <summary>
    /// Serial5 configuration - Additional
    /// </summary>
    public SerialPortConfig Serial5 { get; set; } = new()
    {
        PortIndex = SerialPortIndex.Serial5,
        DisplayName = "Serial5",
        PortLabel = "Serial5",
        Protocol = SerialProtocol.MAVLink1,
        BaudRate = SerialBaudRate.Baud57600,
        Description = "Additional serial port"
    };

    /// <summary>
    /// Serial6 configuration - Additional
    /// </summary>
    public SerialPortConfig Serial6 { get; set; } = new()
    {
        PortIndex = SerialPortIndex.Serial6,
        DisplayName = "Serial6",
        PortLabel = "Serial6",
        Protocol = SerialProtocol.None,
        BaudRate = SerialBaudRate.Baud1200,
        Description = "Additional serial port"
    };

    /// <summary>
    /// Serial7 configuration - Additional (if available)
    /// </summary>
    public SerialPortConfig Serial7 { get; set; } = new()
    {
        PortIndex = SerialPortIndex.Serial7,
        DisplayName = "Serial7",
        PortLabel = "Serial7",
        Protocol = SerialProtocol.None,
        BaudRate = SerialBaudRate.Baud57600,
        IsAvailable = false,
        Description = "Additional serial port (hardware dependent)"
    };

    /// <summary>
    /// Gets all port configurations as a list
    /// </summary>
    public List<SerialPortConfig> GetAllPorts()
    {
        return new List<SerialPortConfig>
        {
            Serial0, Serial1, Serial2, Serial3, Serial4, Serial5, Serial6, Serial7
        };
    }

    /// <summary>
    /// Gets port configuration by index
    /// </summary>
    public SerialPortConfig? GetPort(SerialPortIndex index)
    {
        return index switch
        {
            SerialPortIndex.Serial0 => Serial0,
            SerialPortIndex.Serial1 => Serial1,
            SerialPortIndex.Serial2 => Serial2,
            SerialPortIndex.Serial3 => Serial3,
            SerialPortIndex.Serial4 => Serial4,
            SerialPortIndex.Serial5 => Serial5,
            SerialPortIndex.Serial6 => Serial6,
            SerialPortIndex.Serial7 => Serial7,
            _ => null
        };
    }
}

/// <summary>
/// Represents a protocol option for UI display
/// </summary>
public class SerialProtocolOption
{
    public SerialProtocol Protocol { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresGPS { get; set; }
    public bool IsTelemetry { get; set; }
}

/// <summary>
/// Represents a baud rate option for UI display
/// </summary>
public class SerialBaudRateOption
{
    public SerialBaudRate BaudRate { get; set; }
    public string Label { get; set; } = string.Empty;
    public int ActualBaudRate { get; set; }
    public string RecommendedFor { get; set; } = string.Empty;
}

/// <summary>
/// PDRL-compliant serial configuration defaults
/// </summary>
public static class SerialConfigDefaults
{
    /// <summary>
    /// Gets PDRL-compliant default configuration
    /// </summary>
    public static SerialConfiguration GetPDRLDefaults()
    {
        return new SerialConfiguration
        {
            Serial0 = new SerialPortConfig
            {
                PortIndex = SerialPortIndex.Serial0,
                DisplayName = "Console/USB",
                PortLabel = "Serial0",
                Protocol = SerialProtocol.MAVLink2,
                BaudRate = SerialBaudRate.Baud115200,
                Description = "USB MAVLink connection"
            },
            Serial1 = new SerialPortConfig
            {
                PortIndex = SerialPortIndex.Serial1,
                DisplayName = "TELEM1",
                PortLabel = "Serial1",
                Protocol = SerialProtocol.MAVLink2,
                BaudRate = SerialBaudRate.Baud57600,
                Description = "Primary telemetry - MAVLink2 for PDRL compliance"
            },
            Serial2 = new SerialPortConfig
            {
                PortIndex = SerialPortIndex.Serial2,
                DisplayName = "TELEM2",
                PortLabel = "Serial2",
                Protocol = SerialProtocol.MAVLink2,
                BaudRate = SerialBaudRate.Baud57600,
                Description = "Backup telemetry/companion computer"
            },
            Serial3 = new SerialPortConfig
            {
                PortIndex = SerialPortIndex.Serial3,
                DisplayName = "GPS",
                PortLabel = "Serial3",
                Protocol = SerialProtocol.GPS,
                BaudRate = SerialBaudRate.Baud115200,
                Description = "Primary GPS - required for PDRL"
            },
            Serial4 = new SerialPortConfig
            {
                PortIndex = SerialPortIndex.Serial4,
                DisplayName = "GPS2",
                PortLabel = "Serial4",
                Protocol = SerialProtocol.GPS,
                BaudRate = SerialBaudRate.Baud115200,
                Description = "Secondary GPS for redundancy"
            },
            Serial5 = new SerialPortConfig
            {
                PortIndex = SerialPortIndex.Serial5,
                DisplayName = "Serial5",
                PortLabel = "Serial5",
                Protocol = SerialProtocol.None,
                BaudRate = SerialBaudRate.Baud57600,
                Description = "Available for peripherals"
            },
            Serial6 = new SerialPortConfig
            {
                PortIndex = SerialPortIndex.Serial6,
                DisplayName = "Serial6",
                PortLabel = "Serial6",
                Protocol = SerialProtocol.None,
                BaudRate = SerialBaudRate.Baud57600,
                Description = "Available for peripherals"
            }
        };
    }

    /// <summary>
    /// Gets recommended baud rate for a protocol
    /// </summary>
    public static SerialBaudRate GetRecommendedBaudRate(SerialProtocol protocol)
    {
        return protocol switch
        {
            SerialProtocol.MAVLink1 => SerialBaudRate.Baud57600,
            SerialProtocol.MAVLink2 => SerialBaudRate.Baud57600,
            SerialProtocol.GPS => SerialBaudRate.Baud115200,
            SerialProtocol.Rangefinder => SerialBaudRate.Baud115200,
            SerialProtocol.ESCTelemetry => SerialBaudRate.Baud115200,
            SerialProtocol.FrskySPort => SerialBaudRate.Baud57600,
            SerialProtocol.FrskyD => SerialBaudRate.Baud9600,
            SerialProtocol.CRSF => SerialBaudRate.Baud400000,
            SerialProtocol.MSP => SerialBaudRate.Baud115200,
            _ => SerialBaudRate.Baud57600
        };
    }
}
