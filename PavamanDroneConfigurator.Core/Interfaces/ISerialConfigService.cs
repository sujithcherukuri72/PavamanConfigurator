using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// Service interface for serial port configuration operations.
/// Handles reading/writing ArduPilot SERIALx parameters via MAVLink.
/// </summary>
public interface ISerialConfigService
{
    #region Events

    /// <summary>
    /// Fired when serial configuration is updated from the vehicle
    /// </summary>
    event EventHandler<SerialConfiguration>? ConfigurationChanged;

    /// <summary>
    /// Fired when a specific serial parameter is updated
    /// </summary>
    event EventHandler<string>? ParameterUpdated;

    #endregion

    #region Port Configuration

    /// <summary>
    /// Get configuration for all serial ports
    /// </summary>
    Task<SerialConfiguration?> GetSerialConfigurationAsync();

    /// <summary>
    /// Get configuration for a specific serial port
    /// </summary>
    Task<SerialPortConfig?> GetPortConfigAsync(SerialPortIndex portIndex);

    /// <summary>
    /// Update configuration for a specific serial port
    /// </summary>
    Task<bool> UpdatePortConfigAsync(SerialPortConfig config);

    /// <summary>
    /// Update configuration for all serial ports
    /// </summary>
    Task<bool> UpdateSerialConfigurationAsync(SerialConfiguration config);

    #endregion

    #region Individual Parameter Operations

    /// <summary>
    /// Set protocol for a serial port
    /// Parameter: SERIALx_PROTOCOL
    /// </summary>
    Task<bool> SetPortProtocolAsync(SerialPortIndex portIndex, SerialProtocol protocol);

    /// <summary>
    /// Set baud rate for a serial port
    /// Parameter: SERIALx_BAUD
    /// </summary>
    Task<bool> SetPortBaudRateAsync(SerialPortIndex portIndex, SerialBaudRate baudRate);

    /// <summary>
    /// Set options for a serial port
    /// Parameter: SERIALx_OPTIONS
    /// </summary>
    Task<bool> SetPortOptionsAsync(SerialPortIndex portIndex, SerialOptions options);

    /// <summary>
    /// Get protocol for a serial port
    /// </summary>
    Task<SerialProtocol> GetPortProtocolAsync(SerialPortIndex portIndex);

    /// <summary>
    /// Get baud rate for a serial port
    /// </summary>
    Task<SerialBaudRate> GetPortBaudRateAsync(SerialPortIndex portIndex);

    #endregion

    #region Defaults and Validation

    /// <summary>
    /// Apply PDRL-compliant default configuration
    /// </summary>
    Task<bool> ApplyPDRLDefaultsAsync();

    /// <summary>
    /// Validate serial configuration for conflicts and PDRL compliance
    /// </summary>
    /// <returns>List of warnings/issues</returns>
    List<string> ValidateConfiguration(SerialConfiguration config);

    /// <summary>
    /// Check if a protocol is assigned to any port
    /// </summary>
    bool IsProtocolInUse(SerialConfiguration config, SerialProtocol protocol);

    /// <summary>
    /// Get ports using a specific protocol
    /// </summary>
    List<SerialPortIndex> GetPortsUsingProtocol(SerialConfiguration config, SerialProtocol protocol);

    #endregion

    #region Protocol and Baud Rate Information

    /// <summary>
    /// Get all available protocol options with descriptions
    /// </summary>
    IEnumerable<SerialProtocolOption> GetProtocolOptions();

    /// <summary>
    /// Get all available baud rate options
    /// </summary>
    IEnumerable<SerialBaudRateOption> GetBaudRateOptions();

    /// <summary>
    /// Get recommended baud rate for a protocol
    /// </summary>
    SerialBaudRate GetRecommendedBaudRate(SerialProtocol protocol);

    /// <summary>
    /// Get description for a protocol
    /// </summary>
    string GetProtocolDescription(SerialProtocol protocol);

    #endregion

    #region Hardware Detection

    /// <summary>
    /// Detect which serial ports are available on the connected hardware
    /// </summary>
    Task<List<SerialPortIndex>> DetectAvailablePortsAsync();

    /// <summary>
    /// Get the number of serial ports available
    /// </summary>
    Task<int> GetAvailablePortCountAsync();

    #endregion
}
