using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using PavamanDroneConfigurator.UI.Views;

namespace PavamanDroneConfigurator.UI.ViewModels;

public partial class ParametersPageViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;
    private readonly IExportService _exportService;
    private readonly IImportService _importService;
    
    // Track original values for change detection
    private readonly Dictionary<string, float> _originalValues = new();

    // Track if parameters are fully loaded to prevent progress updates from overwriting
    private bool _parametersLoaded;
    
    // Track if we're currently saving to prevent recursive saves
    private bool _isSaving;

    [ObservableProperty]
    private ObservableCollection<DroneParameter> _parameters = new();

    [ObservableProperty]
    private ObservableCollection<DroneParameter> _filteredParameters = new();

    [ObservableProperty]
    private DroneParameter? _selectedParameter;

    [ObservableProperty]
    private string _statusMessage = "Connect to your drone to load parameters";

    [ObservableProperty]
    private bool _canEditParameters;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalParameterCount;

    [ObservableProperty]
    private int _loadedParameterCount;

    [ObservableProperty]
    private int _modifiedParameterCount;

    [ObservableProperty]
    private bool _hasUnsavedChanges;
    
    [ObservableProperty]
    private bool _isRefreshing;

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public ParametersPageViewModel(
        IParameterService parameterService, 
        IConnectionService connectionService, 
        IExportService exportService,
        IImportService importService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;
        _exportService = exportService;
        _importService = importService;

        // Subscribe to all relevant events
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _parameterService.ParameterDownloadStarted += OnDownloadStarted;
        _parameterService.ParameterDownloadCompleted += OnDownloadCompleted;
        _parameterService.ParameterDownloadProgressChanged += OnProgressChanged;
        
        // Check if already connected and has parameters
        if (_connectionService.IsConnected && _parameterService.IsParameterDownloadComplete)
        {
            _ = LoadParametersIntoGridAsync();
        }
    }

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!connected)
            {
                // Clear everything when disconnected
                UnsubscribeFromParameterChanges();
                Parameters.Clear();
                FilteredParameters.Clear();
                _originalValues.Clear();
                TotalParameterCount = 0;
                LoadedParameterCount = 0;
                ModifiedParameterCount = 0;
                HasUnsavedChanges = false;
                CanEditParameters = false;
                IsRefreshing = false;
                _parametersLoaded = false;
                StatusMessage = "Disconnected - Connect to your drone to load parameters";
            }
            else
            {
                StatusMessage = "Connected - Waiting for parameters...";
            }
        });
    }

    private void OnDownloadStarted(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsRefreshing = true;
            CanEditParameters = false;
            _parametersLoaded = false;
            UnsubscribeFromParameterChanges();
            Parameters.Clear();
            FilteredParameters.Clear();
            _originalValues.Clear();
            TotalParameterCount = 0;
            LoadedParameterCount = 0;
            ModifiedParameterCount = 0;
            HasUnsavedChanges = false;
            StatusMessage = "Downloading parameters from drone...";
        });
    }

    private void OnDownloadCompleted(object? sender, bool success)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            IsRefreshing = false;
            
            if (success && _parameterService.ReceivedParameterCount > 0)
            {
                await LoadParametersIntoGridAsync();
                CanEditParameters = true;
                _parametersLoaded = true;
                StatusMessage = $"Successfully loaded {Parameters.Count} parameters - Edit values to update vehicle";
            }
            else
            {
                StatusMessage = "No parameters received from drone";
                CanEditParameters = false;
                _parametersLoaded = false;
            }
        });
    }

    private void OnProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_parametersLoaded)
            {
                return;
            }
            
            var received = _parameterService.ReceivedParameterCount;
            var expected = _parameterService.ExpectedParameterCount;
            
            TotalParameterCount = expected ?? 0;
            LoadedParameterCount = received;
            
            if (_parameterService.IsParameterDownloadInProgress)
            {
                var expectedStr = expected?.ToString() ?? "?";
                StatusMessage = $"Downloading parameters... {received}/{expectedStr}";
            }
        });
    }

    private async Task LoadParametersIntoGridAsync()
    {
        try
        {
            UnsubscribeFromParameterChanges();
            
            var allParams = await _parameterService.GetAllParametersAsync();
            
            Parameters.Clear();
            FilteredParameters.Clear();
            _originalValues.Clear();
            
            foreach (var p in allParams)
            {
                _originalValues[p.Name] = p.Value;
                p.OriginalValue = p.Value;
                p.PropertyChanged += OnParameterPropertyChanged;
                Parameters.Add(p);
                FilteredParameters.Add(p);
            }
            
            TotalParameterCount = Parameters.Count;
            LoadedParameterCount = FilteredParameters.Count;
            ModifiedParameterCount = 0;
            HasUnsavedChanges = false;
            
            OnPropertyChanged(nameof(Parameters));
            OnPropertyChanged(nameof(FilteredParameters));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading parameters: {ex.Message}";
        }
    }

    private void UnsubscribeFromParameterChanges()
    {
        foreach (var p in Parameters)
        {
            p.PropertyChanged -= OnParameterPropertyChanged;
        }
    }

    private void OnParameterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not DroneParameter parameter || e.PropertyName != nameof(DroneParameter.Value))
            return;
        
        if (_isSaving)
            return;
        
        UpdateModifiedCount();
        _ = SaveParameterToVehicleAsync(parameter);
    }

    private void UpdateModifiedCount()
    {
        ModifiedParameterCount = Parameters.Count(p => p.IsModified);
        HasUnsavedChanges = ModifiedParameterCount > 0;
    }

    private async Task SaveParameterToVehicleAsync(DroneParameter parameter)
    {
        if (!_connectionService.IsConnected || _isSaving)
        {
            return;
        }

        _isSaving = true;
        try
        {
            StatusMessage = $"Updating {parameter.Name}...";
            
            var success = await _parameterService.SetParameterAsync(parameter.Name, parameter.Value);
            
            if (success)
            {
                parameter.MarkAsSaved();
                _originalValues[parameter.Name] = parameter.Value;
                UpdateModifiedCount();
                StatusMessage = $"? Updated {parameter.Name} = {parameter.Value}";
            }
            else
            {
                StatusMessage = $"? Failed to update {parameter.Name}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating {parameter.Name}: {ex.Message}";
        }
        finally
        {
            _isSaving = false;
        }
    }

    [RelayCommand]
    private async Task RefreshParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected";
            return;
        }

        await _parameterService.RefreshParametersAsync();
    }

    [RelayCommand]
    private async Task ImportParametersAsync()
    {
        try
        {
            // Get the main window
            var mainWindow = GetMainWindow();
            if (mainWindow == null)
            {
                StatusMessage = "Error: Could not find main window.";
                return;
            }

            // Create and show the import dialog
            var dialogViewModel = new ImportDialogViewModel();
            var dialog = new ImportDialog
            {
                DataContext = dialogViewModel
            };

            var result = await dialog.ShowDialog<bool>(mainWindow);

            if (result && dialogViewModel.ImportResult != null && dialogViewModel.ImportResult.IsSuccess)
            {
                IsRefreshing = true;
                StatusMessage = "Applying imported parameters...";

                try
                {
                    var importedParams = dialogViewModel.ImportResult.Parameters;
                    var mergeWithExisting = dialogViewModel.MergeWithExisting;

                    // Apply imported parameters to loadedParams
                    ApplyImportedParameters(importedParams, mergeWithExisting);

                    // Update UI
                    ApplyFilter();
                    UpdateModifiedCount();

                    var actionText = mergeWithExisting ? "merged" : "replaced";
                    StatusMessage = $"? Successfully {actionText} {importedParams.Count} parameters from file";
                }
                finally
                {
                    IsRefreshing = false;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Import error: {ex.Message}";
            IsRefreshing = false;
        }
    }

    /// <summary>
    /// Applies imported parameters to the current parameter list.
    /// </summary>
    private void ApplyImportedParameters(Dictionary<string, float> importedParams, bool mergeWithExisting)
    {
        if (!mergeWithExisting)
        {
            // Replace mode: Clear all and add only imported
            // For now, we update existing parameters that match imported keys
            // Parameters not in import file are left unchanged (since we may not have full parameter set)
        }

        var updatedCount = 0;
        var newCount = 0;

        foreach (var (name, value) in importedParams)
        {
            // Find existing parameter
            var existingParam = Parameters.FirstOrDefault(p => 
                p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existingParam != null)
            {
                // Update existing parameter
                if (Math.Abs(existingParam.Value - value) > 0.0001f)
                {
                    existingParam.Value = value;
                    updatedCount++;
                }
            }
            else
            {
                // Add new parameter (not in current list)
                var newParam = new DroneParameter
                {
                    Name = name,
                    Value = value,
                    OriginalValue = value,
                    Description = "Imported parameter"
                };
                newParam.PropertyChanged += OnParameterPropertyChanged;
                Parameters.Add(newParam);
                _originalValues[name] = value;
                newCount++;
            }
        }

        TotalParameterCount = Parameters.Count;
        
        // Log what was done
        if (newCount > 0)
        {
            StatusMessage = $"? Updated {updatedCount} parameters, added {newCount} new parameters";
        }
        else if (updatedCount > 0)
        {
            StatusMessage = $"? Updated {updatedCount} parameters";
        }
    }

    /// <summary>
    /// Applies the current search filter to update FilteredParameters.
    /// </summary>
    private void ApplyFilter()
    {
        FilteredParameters.Clear();
        
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? Parameters
            : Parameters.Where(p => 
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        
        foreach (var p in filtered)
        {
            FilteredParameters.Add(p);
        }
        
        LoadedParameterCount = FilteredParameters.Count;
    }

    [RelayCommand]
    private async Task ExportParametersAsync()
    {
        if (Parameters.Count == 0)
        {
            StatusMessage = "No parameters to export. Load parameters first.";
            return;
        }

        try
        {
            // Get the main window
            var mainWindow = GetMainWindow();
            if (mainWindow == null)
            {
                StatusMessage = "Error: Could not find main window.";
                return;
            }

            // Create and show the export dialog
            var dialogViewModel = new ExportDialogViewModel();
            var dialog = new ExportDialog
            {
                DataContext = dialogViewModel
            };

            var result = await dialog.ShowDialog<bool>(mainWindow);

            if (result && dialogViewModel.SelectedFormat != null && !string.IsNullOrWhiteSpace(dialogViewModel.FullFilePath))
            {
                StatusMessage = "Exporting parameters...";
                IsRefreshing = true;

                try
                {
                    var success = await _exportService.ExportToFileAsync(
                        Parameters,
                        dialogViewModel.SelectedFormat.Format,
                        dialogViewModel.FullFilePath);

                    if (success)
                    {
                        StatusMessage = $"? Successfully exported {Parameters.Count} parameters to {dialogViewModel.FullFilePath}";
                    }
                    else
                    {
                        StatusMessage = "? Failed to export parameters. Check the log for details.";
                    }
                }
                finally
                {
                    IsRefreshing = false;
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export error: {ex.Message}";
            IsRefreshing = false;
        }
    }

    private static Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }
        return null;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            UnsubscribeFromParameterChanges();
            _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
            _parameterService.ParameterDownloadStarted -= OnDownloadStarted;
            _parameterService.ParameterDownloadCompleted -= OnDownloadCompleted;
            _parameterService.ParameterDownloadProgressChanged -= OnProgressChanged;
        }
        base.Dispose(disposing);
    }
}
