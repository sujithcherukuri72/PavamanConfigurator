using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PavanamDroneConfigurator.Core.Interfaces;
using PavanamDroneConfigurator.Core.Models;

namespace PavanamDroneConfigurator.UI.ViewModels;

public partial class ParametersPageViewModel : ViewModelBase
{
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

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
                Parameters.Clear();
                FilteredParameters.Clear();
                TotalParameterCount = 0;
                LoadedParameterCount = 0;
                CanEditParameters = false;
                IsRefreshing = false;
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
            Parameters.Clear();
            FilteredParameters.Clear();
            TotalParameterCount = 0;
            LoadedParameterCount = 0;
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
                // AUTO-LOAD parameters into the grid immediately after download completes
                await LoadParametersIntoGridAsync();
                CanEditParameters = true;
                StatusMessage = $"Successfully loaded {Parameters.Count} parameters from drone";
            }
            else
            {
                StatusMessage = "No parameters received from drone";
                CanEditParameters = false;
            }
        });
    }

    private void OnProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
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

    /// <summary>
    /// Loads all parameters from the service into the UI grid
    /// </summary>
    private async Task LoadParametersIntoGridAsync()
    {
        try
        {
            // Get all parameters from the service
            var allParams = await _parameterService.GetAllParametersAsync();
            
            // Clear and populate the collections
            Parameters.Clear();
            FilteredParameters.Clear();
            
            foreach (var p in allParams)
            {
                Parameters.Add(p);
                FilteredParameters.Add(p);
            }
            
            // Update statistics
            TotalParameterCount = Parameters.Count;
            LoadedParameterCount = Parameters.Count;
            
            // Force UI update
            OnPropertyChanged(nameof(Parameters));
            OnPropertyChanged(nameof(FilteredParameters));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading parameters: {ex.Message}";
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

        // This will trigger OnDownloadStarted, then OnDownloadCompleted which auto-loads the grid
        await _parameterService.RefreshParametersAsync();
    }

    [RelayCommand]
    private async Task SaveParameterAsync(DroneParameter? parameter)
    {
        var p = parameter ?? SelectedParameter;
        if (p == null)
        {
            StatusMessage = "No parameter selected";
            return;
        }

        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected";
            return;
        }

        StatusMessage = $"Saving {p.Name}...";
        var success = await _parameterService.SetParameterAsync(p.Name, p.Value);
        StatusMessage = success 
            ? $"Saved {p.Name} = {p.Value}" 
            : $"Failed to save {p.Name}";
    }

    [RelayCommand]
    private Task SaveAllParametersAsync()
    {
        StatusMessage = "Save all not implemented yet";
        return Task.CompletedTask;
    }

    partial void OnSearchTextChanged(string value)
    {
        // Apply filter when search text changes
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
}
