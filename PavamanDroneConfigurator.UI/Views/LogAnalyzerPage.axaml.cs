using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.UI.Controls;
using PavamanDroneConfigurator.UI.ViewModels;
using System;
using System.Linq;

namespace PavamanDroneConfigurator.UI.Views
{
    public partial class LogAnalyzerPage : UserControl
    {
        private LogGraphControl? _graphControl;

        public LogAnalyzerPage()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Get reference to graph control
            _graphControl = this.FindControl<LogGraphControl>("GraphControl");

            // Find the parent window and set it on the ViewModel
            if (DataContext is LogAnalyzerPageViewModel viewModel)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
                
                // Find parent window
                var window = TopLevel.GetTopLevel(this) as Window;
                if (window != null)
                {
                    viewModel.SetParentWindow(window);
                }
            }
        }

        private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LogAnalyzerPageViewModel.CurrentGraph) && _graphControl != null)
            {
                var viewModel = DataContext as LogAnalyzerPageViewModel;
                _graphControl.UpdateGraph(viewModel?.CurrentGraph);
            }
        }

        private void FieldItem_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is LogFieldInfo field)
            {
                // Toggle selection
                field.IsSelected = !field.IsSelected;

                // Notify ViewModel
                if (DataContext is LogAnalyzerPageViewModel viewModel)
                {
                    viewModel.OnFieldSelectionChanged(field);
                }
            }
        }

        private void ScriptFunction_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.DataContext is Core.Interfaces.ScriptFunctionInfo function)
            {
                if (DataContext is LogAnalyzerPageViewModel viewModel)
                {
                    viewModel.InsertScriptFunction(function);
                }
            }
        }
    }
}
