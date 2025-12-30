using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Services.Interfaces;
using PavamanDroneConfigurator.Infrastructure.Services;
using PavamanDroneConfigurator.ViewModels;
using PavamanDroneConfigurator.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace PavamanDroneConfigurator;

public partial class App : Application
{
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Prevent application from shutting down when all windows are closed
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            
            // Configure services
            var services = new ServiceCollection();
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            // Start the application flow with splash screen and connection dialog
            Dispatcher.UIThread.Post(async () => await ShowStartupSequenceAsync(desktop));
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task ShowStartupSequenceAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Step 1: Show splash screen
        var splashScreen = new SplashScreen();
        await splashScreen.ShowAndWaitAsync(2500);
        splashScreen.Close();

        // Step 2: Show connection dialog
        var connectionViewModel = Services!.GetRequiredService<ConnectionViewModel>();
        var connectionDialog = new ConnectionDialog
        {
            DataContext = connectionViewModel
        };

        // Subscribe to connection success event
        var mavlinkService = Services.GetRequiredService<IMavlinkService>();
        var connectionSubscription = mavlinkService.LinkState.Subscribe(state =>
        {
            if (state == Core.Services.Interfaces.LinkState.Connected)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    connectionDialog.NotifyConnectionSuccess();
                });
            }
        });

        // Handle connection dialog result
        connectionDialog.ConnectionSucceeded += (sender, args) =>
        {
            ShowMainWindow(desktop);
        };

        connectionDialog.Closed += (sender, args) =>
        {
            connectionSubscription.Dispose();
            
            // If window closed without connection, still show main window
            if (!connectionDialog.IsConnectionSuccessful && desktop.MainWindow == null)
            {
                ShowMainWindow(desktop);
            }
        };

        connectionDialog.Show();
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (desktop.MainWindow != null)
            return;

        desktop.MainWindow = new MainWindow
        {
            DataContext = Services!.GetRequiredService<MainWindowViewModel>()
        };
        
        // Change shutdown mode back to normal once main window is set
        desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
        desktop.MainWindow.Show();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core Services (order matters for dependencies)
        services.AddSingleton<IMavlinkService, MavlinkService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IParameterService, ParameterService>();
        services.AddSingleton<ICalibrationService, CalibrationService>(); // Depends on IMavlinkService
        services.AddSingleton<IMotorTestService, MotorTestService>(); // Depends on IMavlinkService
        services.AddSingleton<IArmingService, ArmingService>(); // Depends on IMavlinkService
        services.AddSingleton<IFlightModeService, FlightModeService>(); // Depends on IMavlinkService

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionViewModel>();
        services.AddTransient<SensorsViewModel>();
        services.AddTransient<SafetyViewModel>();
        services.AddTransient<FlightModesViewModel>();
        services.AddTransient<RcCalibrationViewModel>();
        services.AddTransient<MotorEscViewModel>();
        services.AddTransient<PowerViewModel>();
        services.AddTransient<SprayingConfigViewModel>();
        services.AddTransient<PidTuningViewModel>();
        services.AddTransient<ParametersViewModel>();
    }
}
