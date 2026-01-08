using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service implementation for serial port configuration operations.
/// Reads/writes ArduPilot SERIALx parameters via MAVLink parameter protocol.
/// </summary>
public class SerialConfigService : ISerialConfigService
{
    private readonly ILogger<SerialConfigService> _logger;
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

    public event EventHandler<SerialConfiguration>? ConfigurationChanged;
    public event EventHandler<string>? ParameterUpdated;

    public SerialConfigService(
        ILogger<SerialConfigService> logger,
        IParameterService parameterService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _parameterService = parameterService;
        _connectionService = connectionService;

        _parameterService.ParameterUpdated += OnParameterUpdated;
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        if (parameterName.StartsWith("SERIAL", StringComparison.OrdinalIgnoreCase))
        {
            ParameterUpdated?.Invoke(this, parameterName);
        }
    }

    #region Helper Methods

    private async Task<float?> GetParameterValueAsync(string name)
    {
        var param = await _parameterService.GetParameterAsync(name);
        return param?.Value;
    }

    private async Task<bool> SetParameterValueAsync(string name, float value)
    {
        var result = await _parameterService.SetParameterAsync(name, value);
        if (result)
        {
            _logger.LogDebug("Set {Parameter} = {Value}", name, value);
        }
        else
        {
            _logger.LogWarning("Failed to set {Parameter} = {Value}", name, value);
        }
        return result;
    }

    private string GetProtocolParamName(SerialPortIndex index) => $"SERIAL{(int)index}_PROTOCOL";
    private string GetBaudParamName(SerialPortIndex index) => $"SERIAL{(int)index}_BAUD";
    private string GetOptionsParamName(SerialPortIndex index) => $"SERIAL{(int)index}_OPTIONS";

    #endregion

    #region Port Configuration

    public async Task<SerialConfiguration?> GetSerialConfigurationAsync()
    {
        try
        {
            _logger.LogInformation("Loading serial configuration from vehicle");

            var config = new SerialConfiguration();

            // Load each port configuration
            foreach (SerialPortIndex index in Enum.GetValues<SerialPortIndex>())
            {
                var portConfig = await GetPortConfigAsync(index);
                if (portConfig != null)
                {
                    switch (index)
                    {
                        case SerialPortIndex.Serial0: config.Serial0 = portConfig; break;
                        case SerialPortIndex.Serial1: config.Serial1 = portConfig; break;
                        case SerialPortIndex.Serial2: config.Serial2 = portConfig; break;
                        case SerialPortIndex.Serial3: config.Serial3 = portConfig; break;
                        case SerialPortIndex.Serial4: config.Serial4 = portConfig; break;
                        case SerialPortIndex.Serial5: config.Serial5 = portConfig; break;
                        case SerialPortIndex.Serial6: config.Serial6 = portConfig; break;
                        case SerialPortIndex.Serial7: config.Serial7 = portConfig; break;
                    }
                }
            }

            ConfigurationChanged?.Invoke(this, config);
            _logger.LogInformation("Serial configuration loaded successfully");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading serial configuration");
            return null;
        }
    }

    public async Task<SerialPortConfig?> GetPortConfigAsync(SerialPortIndex portIndex)
    {
        try
        {
            var config = GetDefaultPortConfig(portIndex);

            var protocolValue = await GetParameterValueAsync(GetProtocolParamName(portIndex));
            if (protocolValue.HasValue)
            {
                config.Protocol = (SerialProtocol)(int)protocolValue.Value;
            }

            var baudValue = await GetParameterValueAsync(GetBaudParamName(portIndex));
            if (baudValue.HasValue)
            {
                config.BaudRate = (SerialBaudRate)(int)baudValue.Value;
            }

            var optionsValue = await GetParameterValueAsync(GetOptionsParamName(portIndex));
            if (optionsValue.HasValue)
            {
                config.Options = (SerialOptions)(int)optionsValue.Value;
            }

            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting port {PortIndex} configuration", portIndex);
            return null;
        }
    }

    public async Task<bool> UpdatePortConfigAsync(SerialPortConfig config)
    {
        try
        {
            _logger.LogInformation("Updating port {PortIndex} configuration", config.PortIndex);

            var success = true;
            success &= await SetParameterValueAsync(GetProtocolParamName(config.PortIndex), (float)(int)config.Protocol);
            success &= await SetParameterValueAsync(GetBaudParamName(config.PortIndex), (float)(int)config.BaudRate);
            success &= await SetParameterValueAsync(GetOptionsParamName(config.PortIndex), (float)(int)config.Options);

            if (success)
            {
                _logger.LogInformation("Port {PortIndex} configuration updated: Protocol={Protocol}, Baud={Baud}",
                    config.PortIndex, config.Protocol, config.BaudRate);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating port {PortIndex} configuration", config.PortIndex);
            return false;
        }
    }

    public async Task<bool> UpdateSerialConfigurationAsync(SerialConfiguration config)
    {
        try
        {
            _logger.LogInformation("Updating all serial port configurations");

            var success = true;
            var updatedCount = 0;

            foreach (var port in config.GetAllPorts().Where(p => p.IsAvailable))
            {
                if (await UpdatePortConfigAsync(port))
                {
                    updatedCount++;
                }
                else
                {
                    success = false;
                }
            }

            _logger.LogInformation("Serial configuration update: {Updated} ports updated, success={Success}",
                updatedCount, success);

            if (success)
            {
                ConfigurationChanged?.Invoke(this, config);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating serial configuration");
            return false;
        }
    }

    #endregion

    #region Individual Parameter Operations

    public async Task<bool> SetPortProtocolAsync(SerialPortIndex portIndex, SerialProtocol protocol)
    {
        return await SetParameterValueAsync(GetProtocolParamName(portIndex), (float)(int)protocol);
    }

    public async Task<bool> SetPortBaudRateAsync(SerialPortIndex portIndex, SerialBaudRate baudRate)
    {
        return await SetParameterValueAsync(GetBaudParamName(portIndex), (float)(int)baudRate);
    }

    public async Task<bool> SetPortOptionsAsync(SerialPortIndex portIndex, SerialOptions options)
    {
        return await SetParameterValueAsync(GetOptionsParamName(portIndex), (float)(int)options);
    }

    public async Task<SerialProtocol> GetPortProtocolAsync(SerialPortIndex portIndex)
    {
        var value = await GetParameterValueAsync(GetProtocolParamName(portIndex));
        return value.HasValue ? (SerialProtocol)(int)value.Value : SerialProtocol.None;
    }

    public async Task<SerialBaudRate> GetPortBaudRateAsync(SerialPortIndex portIndex)
    {
        var value = await GetParameterValueAsync(GetBaudParamName(portIndex));
        return value.HasValue ? (SerialBaudRate)(int)value.Value : SerialBaudRate.Baud57600;
    }

    #endregion

    #region Defaults and Validation

    public async Task<bool> ApplyPDRLDefaultsAsync()
    {
        _logger.LogInformation("Applying PDRL-compliant serial configuration defaults");

        var defaults = SerialConfigDefaults.GetPDRLDefaults();
        return await UpdateSerialConfigurationAsync(defaults);
    }

    public List<string> ValidateConfiguration(SerialConfiguration config)
    {
        var warnings = new List<string>();

        // Check for GPS port assignment (required for PDRL)
        var gpsports = GetPortsUsingProtocol(config, SerialProtocol.GPS);
        if (gpsports.Count == 0)
        {
            warnings.Add("No GPS port configured - GPS is required for PDRL compliance");
        }

        // Check for MAVLink telemetry (required for PDRL)
        var mavlinkPorts = GetPortsUsingProtocol(config, SerialProtocol.MAVLink1)
            .Concat(GetPortsUsingProtocol(config, SerialProtocol.MAVLink2))
            .ToList();
        if (mavlinkPorts.Count == 0)
        {
            warnings.Add("No MAVLink telemetry port configured - required for ground control");
        }

        // Recommend MAVLink2 over MAVLink1
        var mavlink1Ports = GetPortsUsingProtocol(config, SerialProtocol.MAVLink1);
        if (mavlink1Ports.Count > 0 && GetPortsUsingProtocol(config, SerialProtocol.MAVLink2).Count == 0)
        {
            warnings.Add("Consider using MAVLink2 instead of MAVLink1 for better performance");
        }

        // Check for duplicate critical protocols
        var rangeFinderPorts = GetPortsUsingProtocol(config, SerialProtocol.Rangefinder);
        if (rangeFinderPorts.Count > 1)
        {
            warnings.Add("Multiple rangefinder ports configured - usually only one is needed");
        }

        // Validate baud rates for GPS
        foreach (var portIndex in gpsports)
        {
            var port = config.GetPort(portIndex);
            if (port != null && (int)port.BaudRate < 38)
            {
                warnings.Add($"GPS port {portIndex} has low baud rate - consider 38400 or higher for reliable GPS data");
            }
        }

        // Check Serial0 (USB) configuration
        if (config.Serial0.Protocol == SerialProtocol.None)
        {
            warnings.Add("Serial0 (USB) is disabled - this will prevent USB connections");
        }

        foreach (var warning in warnings)
        {
            _logger.LogWarning("Serial config validation: {Warning}", warning);
        }

        return warnings;
    }

    public bool IsProtocolInUse(SerialConfiguration config, SerialProtocol protocol)
    {
        return config.GetAllPorts().Any(p => p.Protocol == protocol && p.IsAvailable);
    }

    public List<SerialPortIndex> GetPortsUsingProtocol(SerialConfiguration config, SerialProtocol protocol)
    {
        return config.GetAllPorts()
            .Where(p => p.Protocol == protocol && p.IsAvailable)
            .Select(p => p.PortIndex)
            .ToList();
    }

    #endregion

    #region Protocol and Baud Rate Information

    public IEnumerable<SerialProtocolOption> GetProtocolOptions()
    {
        return new List<SerialProtocolOption>
        {
            new() { Protocol = SerialProtocol.None, Label = "None", Description = "Port disabled" },
            new() { Protocol = SerialProtocol.MAVLink1, Label = "MAVLink1", Description = "MAVLink version 1 (legacy)", IsTelemetry = true },
            new() { Protocol = SerialProtocol.MAVLink2, Label = "MAVLink2", Description = "MAVLink version 2 (recommended)", IsTelemetry = true },
            new() { Protocol = SerialProtocol.FrskyD, Label = "FrSky D", Description = "FrSky D protocol telemetry" },
            new() { Protocol = SerialProtocol.FrskySPort, Label = "FrSky SPort", Description = "FrSky Smart Port telemetry" },
            new() { Protocol = SerialProtocol.GPS, Label = "GPS", Description = "GPS module (auto-detect NMEA/UBX)", RequiresGPS = true },
            new() { Protocol = SerialProtocol.AlexmosGimbal, Label = "Alexmos Gimbal", Description = "Alexmos/SimpleBGC gimbal" },
            new() { Protocol = SerialProtocol.SToRM32MAVLink, Label = "SToRM32 Gimbal", Description = "SToRM32 gimbal via MAVLink" },
            new() { Protocol = SerialProtocol.Rangefinder, Label = "Rangefinder", Description = "Serial rangefinder/lidar" },
            new() { Protocol = SerialProtocol.FrSkyPassthrough, Label = "FrSky Passthrough", Description = "FrSky telemetry passthrough" },
            new() { Protocol = SerialProtocol.Lidar360, Label = "Lidar360", Description = "360-degree lidar" },
            new() { Protocol = SerialProtocol.Beacon, Label = "Beacon", Description = "Beacon positioning" },
            new() { Protocol = SerialProtocol.ESCTelemetry, Label = "ESC Telemetry", Description = "ESC telemetry data" },
            new() { Protocol = SerialProtocol.OpticalFlow, Label = "Optical Flow", Description = "Optical flow sensor" },
            new() { Protocol = SerialProtocol.NMEAOutput, Label = "NMEA Output", Description = "NMEA GPS output" },
            new() { Protocol = SerialProtocol.RCIN, Label = "RC Input", Description = "Serial RC receiver input" },
            new() { Protocol = SerialProtocol.LTM, Label = "LTM", Description = "Light Telemetry protocol" },
            new() { Protocol = SerialProtocol.RunCam, Label = "RunCam", Description = "RunCam camera control" },
            new() { Protocol = SerialProtocol.MSP, Label = "MSP", Description = "MultiWii Serial Protocol" },
            new() { Protocol = SerialProtocol.MSPDisplayPort, Label = "MSP DisplayPort", Description = "MSP OSD DisplayPort" },
            new() { Protocol = SerialProtocol.DJIFPV, Label = "DJI FPV", Description = "DJI FPV OSD" },
            new() { Protocol = SerialProtocol.CRSF, Label = "CRSF/Crossfire", Description = "TBS Crossfire protocol" }
        };
    }

    public IEnumerable<SerialBaudRateOption> GetBaudRateOptions()
    {
        return new List<SerialBaudRateOption>
        {
            new() { BaudRate = SerialBaudRate.Baud1200, Label = "1200", ActualBaudRate = 1200, RecommendedFor = "Very slow devices" },
            new() { BaudRate = SerialBaudRate.Baud2400, Label = "2400", ActualBaudRate = 2400, RecommendedFor = "Slow devices" },
            new() { BaudRate = SerialBaudRate.Baud4800, Label = "4800", ActualBaudRate = 4800, RecommendedFor = "Legacy devices" },
            new() { BaudRate = SerialBaudRate.Baud9600, Label = "9600", ActualBaudRate = 9600, RecommendedFor = "FrSky D, basic telemetry" },
            new() { BaudRate = SerialBaudRate.Baud19200, Label = "19200", ActualBaudRate = 19200, RecommendedFor = "Some GPS modules" },
            new() { BaudRate = SerialBaudRate.Baud38400, Label = "38400", ActualBaudRate = 38400, RecommendedFor = "GPS, telemetry radios" },
            new() { BaudRate = SerialBaudRate.Baud57600, Label = "57600", ActualBaudRate = 57600, RecommendedFor = "Telemetry radios (default)" },
            new() { BaudRate = SerialBaudRate.Baud111100, Label = "111100", ActualBaudRate = 111100, RecommendedFor = "Special applications" },
            new() { BaudRate = SerialBaudRate.Baud115200, Label = "115200", ActualBaudRate = 115200, RecommendedFor = "High-speed GPS, USB" },
            new() { BaudRate = SerialBaudRate.Baud230400, Label = "230400", ActualBaudRate = 230400, RecommendedFor = "High-speed peripherals" },
            new() { BaudRate = SerialBaudRate.Baud256000, Label = "256000", ActualBaudRate = 256000, RecommendedFor = "ESC telemetry" },
            new() { BaudRate = SerialBaudRate.Baud460800, Label = "460800", ActualBaudRate = 460800, RecommendedFor = "Very high-speed" },
            new() { BaudRate = SerialBaudRate.Baud500000, Label = "500000", ActualBaudRate = 500000, RecommendedFor = "Very high-speed" },
            new() { BaudRate = SerialBaudRate.Baud921600, Label = "921600", ActualBaudRate = 921600, RecommendedFor = "Maximum speed" },
            new() { BaudRate = SerialBaudRate.Baud1500000, Label = "1500000", ActualBaudRate = 1500000, RecommendedFor = "Ultra high-speed" }
        };
    }

    public SerialBaudRate GetRecommendedBaudRate(SerialProtocol protocol)
    {
        return SerialConfigDefaults.GetRecommendedBaudRate(protocol);
    }

    public string GetProtocolDescription(SerialProtocol protocol)
    {
        return GetProtocolOptions().FirstOrDefault(p => p.Protocol == protocol)?.Description ?? "Unknown protocol";
    }

    #endregion

    #region Hardware Detection

    public async Task<List<SerialPortIndex>> DetectAvailablePortsAsync()
    {
        var availablePorts = new List<SerialPortIndex>();

        // Check each port by attempting to read its protocol parameter
        foreach (SerialPortIndex index in Enum.GetValues<SerialPortIndex>())
        {
            var value = await GetParameterValueAsync(GetProtocolParamName(index));
            if (value.HasValue)
            {
                availablePorts.Add(index);
            }
        }

        _logger.LogInformation("Detected {Count} available serial ports: {Ports}",
            availablePorts.Count, string.Join(", ", availablePorts));

        return availablePorts;
    }

    public async Task<int> GetAvailablePortCountAsync()
    {
        var ports = await DetectAvailablePortsAsync();
        return ports.Count;
    }

    #endregion

    #region Default Port Configurations

    private SerialPortConfig GetDefaultPortConfig(SerialPortIndex index)
    {
        return index switch
        {
            SerialPortIndex.Serial0 => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = "Console/USB",
                PortLabel = "Serial0",
                Protocol = SerialProtocol.MAVLink2,
                BaudRate = SerialBaudRate.Baud115200,
                Description = "USB MAVLink connection"
            },
            SerialPortIndex.Serial1 => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = "TELEM1",
                PortLabel = "Serial1",
                Protocol = SerialProtocol.MAVLink2,
                BaudRate = SerialBaudRate.Baud57600,
                Description = "Primary telemetry port"
            },
            SerialPortIndex.Serial2 => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = "TELEM2",
                PortLabel = "Serial2",
                Protocol = SerialProtocol.MAVLink2,
                BaudRate = SerialBaudRate.Baud57600,
                Description = "Secondary telemetry/companion"
            },
            SerialPortIndex.Serial3 => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = "GPS",
                PortLabel = "Serial3",
                Protocol = SerialProtocol.GPS,
                BaudRate = SerialBaudRate.Baud38400,
                Description = "Primary GPS module"
            },
            SerialPortIndex.Serial4 => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = "GPS2",
                PortLabel = "Serial4",
                Protocol = SerialProtocol.GPS,
                BaudRate = SerialBaudRate.Baud38400,
                Description = "Secondary GPS module"
            },
            SerialPortIndex.Serial5 => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = "Serial5",
                PortLabel = "Serial5",
                Protocol = SerialProtocol.MAVLink1,
                BaudRate = SerialBaudRate.Baud57600,
                Description = "Additional serial port"
            },
            SerialPortIndex.Serial6 => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = "Serial6",
                PortLabel = "Serial6",
                Protocol = SerialProtocol.None,
                BaudRate = SerialBaudRate.Baud1200,
                Description = "Additional serial port"
            },
            SerialPortIndex.Serial7 => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = "Serial7",
                PortLabel = "Serial7",
                Protocol = SerialProtocol.None,
                BaudRate = SerialBaudRate.Baud57600,
                IsAvailable = false,
                Description = "Additional serial port (if available)"
            },
            _ => new SerialPortConfig
            {
                PortIndex = index,
                DisplayName = $"Serial{(int)index}",
                PortLabel = $"Serial{(int)index}"
            }
        };
    }

    #endregion
}
