using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.UI.ViewModels;

/// <summary>
/// ViewModel for PID Tuning page with Basic, Advanced, and AutoTune tabs.
/// </summary>
public partial class PidTuningPageViewModel : ViewModelBase
{
    private readonly ILogger<PidTuningPageViewModel> _logger;
    private readonly IPidTuningService _pidTuningService;
    private readonly IConnectionService _connectionService;

    #region Tab Selection

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    private bool _isBasicTuningSelected = true;

    [ObservableProperty]
    private bool _isAdvancedTuningSelected;

    [ObservableProperty]
    private bool _isAutoTuningSelected;

    #endregion

    #region Status Properties

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _hasValidationWarnings;

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    #endregion

    #region Basic Tuning Properties

    [ObservableProperty]
    private float _rcFeelRollPitch = 0.15f;

    [ObservableProperty]
    private float _rollPitchSensitivity = 0.135f;

    [ObservableProperty]
    private float _climbSensitivity = 1.0f;

    [ObservableProperty]
    private float _spinWhileArmed = 0.1f;

    [ObservableProperty]
    private float _minThrottle = 0.15f;

    #endregion

    #region Advanced Tuning - Axis Selection

    [ObservableProperty]
    private bool _isRollSelected = true;

    [ObservableProperty]
    private bool _isPitchSelected;

    [ObservableProperty]
    private bool _isYawSelected;

    [ObservableProperty]
    private TuningAxis _selectedAxis = TuningAxis.Roll;

    #endregion

    #region Advanced Tuning - Current Axis PID Values

    [ObservableProperty]
    private float _angleP = 4.5f;

    [ObservableProperty]
    private float _rateP = 0.135f;

    [ObservableProperty]
    private float _rateI = 0.135f;

    [ObservableProperty]
    private float _rateD = 0.0036f;

    [ObservableProperty]
    private float _rateFF = 0.0f;

    [ObservableProperty]
    private float _rateFilter = 20.0f;

    [ObservableProperty]
    private float _rateIMax = 0.5f;

    #endregion

    #region AutoTune Properties

    [ObservableProperty]
    private bool _autoTuneRoll = true;

    [ObservableProperty]
    private bool _autoTunePitch = true;

    [ObservableProperty]
    private bool _autoTuneYaw = true;

    [ObservableProperty]
    private AutoTuneChannelOption? _selectedAutoTuneChannel;

    [ObservableProperty]
    private float _autoTuneAggressiveness = 0.1f;

    #endregion

    #region In-Flight Tuning Properties

    [ObservableProperty]
    private InFlightTuningOptionItem? _selectedTuneOption;

    [ObservableProperty]
    private float _tuneMin;

    [ObservableProperty]
    private float _tuneMax;

    #endregion

    #region Summary Panel Properties

    [ObservableProperty]
    private string _summaryTitle = "RC Feel Roll/Pitch Parameters";

    [ObservableProperty]
    private ObservableCollection<SummaryItem> _summaryItems = new();

    #endregion

    #region Collections

    public ObservableCollection<AutoTuneChannelOption> AutoTuneChannelOptions { get; } = new();
    public ObservableCollection<InFlightTuningOptionItem> InFlightTuningOptions { get; } = new();

    #endregion

    #region Internal State

    private AxisPidSettings _rollPidSettings = new() { Axis = TuningAxis.Roll };
    private AxisPidSettings _pitchPidSettings = new() { Axis = TuningAxis.Pitch };
    private AxisPidSettings _yawPidSettings = new() { Axis = TuningAxis.Yaw };

    #endregion

    public PidTuningPageViewModel(
        ILogger<PidTuningPageViewModel> logger,
        IPidTuningService pidTuningService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _pidTuningService = pidTuningService;
        _connectionService = connectionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _pidTuningService.ParameterUpdated += OnParameterUpdated;

        InitializeOptions();
        IsConnected = _connectionService.IsConnected;
        UpdateSummary();
    }

    private void InitializeOptions()
    {
        // AutoTune channel options
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.None, Label = "None" });
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.Channel5, Label = "Channel 5" });
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.Channel6, Label = "Channel 6" });
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.Channel7, Label = "Channel 7" });
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.Channel8, Label = "Channel 8" });
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.Channel9, Label = "Channel 9" });
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.Channel10, Label = "Channel 10" });
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.Channel11, Label = "Channel 11" });
        AutoTuneChannelOptions.Add(new AutoTuneChannelOption { Channel = AutoTuneChannel.Channel12, Label = "Channel 12" });
        SelectedAutoTuneChannel = AutoTuneChannelOptions.First();

        // In-flight tuning options
        var tuningOptions = _pidTuningService.GetInFlightTuningOptions();
        foreach (var (option, label, description) in tuningOptions)
        {
            InFlightTuningOptions.Add(new InFlightTuningOptionItem
            {
                Option = option,
                Label = label,
                Description = description
            });
        }
        SelectedTuneOption = InFlightTuningOptions.First();
    }

    #region Event Handlers

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
            if (connected)
            {
                _ = RefreshAsync();
            }
        });
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        // Could trigger UI update for specific parameter changes
        _logger.LogDebug("PID parameter updated: {Parameter}", parameterName);
    }

    #endregion

    #region Property Change Handlers

    partial void OnSelectedTabIndexChanged(int value)
    {
        IsBasicTuningSelected = value == 0;
        IsAdvancedTuningSelected = value == 1;
        IsAutoTuningSelected = value == 2;
        UpdateSummary();
    }

    partial void OnIsRollSelectedChanged(bool value)
    {
        if (value)
        {
            SelectedAxis = TuningAxis.Roll;
            IsPitchSelected = false;
            IsYawSelected = false;
            LoadAxisSettings(_rollPidSettings);
            UpdateSummary();
        }
    }

    partial void OnIsPitchSelectedChanged(bool value)
    {
        if (value)
        {
            SelectedAxis = TuningAxis.Pitch;
            IsRollSelected = false;
            IsYawSelected = false;
            LoadAxisSettings(_pitchPidSettings);
            UpdateSummary();
        }
    }

    partial void OnIsYawSelectedChanged(bool value)
    {
        if (value)
        {
            SelectedAxis = TuningAxis.Yaw;
            IsRollSelected = false;
            IsPitchSelected = false;
            LoadAxisSettings(_yawPidSettings);
            UpdateSummary();
        }
    }

    partial void OnRcFeelRollPitchChanged(float value) => UpdateSummary();
    partial void OnRollPitchSensitivityChanged(float value) => UpdateSummary();
    partial void OnClimbSensitivityChanged(float value) => UpdateSummary();
    partial void OnSpinWhileArmedChanged(float value) => UpdateSummary();
    partial void OnAnglePChanged(float value) => UpdateAxisAndSummary();
    partial void OnRatePChanged(float value) => UpdateAxisAndSummary();
    partial void OnRateIChanged(float value) => UpdateAxisAndSummary();
    partial void OnRateDChanged(float value) => UpdateAxisAndSummary();

    private void UpdateAxisAndSummary()
    {
        // Store changes to the current axis settings
        var current = GetCurrentAxisSettings();
        current.AngleP = AngleP;
        current.RateP = RateP;
        current.RateI = RateI;
        current.RateD = RateD;
        current.RateFF = RateFF;
        current.RateFilter = RateFilter;
        current.RateIMax = RateIMax;
        UpdateSummary();
    }

    #endregion

    #region Axis Settings Management

    private AxisPidSettings GetCurrentAxisSettings()
    {
        return SelectedAxis switch
        {
            TuningAxis.Roll => _rollPidSettings,
            TuningAxis.Pitch => _pitchPidSettings,
            TuningAxis.Yaw => _yawPidSettings,
            _ => _rollPidSettings
        };
    }

    private void LoadAxisSettings(AxisPidSettings settings)
    {
        AngleP = settings.AngleP;
        RateP = settings.RateP;
        RateI = settings.RateI;
        RateD = settings.RateD;
        RateFF = settings.RateFF;
        RateFilter = settings.RateFilter;
        RateIMax = settings.RateIMax;
    }

    #endregion

    #region Summary Panel

    private void UpdateSummary()
    {
        SummaryItems.Clear();

        if (IsBasicTuningSelected)
        {
            SummaryTitle = "RC Feel Roll/Pitch Parameters";
            SummaryItems.Add(new SummaryItem { Parameter = "ATC_INPUT_TC", Value = RcFeelRollPitch.ToString("F4") });
        }
        else if (IsAdvancedTuningSelected)
        {
            var axisName = SelectedAxis.ToString();
            var prefix = SelectedAxis switch
            {
                TuningAxis.Roll => "RLL",
                TuningAxis.Pitch => "PIT",
                TuningAxis.Yaw => "YAW",
                _ => "RLL"
            };

            SummaryTitle = $"{axisName} Axis Values";
            SummaryItems.Add(new SummaryItem { Parameter = $"ATC_ANG_{prefix}_P", Value = AngleP.ToString("F4") });
            SummaryItems.Add(new SummaryItem { Parameter = $"ATC_RAT_{prefix}_P", Value = RateP.ToString("F4") });
            SummaryItems.Add(new SummaryItem { Parameter = $"ATC_RAT_{prefix}_I", Value = RateI.ToString("F4") });
            SummaryItems.Add(new SummaryItem { Parameter = $"ATC_RAT_{prefix}_D", Value = RateD.ToString("F4") });
        }
        else if (IsAutoTuningSelected)
        {
            SummaryTitle = "AutoTune Configuration";
            var axes = "";
            if (AutoTuneRoll) axes += "Roll ";
            if (AutoTunePitch) axes += "Pitch ";
            if (AutoTuneYaw) axes += "Yaw";
            SummaryItems.Add(new SummaryItem { Parameter = "Axes", Value = axes.Trim() });
            SummaryItems.Add(new SummaryItem { Parameter = "AUTOTUNE_AGGR", Value = AutoTuneAggressiveness.ToString("F2") });
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void SelectBasicTuning()
    {
        SelectedTabIndex = 0;
    }

    [RelayCommand]
    private void SelectAdvancedTuning()
    {
        SelectedTabIndex = 1;
    }

    [RelayCommand]
    private void SelectAutoTuning()
    {
        SelectedTabIndex = 2;
    }

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
            StatusMessage = "Loading PID settings...";

            var config = await _pidTuningService.GetFullConfigurationAsync();
            if (config != null)
            {
                // Basic tuning
                RcFeelRollPitch = config.BasicTuning.RcFeelRollPitch;
                RollPitchSensitivity = config.BasicTuning.RollPitchSensitivity;
                ClimbSensitivity = config.BasicTuning.ClimbSensitivity;
                SpinWhileArmed = config.BasicTuning.SpinWhileArmed;
                MinThrottle = config.BasicTuning.MinThrottle;

                // Axis settings
                _rollPidSettings = config.RollPid;
                _pitchPidSettings = config.PitchPid;
                _yawPidSettings = config.YawPid;

                // Load current axis
                LoadAxisSettings(GetCurrentAxisSettings());

                // AutoTune
                AutoTuneRoll = config.AutoTune.AxesToTune.HasFlag(AutoTuneAxes.Roll);
                AutoTunePitch = config.AutoTune.AxesToTune.HasFlag(AutoTuneAxes.Pitch);
                AutoTuneYaw = config.AutoTune.AxesToTune.HasFlag(AutoTuneAxes.Yaw);
                SelectedAutoTuneChannel = AutoTuneChannelOptions.FirstOrDefault(c => c.Channel == config.AutoTune.AutoTuneSwitch)
                    ?? AutoTuneChannelOptions.First();
                AutoTuneAggressiveness = config.AutoTune.Aggressiveness;

                // In-flight tuning
                SelectedTuneOption = InFlightTuningOptions.FirstOrDefault(o => o.Option == config.InFlightTuning.TuneOption)
                    ?? InFlightTuningOptions.First();
                TuneMin = config.InFlightTuning.TuneMin;
                TuneMax = config.InFlightTuning.TuneMax;

                // Validate
                var warnings = _pidTuningService.ValidateConfiguration(config);
                HasValidationWarnings = warnings.Count > 0;
                ValidationMessage = warnings.Count > 0
                    ? string.Join("\n", warnings.Take(3))
                    : "Configuration looks good!";

                StatusMessage = "PID settings loaded successfully";
                UpdateSummary();
            }
            else
            {
                StatusMessage = "Failed to load PID settings";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing PID settings");
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
            StatusMessage = "Updating PID settings...";

            var config = BuildConfigurationFromUI();
            var success = await _pidTuningService.ApplyFullConfigurationAsync(config);

            if (success)
            {
                StatusMessage = "PID settings updated successfully";

                var warnings = _pidTuningService.ValidateConfiguration(config);
                HasValidationWarnings = warnings.Count > 0;
                ValidationMessage = warnings.Count > 0
                    ? string.Join("\n", warnings.Take(3))
                    : "Configuration looks good!";
            }
            else
            {
                StatusMessage = "Failed to update PID settings";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating PID settings");
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
            StatusMessage = "Applying default PID configuration...";

            // Reset UI to defaults
            RcFeelRollPitch = 0.15f;
            RollPitchSensitivity = 0.135f;
            ClimbSensitivity = 1.0f;
            SpinWhileArmed = 0.1f;
            MinThrottle = 0.15f;

            _rollPidSettings = new AxisPidSettings { Axis = TuningAxis.Roll, AngleP = 4.5f, RateP = 0.135f, RateI = 0.135f, RateD = 0.0036f };
            _pitchPidSettings = new AxisPidSettings { Axis = TuningAxis.Pitch, AngleP = 4.5f, RateP = 0.135f, RateI = 0.135f, RateD = 0.0036f };
            _yawPidSettings = new AxisPidSettings { Axis = TuningAxis.Yaw, AngleP = 4.5f, RateP = 0.18f, RateI = 0.018f, RateD = 0.0f };

            LoadAxisSettings(GetCurrentAxisSettings());

            AutoTuneRoll = true;
            AutoTunePitch = true;
            AutoTuneYaw = true;
            SelectedAutoTuneChannel = AutoTuneChannelOptions.First();
            AutoTuneAggressiveness = 0.1f;

            SelectedTuneOption = InFlightTuningOptions.First();
            TuneMin = 0;
            TuneMax = 0;

            if (IsConnected)
            {
                var success = await _pidTuningService.ApplyDefaultConfigurationAsync();
                StatusMessage = success ? "Default PID configuration applied" : "Failed to apply defaults";
            }
            else
            {
                StatusMessage = "Defaults set (connect to upload)";
            }

            UpdateSummary();
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

    #region Helper Methods

    private PidTuningConfiguration BuildConfigurationFromUI()
    {
        // Update current axis settings from UI
        var current = GetCurrentAxisSettings();
        current.AngleP = AngleP;
        current.RateP = RateP;
        current.RateI = RateI;
        current.RateD = RateD;
        current.RateFF = RateFF;
        current.RateFilter = RateFilter;
        current.RateIMax = RateIMax;

        var autoTuneAxes = AutoTuneAxes.None;
        if (AutoTuneRoll) autoTuneAxes |= AutoTuneAxes.Roll;
        if (AutoTunePitch) autoTuneAxes |= AutoTuneAxes.Pitch;
        if (AutoTuneYaw) autoTuneAxes |= AutoTuneAxes.Yaw;

        return new PidTuningConfiguration
        {
            BasicTuning = new BasicTuningSettings
            {
                RcFeelRollPitch = RcFeelRollPitch,
                RollPitchSensitivity = RollPitchSensitivity,
                ClimbSensitivity = ClimbSensitivity,
                SpinWhileArmed = SpinWhileArmed,
                MinThrottle = MinThrottle
            },
            RollPid = _rollPidSettings,
            PitchPid = _pitchPidSettings,
            YawPid = _yawPidSettings,
            AutoTune = new AutoTuneSettings
            {
                AxesToTune = autoTuneAxes,
                AutoTuneSwitch = SelectedAutoTuneChannel?.Channel ?? AutoTuneChannel.None,
                Aggressiveness = AutoTuneAggressiveness
            },
            InFlightTuning = new InFlightTuningSettings
            {
                TuneOption = SelectedTuneOption?.Option ?? InFlightTuningOption.None,
                TuneMin = TuneMin,
                TuneMax = TuneMax
            }
        };
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
            _pidTuningService.ParameterUpdated -= OnParameterUpdated;
        }
        base.Dispose(disposing);
    }
}

#region Option Classes

public class AutoTuneChannelOption
{
    public AutoTuneChannel Channel { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class InFlightTuningOptionItem
{
    public InFlightTuningOption Option { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SummaryItem
{
    public string Parameter { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

#endregion
