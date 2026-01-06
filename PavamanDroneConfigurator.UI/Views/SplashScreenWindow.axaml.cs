using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace pavamanDroneConfigurator.UI.Views;

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
