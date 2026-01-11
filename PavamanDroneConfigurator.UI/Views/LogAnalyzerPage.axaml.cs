using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.UI.ViewModels;

namespace PavamanDroneConfigurator.UI.Views;

public partial class LogAnalyzerPage : UserControl
{
    public LogAnalyzerPage()
    {
        InitializeComponent();
        
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is LogAnalyzerPageViewModel viewModel)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                viewModel.SetParentWindow(window);
            }
        }
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        
        if (DataContext is LogAnalyzerPageViewModel viewModel)
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window window)
            {
                viewModel.SetParentWindow(window);
            }
        }
    }

    private void LogRow_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is LogFileInfo logFile)
        {
            logFile.IsSelected = !logFile.IsSelected;
            
            if (DataContext is LogAnalyzerPageViewModel viewModel)
            {
                viewModel.SelectedLogFile = logFile;
            }
        }
    }

    private void FieldItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is LogFieldInfo field)
        {
            if (DataContext is LogAnalyzerPageViewModel viewModel)
            {
                if (field.IsSelected)
                {
                    viewModel.RemoveFieldFromGraphCommand.Execute(field);
                }
                else
                {
                    viewModel.AddFieldToGraphCommand.Execute(field);
                }
            }
        }
    }

    private void ScriptFunction_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ScriptFunctionInfo func)
        {
            if (DataContext is LogAnalyzerPageViewModel viewModel)
            {
                viewModel.InsertScriptFunctionCommand.Execute(func);
            }
        }
    }
}
