using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Infrastructure.Services;
using PavanamDroneConfigurator.UI.ViewModels;
using PavanamDroneConfigurator.UI.Views;

namespace PavanamDroneConfigurator.UI;

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

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<ConnectionPageViewModel>();
        services.AddTransient<AirframePageViewModel>();
        services.AddTransient<ParametersPageViewModel>();
        services.AddTransient<CalibrationPageViewModel>();
        services.AddTransient<SafetyPageViewModel>();
        services.AddTransient<ProfilePageViewModel>();
        services.AddTransient<SplashScreenViewModel>();

        Services = services.BuildServiceProvider();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            
            // Show splash screen first
            var splashScreen = new SplashScreenWindow
            {
                DataContext = Services!.GetRequiredService<SplashScreenViewModel>()
            };
            
            splashScreen.Show();

            // Initialize app in background and show main window when ready
            Task.Run(async () =>
            {
                var splashViewModel = (SplashScreenViewModel)splashScreen.DataContext!;
                await splashViewModel.InitializeAsync();
                
                // Show main window on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    desktop.MainWindow = new MainWindow
                    {
                        DataContext = Services!.GetRequiredService<MainWindowViewModel>(),
                    };
                    desktop.MainWindow.Show();
                    splashScreen.Close();
                });
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
