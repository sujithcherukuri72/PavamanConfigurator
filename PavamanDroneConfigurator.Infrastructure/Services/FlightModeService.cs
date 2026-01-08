using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service implementation for managing flight mode configuration.
/// Reads/writes FLTMODE parameters via MAVLink.
/// Monitors real-time flight mode from HEARTBEAT and RC_CHANNELS.
/// 
/// ArduPilot Parameters:
/// - FLTMODE_CH: Flight mode channel (5-12)
/// - FLTMODE1: Mode for PWM 0-1230
/// - FLTMODE2: Mode for PWM 1231-1360
/// - FLTMODE3: Mode for PWM 1361-1490
/// - FLTMODE4: Mode for PWM 1491-1620
/// - FLTMODE5: Mode for PWM 1621-1749
/// - FLTMODE6: Mode for PWM 1750+
/// - SIMPLE: Bitmask for simple mode
/// - SUPER_SIMPLE: Bitmask for super simple mode
/// </summary>
public class FlightModeService : IFlightModeService
{
    private readonly ILogger<FlightModeService> _logger;
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

    private FlightModeSettings? _cachedSettings;
    private FlightMode _currentFlightMode = FlightMode.Stabilize;
    private int _currentModeChannelPwm;
    private FlightModeChannel _modeChannel = FlightModeChannel.Channel5;

    // Parameter names
    private const string PARAM_FLTMODE_CH = "FLTMODE_CH";
    private const string PARAM_FLTMODE1 = "FLTMODE1";
    private const string PARAM_FLTMODE2 = "FLTMODE2";
    private const string PARAM_FLTMODE3 = "FLTMODE3";
    private const string PARAM_FLTMODE4 = "FLTMODE4";
    private const string PARAM_FLTMODE5 = "FLTMODE5";
    private const string PARAM_FLTMODE6 = "FLTMODE6";
    private const string PARAM_SIMPLE = "SIMPLE";
    private const string PARAM_SUPER_SIMPLE = "SUPER_SIMPLE";

    public event EventHandler<FlightModeSettings>? FlightModeSettingsChanged;
    public event EventHandler<FlightMode>? CurrentModeChanged;
    public event EventHandler<int>? ModeChannelPwmChanged;

    public FlightModeService(
        ILogger<FlightModeService> logger,
        IParameterService parameterService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _parameterService = parameterService;
        _connectionService = connectionService;

        // Subscribe to parameter changes
        _parameterService.ParameterUpdated += OnParameterUpdated;
        
        // Subscribe to real-time telemetry
        _connectionService.HeartbeatDataReceived += OnHeartbeatDataReceived;
        _connectionService.RcChannelsReceived += OnRcChannelsReceived;
    }

    private void OnParameterUpdated(object? sender, string paramName)
    {
        // Update cached settings when relevant parameters change
        if (paramName.StartsWith("FLTMODE") || paramName == PARAM_SIMPLE || paramName == PARAM_SUPER_SIMPLE)
        {
            _logger.LogDebug("Flight mode parameter changed: {Name}", paramName);
            
            // Update mode channel if it changed
            if (paramName == PARAM_FLTMODE_CH)
            {
                _ = UpdateModeChannelAsync();
            }
        }
    }

    private async Task UpdateModeChannelAsync()
    {
        var param = await _parameterService.GetParameterAsync(PARAM_FLTMODE_CH);
        if (param != null)
        {
            _modeChannel = (FlightModeChannel)(int)param.Value;
            _logger.LogDebug("Flight mode channel updated to: {Channel}", _modeChannel);
        }
    }

    private void OnHeartbeatDataReceived(object? sender, HeartbeatDataEventArgs e)
    {
        // Custom mode contains the flight mode for ArduPilot
        // For Copter: custom_mode = flight_mode number
        var newMode = (FlightMode)e.CustomMode;
        
        if (newMode != _currentFlightMode)
        {
            var oldMode = _currentFlightMode;
            _currentFlightMode = newMode;
            
            _logger.LogInformation("Flight mode changed: {Old} -> {New}", oldMode, newMode);
            CurrentModeChanged?.Invoke(this, newMode);
        }
    }

    private void OnRcChannelsReceived(object? sender, RcChannelsEventArgs e)
    {
        // Get PWM value from the configured mode channel
        int channelNumber = (int)_modeChannel;
        ushort pwm = e.GetChannel(channelNumber);
        
        if (pwm != _currentModeChannelPwm && pwm > 0)
        {
            _currentModeChannelPwm = pwm;
            _logger.LogDebug("Mode channel {Channel} PWM: {Pwm}", channelNumber, pwm);
            ModeChannelPwmChanged?.Invoke(this, pwm);
        }
    }

    public async Task<FlightModeSettings?> GetFlightModeSettingsAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot get flight mode settings - not connected");
            return null;
        }

        try
        {
            var settings = new FlightModeSettings();

            // Read flight mode channel
            var channelParam = await _parameterService.GetParameterAsync(PARAM_FLTMODE_CH);
            if (channelParam != null)
            {
                settings.ModeChannel = (FlightModeChannel)(int)channelParam.Value;
            }

            // Read all 6 flight modes
            var mode1 = await _parameterService.GetParameterAsync(PARAM_FLTMODE1);
            if (mode1 != null) settings.Mode1 = (FlightMode)(int)mode1.Value;

            var mode2 = await _parameterService.GetParameterAsync(PARAM_FLTMODE2);
            if (mode2 != null) settings.Mode2 = (FlightMode)(int)mode2.Value;

            var mode3 = await _parameterService.GetParameterAsync(PARAM_FLTMODE3);
            if (mode3 != null) settings.Mode3 = (FlightMode)(int)mode3.Value;

            var mode4 = await _parameterService.GetParameterAsync(PARAM_FLTMODE4);
            if (mode4 != null) settings.Mode4 = (FlightMode)(int)mode4.Value;

            var mode5 = await _parameterService.GetParameterAsync(PARAM_FLTMODE5);
            if (mode5 != null) settings.Mode5 = (FlightMode)(int)mode5.Value;

            var mode6 = await _parameterService.GetParameterAsync(PARAM_FLTMODE6);
            if (mode6 != null) settings.Mode6 = (FlightMode)(int)mode6.Value;

            // Read simple mode bitmasks
            var simpleMaskParam = await _parameterService.GetParameterAsync(PARAM_SIMPLE);
            var superSimpleMaskParam = await _parameterService.GetParameterAsync(PARAM_SUPER_SIMPLE);

            if (simpleMaskParam != null && superSimpleMaskParam != null)
            {
                int simple = (int)simpleMaskParam.Value;
                int superSimple = (int)superSimpleMaskParam.Value;

                settings.Simple1 = GetSimpleMode(simple, superSimple, 0);
                settings.Simple2 = GetSimpleMode(simple, superSimple, 1);
                settings.Simple3 = GetSimpleMode(simple, superSimple, 2);
                settings.Simple4 = GetSimpleMode(simple, superSimple, 3);
                settings.Simple5 = GetSimpleMode(simple, superSimple, 4);
                settings.Simple6 = GetSimpleMode(simple, superSimple, 5);
            }

            _cachedSettings = settings;
            FlightModeSettingsChanged?.Invoke(this, settings);

            _logger.LogInformation("Flight mode settings loaded successfully");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting flight mode settings");
            return null;
        }
    }

    private static SimpleMode GetSimpleMode(int simpleMask, int superSimpleMask, int bit)
    {
        bool isSimple = (simpleMask & (1 << bit)) != 0;
        bool isSuperSimple = (superSimpleMask & (1 << bit)) != 0;

        if (isSuperSimple) return SimpleMode.SuperSimple;
        if (isSimple) return SimpleMode.Simple;
        return SimpleMode.Off;
    }

    public async Task<bool> UpdateFlightModeSettingsAsync(FlightModeSettings settings)
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot update flight mode settings - not connected");
            return false;
        }

        try
        {
            _logger.LogInformation("Updating all flight mode settings");

            // Update flight mode channel
            await _parameterService.SetParameterAsync(PARAM_FLTMODE_CH, (float)settings.ModeChannel);

            // Update all 6 flight modes
            await _parameterService.SetParameterAsync(PARAM_FLTMODE1, (float)settings.Mode1);
            await _parameterService.SetParameterAsync(PARAM_FLTMODE2, (float)settings.Mode2);
            await _parameterService.SetParameterAsync(PARAM_FLTMODE3, (float)settings.Mode3);
            await _parameterService.SetParameterAsync(PARAM_FLTMODE4, (float)settings.Mode4);
            await _parameterService.SetParameterAsync(PARAM_FLTMODE5, (float)settings.Mode5);
            await _parameterService.SetParameterAsync(PARAM_FLTMODE6, (float)settings.Mode6);

            // Calculate and update simple mode bitmasks
            int simpleMask = 0;
            int superSimpleMask = 0;

            SetSimpleBit(ref simpleMask, ref superSimpleMask, 0, settings.Simple1);
            SetSimpleBit(ref simpleMask, ref superSimpleMask, 1, settings.Simple2);
            SetSimpleBit(ref simpleMask, ref superSimpleMask, 2, settings.Simple3);
            SetSimpleBit(ref simpleMask, ref superSimpleMask, 3, settings.Simple4);
            SetSimpleBit(ref simpleMask, ref superSimpleMask, 4, settings.Simple5);
            SetSimpleBit(ref simpleMask, ref superSimpleMask, 5, settings.Simple6);

            await _parameterService.SetParameterAsync(PARAM_SIMPLE, simpleMask);
            await _parameterService.SetParameterAsync(PARAM_SUPER_SIMPLE, superSimpleMask);

            _cachedSettings = settings;
            FlightModeSettingsChanged?.Invoke(this, settings);

            _logger.LogInformation("Flight mode settings updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating flight mode settings");
            return false;
        }
    }

    private static void SetSimpleBit(ref int simpleMask, ref int superSimpleMask, int bit, SimpleMode mode)
    {
        switch (mode)
        {
            case SimpleMode.Simple:
                simpleMask |= (1 << bit);
                break;
            case SimpleMode.SuperSimple:
                superSimpleMask |= (1 << bit);
                break;
        }
    }

    public async Task<bool> SetFlightModeAsync(int slot, FlightMode mode)
    {
        if (slot < 1 || slot > 6)
        {
            _logger.LogWarning("Invalid flight mode slot: {Slot}", slot);
            return false;
        }

        string paramName = $"FLTMODE{slot}";
        _logger.LogInformation("Setting {Param} to {Mode}", paramName, mode);

        return await _parameterService.SetParameterAsync(paramName, (float)mode);
    }

    public async Task<bool> SetFlightModeChannelAsync(FlightModeChannel channel)
    {
        _logger.LogInformation("Setting flight mode channel to {Channel}", channel);
        return await _parameterService.SetParameterAsync(PARAM_FLTMODE_CH, (float)channel);
    }

    public async Task<bool> SetSimpleModeAsync(int slot, SimpleMode simpleMode)
    {
        if (slot < 1 || slot > 6)
        {
            _logger.LogWarning("Invalid flight mode slot for simple mode: {Slot}", slot);
            return false;
        }

        try
        {
            var simpleMaskParam = await _parameterService.GetParameterAsync(PARAM_SIMPLE);
            var superSimpleMaskParam = await _parameterService.GetParameterAsync(PARAM_SUPER_SIMPLE);

            int simple = simpleMaskParam != null ? (int)simpleMaskParam.Value : 0;
            int superSimple = superSimpleMaskParam != null ? (int)superSimpleMaskParam.Value : 0;

            int bit = slot - 1;

            // Clear both bits first
            simple &= ~(1 << bit);
            superSimple &= ~(1 << bit);

            // Set the appropriate bit
            switch (simpleMode)
            {
                case SimpleMode.Simple:
                    simple |= (1 << bit);
                    break;
                case SimpleMode.SuperSimple:
                    superSimple |= (1 << bit);
                    break;
            }

            await _parameterService.SetParameterAsync(PARAM_SIMPLE, simple);
            await _parameterService.SetParameterAsync(PARAM_SUPER_SIMPLE, superSimple);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting simple mode for slot {Slot}", slot);
            return false;
        }
    }

    public Task<FlightMode?> GetCurrentFlightModeAsync()
    {
        // This would typically come from HEARTBEAT messages
        // For now, return the cached current mode if available
        return Task.FromResult(_cachedSettings?.CurrentMode);
    }

    public IEnumerable<FlightModeInfo> GetAvailableFlightModes()
    {
        // Return all flight modes that are commonly available
        var modes = new[]
        {
            FlightMode.Stabilize,
            FlightMode.Acro,
            FlightMode.AltHold,
            FlightMode.Auto,
            FlightMode.Guided,
            FlightMode.Loiter,
            FlightMode.RTL,
            FlightMode.Circle,
            FlightMode.Land,
            FlightMode.Drift,
            FlightMode.Sport,
            FlightMode.Flip,
            FlightMode.AutoTune,
            FlightMode.PosHold,
            FlightMode.Brake,
            FlightMode.Throw,
            FlightMode.SmartRTL,
            FlightMode.FlowHold,
            FlightMode.Follow,
            FlightMode.ZigZag
        };

        return modes.Select(FlightModeInfo.GetModeInfo);
    }

    public IEnumerable<FlightModeInfo> GetRecommendedFlightModes()
    {
        return GetAvailableFlightModes().Where(m => m.IsSafeForBeginners);
    }

    public IEnumerable<FlightModeInfo> GetGpsRequiredModes()
    {
        return GetAvailableFlightModes().Where(m => m.RequiresGps);
    }

    public async Task<bool> ApplyDefaultConfigurationAsync()
    {
        _logger.LogInformation("Applying default flight mode configuration");

        var defaults = new FlightModeSettings
        {
            ModeChannel = FlightModeChannel.Channel5,
            Mode1 = FlightMode.Stabilize,  // Safe manual mode
            Mode2 = FlightMode.AltHold,    // Altitude hold
            Mode3 = FlightMode.Loiter,     // GPS position hold
            Mode4 = FlightMode.PosHold,    // Position hold
            Mode5 = FlightMode.RTL,        // Return to launch
            Mode6 = FlightMode.Land,       // Emergency land
            Simple1 = SimpleMode.Off,
            Simple2 = SimpleMode.Off,
            Simple3 = SimpleMode.Off,
            Simple4 = SimpleMode.Off,
            Simple5 = SimpleMode.Off,
            Simple6 = SimpleMode.Off
        };

        return await UpdateFlightModeSettingsAsync(defaults);
    }

    public List<string> ValidateConfiguration(FlightModeSettings settings)
    {
        var warnings = new List<string>();

        // Check if at least one mode is a stable mode
        var stableModes = new[] { FlightMode.Stabilize, FlightMode.AltHold, FlightMode.Loiter, FlightMode.PosHold };
        var allModes = new[] { settings.Mode1, settings.Mode2, settings.Mode3, settings.Mode4, settings.Mode5, settings.Mode6 };

        if (!allModes.Any(m => stableModes.Contains(m)))
        {
            warnings.Add("No stable/safe mode configured. Recommend adding Stabilize or Loiter.");
        }

        // Check if RTL or Land is available
        if (!allModes.Contains(FlightMode.RTL) && !allModes.Contains(FlightMode.Land))
        {
            warnings.Add("No emergency return mode (RTL or Land) configured.");
        }

        // Check for duplicate modes
        var duplicates = allModes.GroupBy(m => m).Where(g => g.Count() > 1).Select(g => g.Key);
        foreach (var dupe in duplicates)
        {
            warnings.Add($"Mode '{dupe}' is assigned to multiple slots.");
        }

        // Check for advanced modes without GPS modes
        var gpsModes = allModes.Where(m => FlightModeInfo.GetModeInfo(m).RequiresGps);
        if (gpsModes.Any() && !allModes.Any(m => !FlightModeInfo.GetModeInfo(m).RequiresGps))
        {
            warnings.Add("All modes require GPS. Add a non-GPS mode for GPS failure situations.");
        }

        // Warn about advanced modes
        var advancedModes = new[] { FlightMode.Acro, FlightMode.Sport, FlightMode.Flip, FlightMode.AutoTune };
        var configuredAdvanced = allModes.Where(m => advancedModes.Contains(m));
        foreach (var mode in configuredAdvanced)
        {
            warnings.Add($"Advanced mode '{mode}' requires experience. Use with caution.");
        }

        return warnings;
    }
}
