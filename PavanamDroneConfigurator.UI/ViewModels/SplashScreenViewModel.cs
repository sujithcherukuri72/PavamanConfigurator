using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Threading;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class SplashScreenViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _loadingMessage = "Initializing...";

    [ObservableProperty]
    private double _progress = 0;

    [ObservableProperty]
    private bool _isIndeterminate = true;

    [ObservableProperty]
    private string _versionText = "Version 1.0.0";

    [ObservableProperty]
    private bool _hasLogo = false;

    [ObservableProperty]
    private bool _hasBackground = false;

    [ObservableProperty]
    private bool _hasSplash = false;

    public SplashScreenViewModel()
    {
        CheckForAssets();
        LoadVersionInfo();
    }

    private void CheckForAssets()
    {
        try
        {
            // Check for logo.ico
            HasLogo = AssetExists("avares://PavanamDroneConfigurator.UI/Assets/Images/logo.ico");
            
            // Check for background.jpg
            HasBackground = AssetExists("avares://PavanamDroneConfigurator.UI/Assets/Images/background.jpg");
            
            // Check for splash.png
            HasSplash = AssetExists("avares://PavanamDroneConfigurator.UI/Assets/Images/splash.png");
        }
        catch
        {
            HasLogo = false;
            HasBackground = false;
            HasSplash = false;
        }
    }

    private bool AssetExists(string uri)
    {
        try
        {
            var assetStream = AssetLoader.Open(new Uri(uri));
            assetStream?.Dispose();
            return assetStream != null;
        }
        catch
        {
            return false;
        }
    }

    private void LoadVersionInfo()
    {
        var version = typeof(SplashScreenViewModel).Assembly.GetName().Version;
        VersionText = $"Version {version?.Major}.{version?.Minor}.{version?.Build ?? 0}";
    }

    public async Task InitializeAsync()
    {
        await UpdateProgress("Loading core services...", 0);
        await Task.Delay(300);

        await UpdateProgress("Initializing connection manager...", 25);
        await Task.Delay(300);

        await UpdateProgress("Loading parameter definitions...", 50);
        await Task.Delay(300);

        await UpdateProgress("Preparing user interface...", 75);
        await Task.Delay(300);

        await UpdateProgress("Ready!", 100);
        await Task.Delay(200);
    }

    private async Task UpdateProgress(string message, double progress)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            LoadingMessage = message;
            Progress = progress;
            IsIndeterminate = false;
        });
    }
}
