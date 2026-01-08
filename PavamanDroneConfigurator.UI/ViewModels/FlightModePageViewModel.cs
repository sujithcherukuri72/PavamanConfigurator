using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.UI.ViewModels;

/// <summary>
/// ViewModel for Flight Modes configuration page.
/// Provides 6 flight mode slots with PWM ranges.
/// </summary>
public partial class FlightModePageViewModel : ViewModelBase
{
    private readonly ILogger<FlightModePageViewModel> _logger;
    private readonly IFlightModeService _flightModeService;
    private readonly IConnectionService _connectionService;

    #region Observable Properties

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private FlightModeChannel _selectedChannel = FlightModeChannel.Channel5;

    [ObservableProperty]
    private FlightModeOption? _selectedMode1;

    [ObservableProperty]
    private FlightModeOption? _selectedMode2;

    [ObservableProperty]
    private FlightModeOption? _selectedMode3;

    [ObservableProperty]
    private FlightModeOption? _selectedMode4;

    [ObservableProperty]
    private FlightModeOption? _selectedMode5;

    [ObservableProperty]
    private FlightModeOption? _selectedMode6;

    [ObservableProperty]
    private SimpleModeOption? _selectedSimple1;

    [ObservableProperty]
    private SimpleModeOption? _selectedSimple2;

    [ObservableProperty]
    private SimpleModeOption? _selectedSimple3;

    [ObservableProperty]
    private SimpleModeOption? _selectedSimple4;

    [ObservableProperty]
    private SimpleModeOption? _selectedSimple5;

    [ObservableProperty]
    private SimpleModeOption? _selectedSimple6;

    [ObservableProperty]
    private int _currentPwm;

    [ObservableProperty]
    private int _activeSlot = 1;

    [ObservableProperty]
    private string _currentModeDisplay = "Unknown";

    [ObservableProperty]
    private bool _hasValidationWarnings;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private string _selectedModeDescription = string.Empty;

    [ObservableProperty]
    private bool _isSlot1Active;

    [ObservableProperty]
    private bool _isSlot2Active;

    [ObservableProperty]
    private bool _isSlot3Active;

    [ObservableProperty]
    private bool _isSlot4Active;

    [ObservableProperty]
    private bool _isSlot5Active;

    [ObservableProperty]
    private bool _isSlot6Active;

    #endregion

    #region Collections

    public ObservableCollection<FlightModeChannelOption> ChannelOptions { get; } = new();
    public ObservableCollection<FlightModeOption> FlightModeOptions { get; } = new();
    public ObservableCollection<SimpleModeOption> SimpleModeOptions { get; } = new();
    public ObservableCollection<string> ValidationWarnings { get; } = new();

    #endregion

    #region PWM Range Labels

    public string PwmRange1 => FlightModeSettings.GetPwmRange(1);
    public string PwmRange2 => FlightModeSettings.GetPwmRange(2);
    public string PwmRange3 => FlightModeSettings.GetPwmRange(3);
    public string PwmRange4 => FlightModeSettings.GetPwmRange(4);
    public string PwmRange5 => FlightModeSettings.GetPwmRange(5);
    public string PwmRange6 => FlightModeSettings.GetPwmRange(6);

    #endregion

    public FlightModePageViewModel(
        ILogger<FlightModePageViewModel> logger,
        IFlightModeService flightModeService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _flightModeService = flightModeService;
        _connectionService = connectionService;

        // Subscribe to events
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _flightModeService.FlightModeSettingsChanged += OnFlightModeSettingsChanged;
        _flightModeService.ModeChannelPwmChanged += OnModeChannelPwmChanged;

        // Initialize options
        InitializeOptions();

        // Update connection state
        IsConnected = _connectionService.IsConnected;
    }

    private void InitializeOptions()
    {
        // Channel options
        ChannelOptions.Add(new FlightModeChannelOption { Channel = FlightModeChannel.Channel5, Label = "Channel 5" });
        ChannelOptions.Add(new FlightModeChannelOption { Channel = FlightModeChannel.Channel6, Label = "Channel 6" });
        ChannelOptions.Add(new FlightModeChannelOption { Channel = FlightModeChannel.Channel7, Label = "Channel 7" });
        ChannelOptions.Add(new FlightModeChannelOption { Channel = FlightModeChannel.Channel8, Label = "Channel 8" });
        ChannelOptions.Add(new FlightModeChannelOption { Channel = FlightModeChannel.Channel9, Label = "Channel 9" });
        ChannelOptions.Add(new FlightModeChannelOption { Channel = FlightModeChannel.Channel10, Label = "Channel 10" });
        ChannelOptions.Add(new FlightModeChannelOption { Channel = FlightModeChannel.Channel11, Label = "Channel 11" });
        ChannelOptions.Add(new FlightModeChannelOption { Channel = FlightModeChannel.Channel12, Label = "Channel 12" });

        // Flight mode options
        var modes = _flightModeService.GetAvailableFlightModes();
        foreach (var mode in modes)
        {
            FlightModeOptions.Add(new FlightModeOption
            {
                Mode = mode.Mode,
                Label = mode.Name,
                Description = mode.Description,
                RequiresGps = mode.RequiresGps,
                IsAutonomous = mode.IsAutonomous,
                IsSafeForBeginners = mode.IsSafeForBeginners
            });
        }

        // Simple mode options
        SimpleModeOptions.Add(new SimpleModeOption { Mode = SimpleMode.Off, Label = "Off" });
        SimpleModeOptions.Add(new SimpleModeOption { Mode = SimpleMode.Simple, Label = "Simple" });
        SimpleModeOptions.Add(new SimpleModeOption { Mode = SimpleMode.SuperSimple, Label = "Super Simple" });

        // Set defaults
        var defaultMode = FlightModeOptions.FirstOrDefault(m => m.Mode == FlightMode.Stabilize);
        SelectedMode1 = defaultMode;
        SelectedMode2 = defaultMode;
        SelectedMode3 = defaultMode;
        SelectedMode4 = defaultMode;
        SelectedMode5 = defaultMode;
        SelectedMode6 = defaultMode;

        var simpleOff = SimpleModeOptions.FirstOrDefault(s => s.Mode == SimpleMode.Off);
        SelectedSimple1 = simpleOff;
        SelectedSimple2 = simpleOff;
        SelectedSimple3 = simpleOff;
        SelectedSimple4 = simpleOff;
        SelectedSimple5 = simpleOff;
        SelectedSimple6 = simpleOff;
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        if (connected)
        {
            _ = RefreshAsync();
        }
    }

    private void OnFlightModeSettingsChanged(object? sender, FlightModeSettings settings)
    {
        UpdateFromSettings(settings);
    }

    private void OnModeChannelPwmChanged(object? sender, int pwm)
    {
        CurrentPwm = pwm;
        ActiveSlot = FlightModeSettings.GetActiveModeSlot(pwm);
        UpdateActiveSlotIndicators();
        UpdateCurrentModeDisplay();
    }

    private void UpdateActiveSlotIndicators()
    {
        IsSlot1Active = ActiveSlot == 1;
        IsSlot2Active = ActiveSlot == 2;
        IsSlot3Active = ActiveSlot == 3;
        IsSlot4Active = ActiveSlot == 4;
        IsSlot5Active = ActiveSlot == 5;
        IsSlot6Active = ActiveSlot == 6;
    }

    private void UpdateFromSettings(FlightModeSettings settings)
    {
        SelectedChannel = settings.ModeChannel;

        SelectedMode1 = FlightModeOptions.FirstOrDefault(m => m.Mode == settings.Mode1);
        SelectedMode2 = FlightModeOptions.FirstOrDefault(m => m.Mode == settings.Mode2);
        SelectedMode3 = FlightModeOptions.FirstOrDefault(m => m.Mode == settings.Mode3);
        SelectedMode4 = FlightModeOptions.FirstOrDefault(m => m.Mode == settings.Mode4);
        SelectedMode5 = FlightModeOptions.FirstOrDefault(m => m.Mode == settings.Mode5);
        SelectedMode6 = FlightModeOptions.FirstOrDefault(m => m.Mode == settings.Mode6);

        SelectedSimple1 = SimpleModeOptions.FirstOrDefault(s => s.Mode == settings.Simple1);
        SelectedSimple2 = SimpleModeOptions.FirstOrDefault(s => s.Mode == settings.Simple2);
        SelectedSimple3 = SimpleModeOptions.FirstOrDefault(s => s.Mode == settings.Simple3);
        SelectedSimple4 = SimpleModeOptions.FirstOrDefault(s => s.Mode == settings.Simple4);
        SelectedSimple5 = SimpleModeOptions.FirstOrDefault(s => s.Mode == settings.Simple5);
        SelectedSimple6 = SimpleModeOptions.FirstOrDefault(s => s.Mode == settings.Simple6);

        ValidateCurrentConfiguration();
        UpdateCurrentModeDisplay();
    }

    private void UpdateCurrentModeDisplay()
    {
        var currentMode = ActiveSlot switch
        {
            1 => SelectedMode1,
            2 => SelectedMode2,
            3 => SelectedMode3,
            4 => SelectedMode4,
            5 => SelectedMode5,
            6 => SelectedMode6,
            _ => null
        };

        CurrentModeDisplay = currentMode?.Label ?? "Unknown";
    }

    private void ValidateCurrentConfiguration()
    {
        var settings = BuildSettingsFromUI();
        var warnings = _flightModeService.ValidateConfiguration(settings);

        ValidationWarnings.Clear();
        foreach (var warning in warnings)
        {
            ValidationWarnings.Add(warning);
        }

        HasValidationWarnings = warnings.Count > 0;
        ValidationMessage = warnings.Count > 0
            ? string.Join("\n", warnings.Take(3))
            : "Configuration looks good!";
    }

    private FlightModeSettings BuildSettingsFromUI()
    {
        return new FlightModeSettings
        {
            ModeChannel = SelectedChannel,
            Mode1 = SelectedMode1?.Mode ?? FlightMode.Stabilize,
            Mode2 = SelectedMode2?.Mode ?? FlightMode.Stabilize,
            Mode3 = SelectedMode3?.Mode ?? FlightMode.Stabilize,
            Mode4 = SelectedMode4?.Mode ?? FlightMode.Stabilize,
            Mode5 = SelectedMode5?.Mode ?? FlightMode.Stabilize,
            Mode6 = SelectedMode6?.Mode ?? FlightMode.Stabilize,
            Simple1 = SelectedSimple1?.Mode ?? SimpleMode.Off,
            Simple2 = SelectedSimple2?.Mode ?? SimpleMode.Off,
            Simple3 = SelectedSimple3?.Mode ?? SimpleMode.Off,
            Simple4 = SelectedSimple4?.Mode ?? SimpleMode.Off,
            Simple5 = SelectedSimple5?.Mode ?? SimpleMode.Off,
            Simple6 = SelectedSimple6?.Mode ?? SimpleMode.Off
        };
    }

    partial void OnSelectedMode1Changed(FlightModeOption? value)
    {
        if (value != null)
        {
            SelectedModeDescription = value.Description;
        }
        ValidateCurrentConfiguration();
    }

    partial void OnSelectedMode2Changed(FlightModeOption? value) => ValidateCurrentConfiguration();
    partial void OnSelectedMode3Changed(FlightModeOption? value) => ValidateCurrentConfiguration();
    partial void OnSelectedMode4Changed(FlightModeOption? value) => ValidateCurrentConfiguration();
    partial void OnSelectedMode5Changed(FlightModeOption? value) => ValidateCurrentConfiguration();
    partial void OnSelectedMode6Changed(FlightModeOption? value) => ValidateCurrentConfiguration();

    #region Commands

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Loading flight modes...";

            var settings = await _flightModeService.GetFlightModeSettingsAsync();
            if (settings != null)
            {
                UpdateFromSettings(settings);
                StatusMessage = "Flight modes loaded successfully";
            }
            else
            {
                StatusMessage = "Failed to load flight modes";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing flight modes");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task UpdateAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Updating flight modes...";

            var settings = BuildSettingsFromUI();
            var success = await _flightModeService.UpdateFlightModeSettingsAsync(settings);

            if (success)
            {
                StatusMessage = "Flight modes updated successfully";
            }
            else
            {
                StatusMessage = "Failed to update flight modes";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating flight modes");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ApplyDefaultsAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Applying default configuration...";

            // Set recommended defaults in UI
            SelectedMode1 = FlightModeOptions.FirstOrDefault(m => m.Mode == FlightMode.Stabilize);
            SelectedMode2 = FlightModeOptions.FirstOrDefault(m => m.Mode == FlightMode.AltHold);
            SelectedMode3 = FlightModeOptions.FirstOrDefault(m => m.Mode == FlightMode.Loiter);
            SelectedMode4 = FlightModeOptions.FirstOrDefault(m => m.Mode == FlightMode.PosHold);
            SelectedMode5 = FlightModeOptions.FirstOrDefault(m => m.Mode == FlightMode.RTL);
            SelectedMode6 = FlightModeOptions.FirstOrDefault(m => m.Mode == FlightMode.Land);

            var simpleOff = SimpleModeOptions.FirstOrDefault(s => s.Mode == SimpleMode.Off);
            SelectedSimple1 = simpleOff;
            SelectedSimple2 = simpleOff;
            SelectedSimple3 = simpleOff;
            SelectedSimple4 = simpleOff;
            SelectedSimple5 = simpleOff;
            SelectedSimple6 = simpleOff;

            SelectedChannel = FlightModeChannel.Channel5;

            if (IsConnected)
            {
                var success = await _flightModeService.ApplyDefaultConfigurationAsync();
                StatusMessage = success ? "Default configuration applied" : "Failed to apply defaults";
            }
            else
            {
                StatusMessage = "Defaults set (connect to upload)";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying defaults");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
            _flightModeService.FlightModeSettingsChanged -= OnFlightModeSettingsChanged;
            _flightModeService.ModeChannelPwmChanged -= OnModeChannelPwmChanged;
        }
        base.Dispose(disposing);
    }
}

#region Option Classes

public class FlightModeChannelOption
{
    public FlightModeChannel Channel { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class FlightModeOption
{
    public FlightMode Mode { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresGps { get; set; }
    public bool IsAutonomous { get; set; }
    public bool IsSafeForBeginners { get; set; }
}

public class SimpleModeOption
{
    public SimpleMode Mode { get; set; }
    public string Label { get; set; } = string.Empty;
}

#endregion
