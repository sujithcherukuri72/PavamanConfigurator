using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;

namespace PavamanDroneConfigurator.Views;

public partial class ConnectionDialog : Window
{
    public bool IsConnectionSuccessful { get; private set; }
    public event EventHandler? ConnectionSucceeded;

    public ConnectionDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void NotifyConnectionSuccess()
    {
        IsConnectionSuccessful = true;
        ConnectionSucceeded?.Invoke(this, EventArgs.Empty);
        Dispatcher.UIThread.Post(Close);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        IsConnectionSuccessful = false;
        Close();
    }
}
