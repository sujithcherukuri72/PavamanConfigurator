using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.UI.ViewModels;

/// <summary>
/// ViewModel for the Import Parameters dialog.
/// </summary>
public partial class ImportDialogViewModel : ViewModelBase
{
    /// <summary>
    /// The import result from parsing the file.
    /// </summary>
    [ObservableProperty]
    private ImportResult? _importResult;

    /// <summary>
    /// The selected file path.
    /// </summary>
    [ObservableProperty]
    private string? _selectedFilePath;

    /// <summary>
    /// The display name of the selected file.
    /// </summary>
    [ObservableProperty]
    private string? _selectedFileName;

    /// <summary>
    /// Status message for the user.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = "Select a parameter file to import.";

    /// <summary>
    /// Whether the dialog is currently loading/parsing.
    /// </summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Whether we have a valid import result ready to apply.
    /// </summary>
    [ObservableProperty]
    private bool _hasValidResult;

    /// <summary>
    /// Whether to merge with existing parameters (true) or replace all (false).
    /// </summary>
    [ObservableProperty]
    private bool _mergeWithExisting = true;

    /// <summary>
    /// List of warnings from the import.
    /// </summary>
    public ObservableCollection<string> Warnings { get; } = new();

    /// <summary>
    /// Whether the import has warnings.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    /// Gets whether the dialog can proceed with import.
    /// </summary>
    public bool CanImport => HasValidResult && !IsLoading;

    /// <summary>
    /// Event raised when the dialog should close with a result.
    /// </summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>
    /// Supported file extensions filter for the file picker.
    /// </summary>
    public string FileFilter => "All Supported|*.csv;*.params;*.cfg;*.json;*.yaml;*.yml|" +
                                "CSV Files|*.csv|" +
                                "ArduPilot Parameters|*.params|" +
                                "Configuration Files|*.cfg|" +
                                "JSON Files|*.json|" +
                                "YAML Files|*.yaml;*.yml";

    public ImportDialogViewModel()
    {
    }

    /// <summary>
    /// Sets the import result after file parsing.
    /// </summary>
    public void SetImportResult(ImportResult result, string? filePath)
    {
        ImportResult = result;
        SelectedFilePath = filePath;
        SelectedFileName = string.IsNullOrEmpty(filePath) ? null : System.IO.Path.GetFileName(filePath);

        Warnings.Clear();
        if (result.Warnings != null)
        {
            foreach (var warning in result.Warnings)
            {
                Warnings.Add(warning);
            }
        }
        OnPropertyChanged(nameof(HasWarnings));

        if (result.IsSuccess)
        {
            HasValidResult = true;
            var warnText = result.Warnings?.Count > 0 ? $" ({result.Warnings.Count} warnings)" : "";
            StatusMessage = $"? Found {result.SuccessCount} parameters{warnText}";
            
            if (result.DuplicateCount > 0)
            {
                StatusMessage += $"\n  • {result.DuplicateCount} duplicate keys (latest value used)";
            }
            if (result.SkippedCount > 0)
            {
                StatusMessage += $"\n  • {result.SkippedCount} invalid rows skipped";
            }
        }
        else
        {
            HasValidResult = false;
            StatusMessage = $"? Import failed: {result.ErrorMessage ?? "Unknown error"}";
        }

        OnPropertyChanged(nameof(CanImport));
        IsLoading = false;
    }

    /// <summary>
    /// Sets the loading state.
    /// </summary>
    public void SetLoading(bool loading, string? message = null)
    {
        IsLoading = loading;
        if (loading && message != null)
        {
            StatusMessage = message;
        }
        OnPropertyChanged(nameof(CanImport));
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanImport));
    }

    partial void OnHasValidResultChanged(bool value)
    {
        OnPropertyChanged(nameof(CanImport));
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, false);
    }

    [RelayCommand]
    private void Import()
    {
        if (!CanImport)
            return;

        CloseRequested?.Invoke(this, true);
    }
}
