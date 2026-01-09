using Avalonia.Controls;
using Avalonia.Input;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.UI.ViewModels;

namespace PavamanDroneConfigurator.UI.Views;

public partial class ParametersPage : UserControl
{
    public ParametersPage()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Handles parameter row selection when clicked.
    /// </summary>
    private void OnParameterRowPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.Tag is DroneParameter parameter)
        {
            if (DataContext is ParametersPageViewModel vm)
            {
                vm.SelectedParameter = parameter;
            }
        }
    }
}
