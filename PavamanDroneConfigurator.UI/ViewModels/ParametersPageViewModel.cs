using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.UI.ViewModels;

public partial class ParametersPageViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;
    
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

    public ParametersPageViewModel(IParameterService parameterService, IConnectionService connectionService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;

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
    private Task ImportParametersAsync()
    {
        StatusMessage = "Import parameters feature - coming soon";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task ExportParametersAsync()
    {
        StatusMessage = "Export parameters feature - coming soon";
        return Task.CompletedTask;
    }

    partial void OnSearchTextChanged(string value)
    {
        FilteredParameters.Clear();
        
        var filtered = string.IsNullOrWhiteSpace(value)
            ? Parameters
            : Parameters.Where(p => 
                p.Name.Contains(value, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false));
        
        foreach (var p in filtered)
        {
            FilteredParameters.Add(p);
        }
        
        LoadedParameterCount = FilteredParameters.Count;
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
