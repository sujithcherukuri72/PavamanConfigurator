using ReactiveUI;
using System.Reactive;
using Microsoft.Extensions.DependencyInjection;

namespace PavamanDroneConfigurator.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase _currentView;
    private string _selectedSection = "Connection";

    public MainWindowViewModel()
    {
        _currentView = App.Services!.GetRequiredService<ConnectionViewModel>();
        
        NavigateToConnectionCommand = ReactiveCommand.Create(NavigateToConnection);
        NavigateToSensorsCommand = ReactiveCommand.Create(NavigateToSensors);
        NavigateToSafetyCommand = ReactiveCommand.Create(NavigateToSafety);
        NavigateToFlightModesCommand = ReactiveCommand.Create(NavigateToFlightModes);
        NavigateToRcCalibrationCommand = ReactiveCommand.Create(NavigateToRcCalibration);
        NavigateToMotorEscCommand = ReactiveCommand.Create(NavigateToMotorEsc);
        NavigateToPowerCommand = ReactiveCommand.Create(NavigateToPower);
        NavigateToSprayingConfigCommand = ReactiveCommand.Create(NavigateToSprayingConfig);
        NavigateToPidTuningCommand = ReactiveCommand.Create(NavigateToPidTuning);
        NavigateToParametersCommand = ReactiveCommand.Create(NavigateToParameters);
    }

    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    public string SelectedSection
    {
        get => _selectedSection;
        set => this.RaiseAndSetIfChanged(ref _selectedSection, value);
    }

    public bool IsConnectionSelected => SelectedSection == "Connection";
    public bool IsSensorsSelected => SelectedSection == "Sensors";
    public bool IsSafetySelected => SelectedSection == "Safety";
    public bool IsFlightModesSelected => SelectedSection == "FlightModes";
    public bool IsRcCalibrationSelected => SelectedSection == "RcCalibration";
    public bool IsMotorEscSelected => SelectedSection == "MotorEsc";
    public bool IsPowerSelected => SelectedSection == "Power";
    public bool IsSprayingConfigSelected => SelectedSection == "SprayingConfig";
    public bool IsPidTuningSelected => SelectedSection == "PidTuning";
    public bool IsParametersSelected => SelectedSection == "Parameters";

    public ReactiveCommand<Unit, Unit> NavigateToConnectionCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToSensorsCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToSafetyCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToFlightModesCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToRcCalibrationCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToMotorEscCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToPowerCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToSprayingConfigCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToPidTuningCommand { get; }
    public ReactiveCommand<Unit, Unit> NavigateToParametersCommand { get; }

    private void NavigateToConnection()
    {
        CurrentView = App.Services!.GetRequiredService<ConnectionViewModel>();
        SelectedSection = "Connection";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToSensors()
    {
        CurrentView = App.Services!.GetRequiredService<SensorsViewModel>();
        SelectedSection = "Sensors";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToSafety()
    {
        CurrentView = App.Services!.GetRequiredService<SafetyViewModel>();
        SelectedSection = "Safety";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToFlightModes()
    {
        CurrentView = App.Services!.GetRequiredService<FlightModesViewModel>();
        SelectedSection = "FlightModes";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToRcCalibration()
    {
        CurrentView = App.Services!.GetRequiredService<RcCalibrationViewModel>();
        SelectedSection = "RcCalibration";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToMotorEsc()
    {
        CurrentView = App.Services!.GetRequiredService<MotorEscViewModel>();
        SelectedSection = "MotorEsc";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToPower()
    {
        CurrentView = App.Services!.GetRequiredService<PowerViewModel>();
        SelectedSection = "Power";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToSprayingConfig()
    {
        CurrentView = App.Services!.GetRequiredService<SprayingConfigViewModel>();
        SelectedSection = "SprayingConfig";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToPidTuning()
    {
        CurrentView = App.Services!.GetRequiredService<PidTuningViewModel>();
        SelectedSection = "PidTuning";
        RaiseNavigationPropertyChanged();
    }

    private void NavigateToParameters()
    {
        CurrentView = App.Services!.GetRequiredService<ParametersViewModel>();
        SelectedSection = "Parameters";
        RaiseNavigationPropertyChanged();
    }

    private void RaiseNavigationPropertyChanged()
    {
        this.RaisePropertyChanged(nameof(IsConnectionSelected));
        this.RaisePropertyChanged(nameof(IsSensorsSelected));
        this.RaisePropertyChanged(nameof(IsSafetySelected));
        this.RaisePropertyChanged(nameof(IsFlightModesSelected));
        this.RaisePropertyChanged(nameof(IsRcCalibrationSelected));
        this.RaisePropertyChanged(nameof(IsMotorEscSelected));
        this.RaisePropertyChanged(nameof(IsPowerSelected));
        this.RaisePropertyChanged(nameof(IsSprayingConfigSelected));
        this.RaisePropertyChanged(nameof(IsPidTuningSelected));
        this.RaisePropertyChanged(nameof(IsParametersSelected));
    }
}
