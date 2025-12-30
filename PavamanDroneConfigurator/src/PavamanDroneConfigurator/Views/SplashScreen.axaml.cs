using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;

namespace PavamanDroneConfigurator.Views;

public partial class SplashScreen : Window
{
    public SplashScreen()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public async Task ShowAndWaitAsync(int milliseconds = 2500)
    {
        Show();
        await Task.Delay(milliseconds);
    }
}
