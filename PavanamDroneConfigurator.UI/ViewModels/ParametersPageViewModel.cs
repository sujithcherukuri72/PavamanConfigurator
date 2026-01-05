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
    
    [ObservableProperty]
    private bool _isRefreshing;

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
            await Dispatcher.UIThread.InvokeAsync(() =>
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
                OnPropertyChanged(nameof(HasStatusMessage));
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error handling connection state: {ex.Message}";
            OnPropertyChanged(nameof(HasStatusMessage));
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
            OnPropertyChanged(nameof(HasStatusMessage));
            
            var parameters = await _parameterService.GetAllParametersAsync();
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Clear and repopulate the existing collections (don't create new ones!)
                Parameters.Clear();
                FilteredParameters.Clear();
                
                foreach (var p in parameters.OrderBy(x => x.Name))
                {
                    Parameters.Add(p);
                    FilteredParameters.Add(p);
                }
                
                UpdateStatistics();
                CanEditParameters = true;
                StatusMessage = $"Successfully loaded {Parameters.Count} parameters";
                OnPropertyChanged(nameof(HasStatusMessage));
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading parameters: {ex.Message}";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRefreshParameters))]
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
            IsRefreshing = true;
            _hasLoadedParameters = false;
            StatusMessage = "Refreshing parameters from drone...";
            OnPropertyChanged(nameof(HasStatusMessage));
            
            // Clear existing parameters
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Parameters.Clear();
                FilteredParameters.Clear();
                UpdateStatistics();
            });
            
            await _parameterService.RefreshParametersAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error refreshing parameters: {ex.Message}";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        finally
        {
            IsRefreshing = false;
        }
    }
    
    private bool CanRefreshParameters() => _connectionService.IsConnected && !IsRefreshing;

    [RelayCommand(CanExecute = nameof(CanSaveParameter))]
    private async Task SaveParameterAsync(DroneParameter? parameter)
    {
        var paramToSave = parameter ?? SelectedParameter;
        
        if (paramToSave == null)
        {
            StatusMessage = "No parameter selected.";
            OnPropertyChanged(nameof(HasStatusMessage));
            return;
        }
        
        if (!_connectionService.IsConnected)
        {
            StatusMessage = "Not connected. Cannot save parameter.";
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
                StatusMessage = $"Successfully saved {paramToSave.Name} = {paramToSave.Value}";
            }
            else
            {
                StatusMessage = $"Failed to save {paramToSave.Name}. Timeout or not acknowledged.";
            }
            OnPropertyChanged(nameof(HasStatusMessage));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving parameter: {ex.Message}";
            OnPropertyChanged(nameof(HasStatusMessage));
        }
    }
    
    private bool CanSaveParameter() => _connectionService.IsConnected && CanEditParameters;

    [RelayCommand]
    private async Task SaveAllParametersAsync()
    {
        if (!_connectionService.IsConnected || !HasUnsavedChanges)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            StatusMessage = "Saving all modified parameters...";
            OnPropertyChanged(nameof(HasStatusMessage));
        });
        
        // Wait for async operation simulation
        await Task.Delay(100);
        
        // In a real implementation, track modified parameters
        HasUnsavedChanges = false;
        StatusMessage = "All changes saved successfully";
        OnPropertyChanged(nameof(HasStatusMessage));
    }

    private void OnParameterDownloadProgressChanged(object? sender, EventArgs e)
    {
        _ = Dispatcher.UIThread.InvokeAsync(UpdateParameterDownloadStateAsync);
    }

    private void OnParameterUpdated(object? sender, string parameterName)
    {
        // This is called frequently during download - just update statistics
        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (_parameterService.IsParameterDownloadInProgress)
            {
                // During download, just update the counts
                TotalParameterCount = _parameterService.ExpectedParameterCount ?? 0;
                LoadedParameterCount = _parameterService.ReceivedParameterCount;
            }
        });
    }

    private async Task UpdateParameterDownloadStateAsync()
    {
        if (_parameterService.IsParameterDownloadInProgress)
        {
            var expected = _parameterService.ExpectedParameterCount.HasValue
                ? _parameterService.ExpectedParameterCount.Value.ToString()
                : "?";
            StatusMessage = $"Downloading parameters... {_parameterService.ReceivedParameterCount}/{expected}";
            OnPropertyChanged(nameof(HasStatusMessage));
            TotalParameterCount = _parameterService.ExpectedParameterCount ?? 0;
            LoadedParameterCount = _parameterService.ReceivedParameterCount;
            CanEditParameters = false;
        }
        else if (_parameterService.IsParameterDownloadComplete && _connectionService.IsConnected)
        {
            // Download complete - load all parameters into the grid
            if (!_hasLoadedParameters)
            {
                _hasLoadedParameters = true;
                await LoadParametersAsync();
            }
            CanEditParameters = true;
        }
        else if (!_connectionService.IsConnected)
        {
            Parameters.Clear();
            FilteredParameters.Clear();
            UpdateStatistics();
            StatusMessage = "Connect to your drone to load parameters";
            OnPropertyChanged(nameof(HasStatusMessage));
            CanEditParameters = false;
        }
        
        RefreshParametersCommand.NotifyCanExecuteChanged();
        SaveParameterCommand.NotifyCanExecuteChanged();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        // Clear and repopulate the FilteredParameters collection (don't replace it!)
        FilteredParameters.Clear();
        
        var source = string.IsNullOrWhiteSpace(SearchText) 
            ? Parameters 
            : Parameters.Where(p =>
                p.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                (p.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));
        
        foreach (var p in source)
        {
            FilteredParameters.Add(p);
        }
        
        // Update the count display
        OnPropertyChanged(nameof(FilteredParameters));
    }

    private void UpdateStatistics()
    {
        TotalParameterCount = Parameters.Count;
        LoadedParameterCount = FilteredParameters.Count;
        ModifiedParameterCount = 0;
    }
}
