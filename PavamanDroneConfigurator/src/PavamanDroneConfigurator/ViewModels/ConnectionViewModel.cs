using ReactiveUI;
using System.Reactive;
using System.Collections.ObjectModel;
using System.IO.Ports;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Services.Interfaces;
using PavamanDroneConfigurator.Infrastructure.Helpers;

namespace PavamanDroneConfigurator.ViewModels;

public class ConnectionViewModel : ViewModelBase
{
    private readonly IMavlinkService _mavlinkService;
    
    private ConnectionType _selectedConnectionType = ConnectionType.Serial;
    private string? _selectedPort;
    private int _selectedBaudRate = 115200;
    private int _selectedDataBits = 8;
    private Parity _selectedParity = Parity.None;
    private StopBits _selectedStopBits = StopBits.One;
    private string _host = "127.0.0.1";
    private int _port = 5760;
    private bool _isConnected;
    private string _connectionStatus = "Disconnected";

    public ConnectionViewModel(IMavlinkService mavlinkService)
    {
        _mavlinkService = mavlinkService;
        
        // Initialize available ports
        AvailablePorts = new ObservableCollection<string>(SerialPortHelper.GetAvailablePorts());
        AvailableBaudRates = new ObservableCollection<int>(SerialPortHelper.GetStandardBaudRates());
        AvailableDataBits = new ObservableCollection<int> { 6, 7, 8 };
        AvailableParities = new ObservableCollection<Parity> { Parity.None, Parity.Even, Parity.Odd };
        AvailableStopBits = new ObservableCollection<StopBits> { StopBits.One, StopBits.Two };
        
        if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
        
        ConnectCommand = ReactiveCommand.CreateFromTask(ConnectAsync);
        DisconnectCommand = ReactiveCommand.CreateFromTask(DisconnectAsync);
        RefreshPortsCommand = ReactiveCommand.Create(RefreshPorts);
        
        // Subscribe to link state
        _mavlinkService.LinkState.Subscribe(state =>
        {
            IsConnected = state == Core.Services.Interfaces.LinkState.Connected;
            ConnectionStatus = state.ToString();
        });
    }

    public ObservableCollection<string> AvailablePorts { get; }
    public ObservableCollection<int> AvailableBaudRates { get; }
    public ObservableCollection<int> AvailableDataBits { get; }
    public ObservableCollection<Parity> AvailableParities { get; }
    public ObservableCollection<StopBits> AvailableStopBits { get; }

    public ConnectionType SelectedConnectionType
    {
        get => _selectedConnectionType;
        set => this.RaiseAndSetIfChanged(ref _selectedConnectionType, value);
    }

    public string? SelectedPort
    {
        get => _selectedPort;
        set => this.RaiseAndSetIfChanged(ref _selectedPort, value);
    }

    public int SelectedBaudRate
    {
        get => _selectedBaudRate;
        set => this.RaiseAndSetIfChanged(ref _selectedBaudRate, value);
    }

    public int SelectedDataBits
    {
        get => _selectedDataBits;
        set => this.RaiseAndSetIfChanged(ref _selectedDataBits, value);
    }

    public Parity SelectedParity
    {
        get => _selectedParity;
        set => this.RaiseAndSetIfChanged(ref _selectedParity, value);
    }

    public StopBits SelectedStopBits
    {
        get => _selectedStopBits;
        set => this.RaiseAndSetIfChanged(ref _selectedStopBits, value);
    }

    public string Host
    {
        get => _host;
        set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    public int Port
    {
        get => _port;
        set => this.RaiseAndSetIfChanged(ref _port, value);
    }

    public bool IsConnected
    {
        get => _isConnected;
        set => this.RaiseAndSetIfChanged(ref _isConnected, value);
    }

    public string ConnectionStatus
    {
        get => _connectionStatus;
        set => this.RaiseAndSetIfChanged(ref _connectionStatus, value);
    }

    public ReactiveCommand<Unit, Unit> ConnectCommand { get; }
    public ReactiveCommand<Unit, Unit> DisconnectCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshPortsCommand { get; }

    private async Task ConnectAsync()
    {
        bool success = false;
        
        switch (SelectedConnectionType)
        {
            case ConnectionType.Serial:
                if (!string.IsNullOrEmpty(SelectedPort))
                {
                    success = await _mavlinkService.ConnectSerialAsync(
                        SelectedPort, SelectedBaudRate, SelectedDataBits, 
                        SelectedParity, SelectedStopBits);
                }
                break;
            case ConnectionType.TCP:
                success = await _mavlinkService.ConnectTcpAsync(Host, Port);
                break;
            case ConnectionType.UDP:
                success = await _mavlinkService.ConnectUdpAsync(Host, Port);
                break;
        }
        
        if (!success)
        {
            ConnectionStatus = "Connection failed";
        }
    }

    private async Task DisconnectAsync()
    {
        await _mavlinkService.DisconnectAsync();
    }

    private void RefreshPorts()
    {
        var currentPort = SelectedPort;
        AvailablePorts.Clear();
        foreach (var port in SerialPortHelper.GetAvailablePorts())
        {
            AvailablePorts.Add(port);
        }
        
        if (AvailablePorts.Contains(currentPort!))
            SelectedPort = currentPort;
        else if (AvailablePorts.Count > 0)
            SelectedPort = AvailablePorts[0];
    }
}
