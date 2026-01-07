using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Infrastructure.Services;
using PavamanDroneConfigurator.UI.ViewModels;
using PavamanDroneConfigurator.UI.Views;
using Avalonia.Threading;
using System;

namespace PavamanDroneConfigurator.UI;

public partial class App : Application
{
    public static ServiceProvider? Services { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        ConfigureServices();
    }

    private void ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core services
        services.AddSingleton<IConnectionService, ConnectionService>();
        services.AddSingleton<IParameterService, ParameterService>();
        services.AddSingleton<ICalibrationService, CalibrationService>();
        services.AddSingleton<ISafetyService, SafetyService>();
        services.AddSingleton<IAirframeService, AirframeService>();
        services.AddSingleton<IPersistenceService, PersistenceService>();
        services.AddSingleton<IFlightModeService, FlightModeService>();
        services.AddSingleton<IMotorEscService, MotorEscService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionPageViewModel>();
        services.AddTransient<AirframePageViewModel>();
        services.AddTransient<ParametersPageViewModel>();
        services.AddTransient<CalibrationPageViewModel>();
        services.AddTransient<SafetyPageViewModel>();
        services.AddTransient<ProfilePageViewModel>();
        services.AddTransient<SplashScreenViewModel>();
        services.AddTransient<FlightModePageViewModel>();
        services.AddTransient<PowerPageViewModel>();
        services.AddTransient<MotorEscPageViewModel>();

        Services = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            var splashScreen = new SplashScreenWindow
            {
                DataContext = Services!.GetRequiredService<SplashScreenViewModel>()
            };
            
            splashScreen.Show();

            Task.Run(async () =>
            {
                try
                {
                    var splashViewModel = (SplashScreenViewModel)splashScreen.DataContext!;
                    await splashViewModel.InitializeAsync();
                }
                catch (Exception ex)
                {
                    // Minimal fallback logging
                    Console.WriteLine($"Splash initialization failed: {ex.Message}");
                }
                finally
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        desktop.MainWindow = new MainWindow
                        {
                            DataContext = Services!.GetRequiredService<MainWindowViewModel>(),
                        };
                        desktop.MainWindow.Show();
                        splashScreen.Close();
                    });
                }
            });

            desktop.Exit += (_, _) => Services?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }
}
