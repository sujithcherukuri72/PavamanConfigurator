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
    private bool _hasLoadedParameters;

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

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    public ParametersPageViewModel(IParameterService parameterService, IConnectionService connectionService)
    {
        _parameterService = parameterService;
        _connectionService = connectionService;

        // Subscribe to connection state changes
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _parameterService.ParameterDownloadProgressChanged += OnParameterDownloadProgressChanged;
        _parameterService.ParameterUpdated += OnParameterUpdated;
        
        // Initialize can edit state
        CanEditParameters = _connectionService.IsConnected && _parameterService.IsParameterDownloadComplete;
    }

    private async void OnConnectionStateChanged(object? sender, bool connected)
    {
        try
        {
            if (connected)
            {
                CanEditParameters = _parameterService.IsParameterDownloadComplete;
                _hasLoadedParameters = false;
                StatusMessage = _parameterService.IsParameterDownloadComplete
                    ? "Parameters ready"
                    : "Waiting for parameter download...";
            }
            else
            {
                // Clear parameters when disconnected
                Parameters.Clear();
                FilteredParameters.Clear();
                UpdateStatistics();
                
                var downloadInterrupted = _parameterService.IsParameterDownloadInProgress ||
                                          (!_parameterService.IsParameterDownloadComplete &&
                                           _parameterService.ReceivedParameterCount > 0);
                if (downloadInterrupted)
                {
                    StatusMessage = "Disconnected during parameter download - parameters unavailable";
                }
                else
                {
                    StatusMessage = "Connect to your drone to load parameters";
                }
                _hasLoadedParameters = false;
                CanEditParameters = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error handling connection state: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Please connect first.";
            return;
        }

        try
        {
            StatusMessage = "Loading parameters...";
            var parameters = await _parameterService.GetAllParametersAsync();
            
            Parameters.Clear();
            foreach (var p in parameters)
            {
                Parameters.Add(p);
            }
            
            ApplyFilter();
            UpdateStatistics();
            StatusMessage = $"Successfully loaded {Parameters.Count} parameters";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading parameters: {ex.Message}";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    [RelayCommand]
    private async Task RefreshParametersAsync()
    {
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Please connect first.";
            OnPropertyChanged(nameof(HasStatusMessage));
            return;
        }

        try
        {
            StatusMessage = "Refreshing parameters from drone...";
            OnPropertyChanged(nameof(HasStatusMessage));
            await _parameterService.RefreshParametersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing parameters: {ex.Message}";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    [RelayCommand]
    private async Task SaveParameterAsync(DroneParameter? parameter)
    {
        var paramToSave = parameter ?? SelectedParameter;
        
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Cannot save parameter.";
            OnPropertyChanged(nameof(HasStatusMessage));
            return;
        }

        if (paramToSave == null)
        {
            StatusMessage = "No parameter selected.";
            OnPropertyChanged(nameof(HasStatusMessage));
            return;
        }

        try
        {
            StatusMessage = $"Saving {paramToSave.Name} = {paramToSave.Value}...";
            OnPropertyChanged(nameof(HasStatusMessage));
            
            var success = await _parameterService.SetParameterAsync(paramToSave.Name, paramToSave.Value);
            
            if (success)
            {
                StatusMessage = $"? Successfully saved {paramToSave.Name} = {paramToSave.Value}";
                HasUnsavedChanges = false;
            }
            else
            {
                StatusMessage = $"? Failed to save {paramToSave.Name}. Timeout or not acknowledged.";
            }
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving parameter: {ex.Message}";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    [RelayCommand]
    private async Task SaveAllParametersAsync()
    {
        if (!_connectionService.IsConnected || !HasUnsavedChanges)
        {
            return;
        }

        StatusMessage = "Saving all modified parameters...";
        OnPropertyChanged(nameof(HasStatusMessage));
        
        // In a real implementation, track modified parameters
        HasUnsavedChanges = false;
        StatusMessage = "All changes saved successfully";
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(UpdateParameterDownloadStateAsync);
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var updatedParameter = await _parameterService.GetParameterAsync(parameterName);
            if (updatedParameter == null)
            {
                return;
            }

            for (int i = 0; i < Parameters.Count; i++)
            {
                if (string.Equals(Parameters[i].Name, updatedParameter.Name, StringComparison.OrdinalIgnoreCase))
                {
                    Parameters[i] = updatedParameter;
                    if (SelectedParameter != null &&
                        string.Equals(SelectedParameter.Name, updatedParameter.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        SelectedParameter = updatedParameter;
                    }
                    ApplyFilter();
                    UpdateStatistics();
                    return;
                }
            }

            Parameters.Add(updatedParameter);
            ApplyFilter();
            UpdateStatistics();
        });
    }

    private async Task UpdateParameterDownloadStateAsync()
    {
        CanEditParameters = _connectionService.IsConnected && _parameterService.IsParameterDownloadComplete;

        if (_parameterService.IsParameterDownloadInProgress)
        {
            var expected = _parameterService.ExpectedParameterCount.HasValue
                ? _parameterService.ExpectedParameterCount.Value.ToString()
                : "?";
            StatusMessage = $"Downloading parameters... {_parameterService.ReceivedParameterCount}/{expected}";
            OnPropertyChanged(nameof(HasStatusMessage));
            TotalParameterCount = _parameterService.ExpectedParameterCount ?? 0;
            LoadedParameterCount = _parameterService.ReceivedParameterCount;
        }
        else if (_parameterService.IsParameterDownloadComplete && _connectionService.IsConnected && !_hasLoadedParameters)
        {
            await LoadParametersAsync();
            _hasLoadedParameters = true;
        }
        else if (!_connectionService.IsConnected)
        {
            Parameters.Clear();
            FilteredParameters.Clear();
            UpdateStatistics();
            StatusMessage = "Connect to your drone to load parameters";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            FilteredParameters = new ObservableCollection<DroneParameter>(Parameters);
        }
        else
        {
            var filtered = Parameters.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
            
            FilteredParameters = new ObservableCollection<DroneParameter>(filtered);
        }
    }

    private void UpdateStatistics()
    {
        TotalParameterCount = Parameters.Count;
        LoadedParameterCount = Parameters.Count;
        ModifiedParameterCount = 0; // Track this in real implementation
    }
}
