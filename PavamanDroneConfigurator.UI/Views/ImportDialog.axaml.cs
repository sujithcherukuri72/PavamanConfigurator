using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.UI.ViewModels;

namespace PavamanDroneConfigurator.UI.Views;

public partial class ImportDialog : Window
{
    private readonly IImportService? _importService;

    public ImportDialog()
    {
        InitializeComponent();
        
        // Get ImportService from DI
        _importService = App.Services?.GetService(typeof(IImportService)) as IImportService;
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ImportDialogViewModel viewModel)
        {
            viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        if (DataContext is ImportDialogViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }

        Close(result);
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        await SelectFileAsync();
    }

    private async Task SelectFileAsync()
    {
        if (_importService == null || DataContext is not ImportDialogViewModel viewModel)
            return;

        var storageProvider = StorageProvider;

        // Define supported file types
        var fileTypes = new[]
        {
            new FilePickerFileType("All Supported Files")
            {
                Patterns = new[] { "*.csv", "*.params", "*.cfg", "*.json", "*.yaml", "*.yml" }
            },
            new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } },
            new FilePickerFileType("ArduPilot Parameters") { Patterns = new[] { "*.params" } },
            new FilePickerFileType("Configuration Files") { Patterns = new[] { "*.cfg" } },
            new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } },
            new FilePickerFileType("YAML Files") { Patterns = new[] { "*.yaml", "*.yml" } }
        };

        var result = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Parameter File to Import",
            AllowMultiple = false,
            FileTypeFilter = fileTypes
        });

        if (result.Count > 0)
        {
            var file = result[0];
            var filePath = file.Path.LocalPath;

            viewModel.SetLoading(true, "Parsing file...");

            try
            {
                var importResult = await _importService.ImportFromFileAsync(filePath);
                viewModel.SetImportResult(importResult, filePath);
            }
            catch (Exception ex)
            {
                viewModel.SetImportResult(
                    new ImportResult { ErrorMessage = ex.Message }, 
                    filePath);
            }
        }
    }
}
