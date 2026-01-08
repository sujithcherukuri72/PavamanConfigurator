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
/// ViewModel for RC Calibration page.
/// Manages RC channel calibration, monitoring, and attitude channel settings.
/// </summary>
public partial class RcCalibrationPageViewModel : ViewModelBase
{
    private readonly ILogger<RcCalibrationPageViewModel> _logger;
    private readonly IRcCalibrationService _rcCalibrationService;
    private readonly IConnectionService _connectionService;

    #region Status Properties

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isCalibrating;

    [ObservableProperty]
    private bool _calibrationRequired = true;

    [ObservableProperty]
    private string _calibrationStatusText = "Radio calibration required";

    #endregion

    #region Main Attitude Channels (Roll, Pitch, Yaw, Throttle)

    [ObservableProperty]
    private int _rollValue;

    [ObservableProperty]
    private int _pitchValue;

    [ObservableProperty]
    private int _yawValue;

    [ObservableProperty]
    private int _throttleValue;

    #endregion

    #region All 16 Channel Values

    [ObservableProperty]
    private int _channel1Value;

    [ObservableProperty]
    private int _channel2Value;

    [ObservableProperty]
    private int _channel3Value;

    [ObservableProperty]
    private int _channel4Value;

    [ObservableProperty]
    private int _channel5Value;

    [ObservableProperty]
    private int _channel6Value;

    [ObservableProperty]
    private int _channel7Value;

    [ObservableProperty]
    private int _channel8Value;

    [ObservableProperty]
    private int _channel9Value;

    [ObservableProperty]
    private int _channel10Value;

    [ObservableProperty]
    private int _channel11Value;

    [ObservableProperty]
    private int _channel12Value;

    [ObservableProperty]
    private int _channel13Value;

    [ObservableProperty]
    private int _channel14Value;

    [ObservableProperty]
    private int _channel15Value;

    [ObservableProperty]
    private int _channel16Value;

    #endregion

    #region Attitude Channel Mapping

    [ObservableProperty]
    private RcChannelOption? _selectedThrottleChannel;

    [ObservableProperty]
    private RcChannelOption? _selectedRollChannel;

    [ObservableProperty]
    private RcChannelOption? _selectedPitchChannel;

    [ObservableProperty]
    private RcChannelOption? _selectedYawChannel;

    #endregion

    #region Collections

    public ObservableCollection<ChannelMappingOption> ChannelOptions { get; } = new();

    #endregion

    #region Internal State

    private RcCalibrationConfiguration? _currentConfiguration;
    private AttitudeChannelMapping? _currentMapping;

    #endregion

    public RcCalibrationPageViewModel(
        ILogger<RcCalibrationPageViewModel> logger,
        IRcCalibrationService rcCalibrationService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _rcCalibrationService = rcCalibrationService;
        _connectionService = connectionService;

        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _rcCalibrationService.RcChannelsUpdated += OnRcChannelsUpdated;
        _rcCalibrationService.CalibrationStateChanged += OnCalibrationStateChanged;
        _rcCalibrationService.CalibrationCompleted += OnCalibrationCompleted;

        InitializeChannelOptions();
        IsConnected = _connectionService.IsConnected;
    }

    private void InitializeChannelOptions()
    {
        // Initialize channel mapping options (1-16)
        for (int i = 1; i <= 16; i++)
        {
            ChannelOptions.Add(new ChannelMappingOption
            {
                Channel = (RcChannel)i,
                Label = $"Channel {i}"
            });
        }

        // Set defaults
        SelectedThrottleChannel = ChannelOptions.FirstOrDefault(c => c.Channel == RcChannel.Channel3)?.Option;
        SelectedRollChannel = ChannelOptions.FirstOrDefault(c => c.Channel == RcChannel.Channel1)?.Option;
        SelectedPitchChannel = ChannelOptions.FirstOrDefault(c => c.Channel == RcChannel.Channel2)?.Option;
        SelectedYawChannel = ChannelOptions.FirstOrDefault(c => c.Channel == RcChannel.Channel4)?.Option;
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
            else
            {
                ResetChannelValues();
            }
        });
    }

    private void OnRcChannelsUpdated(object? sender, RcChannelsUpdateEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateChannelValues(e);
        });
    }

    private void OnCalibrationStateChanged(object? sender, RcCalibrationProgress e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsCalibrating = e.IsCalibrating;
            CalibrationStatusText = e.StatusMessage;
            StatusMessage = e.Instructions;
        });
    }

    private void OnCalibrationCompleted(object? sender, bool success)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsCalibrating = false;
            CalibrationRequired = !success;
            CalibrationStatusText = success ? "Calibration completed" : "Calibration failed";
            StatusMessage = success ? "RC calibration completed successfully" : "RC calibration failed";
        });
    }

    private void UpdateChannelValues(RcChannelsUpdateEventArgs e)
    {
        // Update individual channel values
        Channel1Value = e.GetChannel(1)?.PwmValue ?? 0;
        Channel2Value = e.GetChannel(2)?.PwmValue ?? 0;
        Channel3Value = e.GetChannel(3)?.PwmValue ?? 0;
        Channel4Value = e.GetChannel(4)?.PwmValue ?? 0;
        Channel5Value = e.GetChannel(5)?.PwmValue ?? 0;
        Channel6Value = e.GetChannel(6)?.PwmValue ?? 0;
        Channel7Value = e.GetChannel(7)?.PwmValue ?? 0;
        Channel8Value = e.GetChannel(8)?.PwmValue ?? 0;
        Channel9Value = e.GetChannel(9)?.PwmValue ?? 0;
        Channel10Value = e.GetChannel(10)?.PwmValue ?? 0;
        Channel11Value = e.GetChannel(11)?.PwmValue ?? 0;
        Channel12Value = e.GetChannel(12)?.PwmValue ?? 0;
        Channel13Value = e.GetChannel(13)?.PwmValue ?? 0;
        Channel14Value = e.GetChannel(14)?.PwmValue ?? 0;
        Channel15Value = e.GetChannel(15)?.PwmValue ?? 0;
        Channel16Value = e.GetChannel(16)?.PwmValue ?? 0;

        // Update main attitude channels based on current mapping
        if (_currentMapping != null)
        {
            RollValue = e.GetChannel(_currentMapping.RollChannel)?.PwmValue ?? 0;
            PitchValue = e.GetChannel(_currentMapping.PitchChannel)?.PwmValue ?? 0;
            YawValue = e.GetChannel(_currentMapping.YawChannel)?.PwmValue ?? 0;
            ThrottleValue = e.GetChannel(_currentMapping.ThrottleChannel)?.PwmValue ?? 0;
        }
        else
        {
            // Default mapping
            RollValue = Channel1Value;
            PitchValue = Channel2Value;
            ThrottleValue = Channel3Value;
            YawValue = Channel4Value;
        }
    }

    private void ResetChannelValues()
    {
        Channel1Value = Channel2Value = Channel3Value = Channel4Value = 0;
        Channel5Value = Channel6Value = Channel7Value = Channel8Value = 0;
        Channel9Value = Channel10Value = Channel11Value = Channel12Value = 0;
        Channel13Value = Channel14Value = Channel15Value = Channel16Value = 0;
        RollValue = PitchValue = YawValue = ThrottleValue = 0;
    }

    #endregion

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
            StatusMessage = "Loading RC configuration...";

            // Get current configuration
            _currentConfiguration = await _rcCalibrationService.GetRcConfigurationAsync();

            // Get attitude mapping
            _currentMapping = await _rcCalibrationService.GetAttitudeMappingAsync();

            if (_currentMapping != null)
            {
                // Update UI with current mapping
                // Note: These would need proper binding if using ChannelMappingOption
            }

            // Check if calibration is needed
            CalibrationRequired = !await _rcCalibrationService.IsRcCalibratedAsync();
            CalibrationStatusText = CalibrationRequired ? "Radio calibration required" : "Radio calibrated";

            StatusMessage = "RC configuration loaded";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing RC configuration");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CalibrateAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        if (IsCalibrating)
        {
            // Complete/stop calibration
            await _rcCalibrationService.CompleteCalibrationAsync();
        }
        else
        {
            // Start calibration
            await _rcCalibrationService.StartCalibrationAsync();
            StatusMessage = "Move all sticks to their extreme positions...";
        }
    }

    [RelayCommand]
    private async Task UpdateMappingAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Updating attitude channel mapping...";

            var mapping = new AttitudeChannelMapping
            {
                ThrottleChannel = GetSelectedChannel(SelectedThrottleChannel) ?? RcChannel.Channel3,
                RollChannel = GetSelectedChannel(SelectedRollChannel) ?? RcChannel.Channel1,
                PitchChannel = GetSelectedChannel(SelectedPitchChannel) ?? RcChannel.Channel2,
                YawChannel = GetSelectedChannel(SelectedYawChannel) ?? RcChannel.Channel4
            };

            var success = await _rcCalibrationService.UpdateAttitudeMappingAsync(mapping);

            if (success)
            {
                _currentMapping = mapping;
                StatusMessage = "Attitude channel mapping updated successfully";
            }
            else
            {
                StatusMessage = "Failed to update channel mapping";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating channel mapping");
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
            StatusMessage = "Applying PDRL-compliant defaults...";

            if (IsConnected)
            {
                var success = await _rcCalibrationService.ApplyPDRLDefaultsAsync();
                StatusMessage = success ? "PDRL defaults applied successfully" : "Failed to apply defaults";
            }
            else
            {
                // Set UI to defaults
                SelectedThrottleChannel = ChannelOptions.FirstOrDefault(c => c.Channel == RcChannel.Channel3)?.Option;
                SelectedRollChannel = ChannelOptions.FirstOrDefault(c => c.Channel == RcChannel.Channel1)?.Option;
                SelectedPitchChannel = ChannelOptions.FirstOrDefault(c => c.Channel == RcChannel.Channel2)?.Option;
                SelectedYawChannel = ChannelOptions.FirstOrDefault(c => c.Channel == RcChannel.Channel4)?.Option;
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

    private RcChannel? GetSelectedChannel(RcChannelOption? option)
    {
        // Convert option back to channel - this is a simplified approach
        // In a real implementation, you'd have proper two-way mapping
        return option switch
        {
            _ => null
        };
    }

    #endregion

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
            _rcCalibrationService.RcChannelsUpdated -= OnRcChannelsUpdated;
            _rcCalibrationService.CalibrationStateChanged -= OnCalibrationStateChanged;
            _rcCalibrationService.CalibrationCompleted -= OnCalibrationCompleted;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Helper class for channel mapping dropdown options
/// </summary>
public class ChannelMappingOption
{
    public RcChannel Channel { get; set; }
    public string Label { get; set; } = string.Empty;
    public RcChannelOption? Option { get; set; }
}
