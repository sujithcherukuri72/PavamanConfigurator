using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PavamanDroneConfigurator.UI.Views;

public partial class SplashScreenWindow : Window
{
    public SplashScreenWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }
}
