using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.UI.ViewModels;

public partial class DroneDetailsPageViewModel : ViewModelBase
{
    private readonly IDroneInfoService _droneInfoService;
    private readonly IConnectionService _connectionService;

    [ObservableProperty]
    private string _droneId = "Not Connected";

    [ObservableProperty]
    private string _fcId = "Not Connected";

    [ObservableProperty]
    private string _firmwareVersion = "-";

    [ObservableProperty]
    private string _codeChecksum = "-";

    [ObservableProperty]
    private string _dataChecksum = "-";

    [ObservableProperty]
    private string _vehicleType = "-";

    [ObservableProperty]
    private string _autopilotType = "-";

    [ObservableProperty]
    private string _boardType = "-";

    [ObservableProperty]
    private string _flightMode = "-";

    [ObservableProperty]
    private bool _isArmed;

    [ObservableProperty]
    private byte _systemId;

    [ObservableProperty]
    private byte _componentId;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Connect to a drone to view details";

    public DroneDetailsPageViewModel(
        IDroneInfoService droneInfoService,
        IConnectionService connectionService)
    {
        _droneInfoService = droneInfoService;
        _connectionService = connectionService;

        // Subscribe to events
        _droneInfoService.DroneInfoUpdated += OnDroneInfoUpdated;
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;

        // Initialize state
        IsConnected = _connectionService.IsConnected;
        if (IsConnected)
        {
            _ = RefreshAsync();
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
            if (connected)
            {
                StatusMessage = "Loading drone details...";
                _ = RefreshAsync();
            }
            else
            {
                ClearDroneInfo();
                StatusMessage = "Connect to a drone to view details";
            }
        });
    }

    private void OnDroneInfoUpdated(object? sender, DroneInfo info)
    {
        Dispatcher.UIThread.Post(() => UpdateFromDroneInfo(info));
    }

    private void UpdateFromDroneInfo(DroneInfo info)
    {
        DroneId = info.DroneId;
        FcId = info.FcId;
        FirmwareVersion = info.FirmwareVersion;
        CodeChecksum = info.CodeChecksum;
        DataChecksum = info.DataChecksum;
        VehicleType = info.VehicleType;
        AutopilotType = info.AutopilotType;
        BoardType = info.BoardType;
        FlightMode = info.FlightMode;
        IsArmed = info.IsArmed;
        SystemId = info.SystemId;
        ComponentId = info.ComponentId;
        StatusMessage = "Drone information loaded";
    }

    private void ClearDroneInfo()
    {
        DroneId = "Not Connected";
        FcId = "Not Connected";
        FirmwareVersion = "-";
        CodeChecksum = "-";
        DataChecksum = "-";
        VehicleType = "-";
        AutopilotType = "-";
        BoardType = "-";
        FlightMode = "-";
        IsArmed = false;
        SystemId = 0;
        ComponentId = 0;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to a drone";
            return;
        }

        IsLoading = true;
        StatusMessage = "Refreshing drone details...";

        try
        {
            await _droneInfoService.RefreshDroneInfoAsync();
            var info = await _droneInfoService.GetDroneInfoAsync();
            
            if (info != null)
            {
                UpdateFromDroneInfo(info);
            }
            else
            {
                StatusMessage = "Unable to retrieve drone information";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _droneInfoService.DroneInfoUpdated -= OnDroneInfoUpdated;
            _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
        }
        base.Dispose(disposing);
    }
}
