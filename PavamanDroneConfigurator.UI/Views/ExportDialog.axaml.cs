using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using PavamanDroneConfigurator.UI.ViewModels;

namespace PavamanDroneConfigurator.UI.Views;

public partial class ExportDialog : Window
{
    public ExportDialog()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is ExportDialogViewModel viewModel)
        {
            viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, bool result)
    {
        if (DataContext is ExportDialogViewModel viewModel)
        {
            viewModel.CloseRequested -= OnCloseRequested;
        }

        Close(result);
    }

    private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
    {
        await SelectFolderAsync();
    }

    private async Task SelectFolderAsync()
    {
        var storageProvider = StorageProvider;
        
        var result = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Export Location",
            AllowMultiple = false
        });

        if (result.Count > 0 && DataContext is ExportDialogViewModel viewModel)
        {
            var folder = result[0];
            viewModel.SelectedFilePath = folder.Path.LocalPath;
        }
    }
}
