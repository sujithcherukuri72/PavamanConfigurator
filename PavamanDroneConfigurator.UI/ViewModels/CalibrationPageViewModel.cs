using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.UI.ViewModels;

public partial class CalibrationPageViewModel : ViewModelBase
{
    private readonly ICalibrationService _calibrationService;
    private readonly IConnectionService _connectionService;

    [ObservableProperty]
    private CalibrationStateModel? _currentState;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isCalibrating;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _calibrationProgress;

    [ObservableProperty]
    private string _calibrationInstructions = string.Empty;

    [ObservableProperty]
    private CalibrationStep _currentStep;

    [ObservableProperty]
    private int _currentStepNumber;

    [ObservableProperty]
    private int _totalSteps;

    [ObservableProperty]
    private bool _requiresUserAction;

    public CalibrationPageViewModel(ICalibrationService calibrationService, IConnectionService connectionService)
    {
        _calibrationService = calibrationService;
        _connectionService = connectionService;

        // Subscribe to connection events
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        IsConnected = _connectionService.IsConnected;

        // Subscribe to calibration events
        _calibrationService.CalibrationStateChanged += OnCalibrationStateChanged;
        _calibrationService.CalibrationProgressChanged += OnCalibrationProgressChanged;
        _calibrationService.CalibrationStepRequired += OnCalibrationStepRequired;
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        IsConnected = connected;
        if (!connected && IsCalibrating)
        {
            StatusMessage = "Connection lost during calibration";
            IsCalibrating = false;
        }
    }

    private void OnCalibrationStateChanged(object? sender, CalibrationStateModel state)
    {
        CurrentState = state;
        StatusMessage = state.Message ?? "Ready";
        IsCalibrating = state.State == CalibrationState.InProgress;
        CalibrationProgress = state.Progress;

        if (state.State == CalibrationState.Completed)
        {
            RequiresUserAction = false;
            CalibrationInstructions = "Calibration completed successfully!";
        }
        else if (state.State == CalibrationState.Failed)
        {
            RequiresUserAction = false;
            CalibrationInstructions = state.Message ?? "Calibration failed";
        }
    }

    private void OnCalibrationProgressChanged(object? sender, CalibrationProgressEventArgs e)
    {
        CalibrationProgress = e.ProgressPercent;
        StatusMessage = e.StatusText ?? StatusMessage;
        CurrentStepNumber = e.CurrentStep ?? 0;
        TotalSteps = e.TotalSteps ?? 1;
    }

    private void OnCalibrationStepRequired(object? sender, CalibrationStepEventArgs e)
    {
        CurrentStep = e.Step;
        CalibrationInstructions = e.Instructions ?? GetStepInstructions(e.Step);
        RequiresUserAction = true;
    }

    private static string GetStepInstructions(CalibrationStep step) => step switch
    {
        CalibrationStep.Level => "Place the vehicle LEVEL on a flat surface",
        CalibrationStep.LeftSide => "Place the vehicle on its LEFT SIDE",
        CalibrationStep.RightSide => "Place the vehicle on its RIGHT SIDE",
        CalibrationStep.NoseDown => "Place the vehicle NOSE DOWN",
        CalibrationStep.NoseUp => "Place the vehicle NOSE UP",
        CalibrationStep.Back => "Place the vehicle on its BACK (upside down)",
        CalibrationStep.Rotate => "Slowly rotate the vehicle in all directions",
        CalibrationStep.KeepStill => "Keep the vehicle completely still",
        _ => "Follow the instructions"
    };

    [RelayCommand]
    private async Task CalibrateAccelerometerAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        RequiresUserAction = false;
        CalibrationInstructions = "Starting accelerometer calibration...";
        await _calibrationService.StartAccelerometerCalibrationAsync(fullSixAxis: true);
    }

    [RelayCommand]
    private async Task CalibrateCompassAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        RequiresUserAction = false;
        CalibrationInstructions = "Starting compass calibration...";
        await _calibrationService.StartCompassCalibrationAsync(onboardCalibration: false);
    }

    [RelayCommand]
    private async Task CalibrateGyroscopeAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        RequiresUserAction = false;
        CalibrationInstructions = "Keep vehicle still - calibrating gyroscope...";
        await _calibrationService.StartGyroscopeCalibrationAsync();
    }

    [RelayCommand]
    private async Task CalibrateLevelHorizonAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        RequiresUserAction = false;
        CalibrationInstructions = "Level horizon calibration...";
        await _calibrationService.StartLevelHorizonCalibrationAsync();
    }

    [RelayCommand]
    private async Task CalibrateBarometerAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        RequiresUserAction = false;
        CalibrationInstructions = "Calibrating barometer...";
        await _calibrationService.StartBarometerCalibrationAsync();
    }

    [RelayCommand]
    private async Task AcceptStepAsync()
    {
        if (!IsCalibrating)
            return;

        RequiresUserAction = false;
        await _calibrationService.AcceptCalibrationStepAsync();
    }

    [RelayCommand]
    private async Task CancelCalibrationAsync()
    {
        await _calibrationService.CancelCalibrationAsync();
        RequiresUserAction = false;
        CalibrationInstructions = "Calibration cancelled";
    }

    [RelayCommand]
    private async Task RebootFlightControllerAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to vehicle";
            return;
        }

        StatusMessage = "Rebooting flight controller...";
        var success = await _calibrationService.RebootFlightControllerAsync();
        StatusMessage = success ? "Reboot command sent" : "Failed to send reboot command";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
            _calibrationService.CalibrationStateChanged -= OnCalibrationStateChanged;
            _calibrationService.CalibrationProgressChanged -= OnCalibrationProgressChanged;
            _calibrationService.CalibrationStepRequired -= OnCalibrationStepRequired;
        }
        base.Dispose(disposing);
    }
}
