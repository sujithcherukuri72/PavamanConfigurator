using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PavamanDroneConfigurator.UI.ViewModels;

/// <summary>
/// ViewModel for managing and displaying parameter metadata.
/// Follows MVVM pattern with reactive properties and commands.
/// Provides UI-specific logic for metadata browsing, filtering, and display.
/// </summary>
public partial class ParameterMetadataViewModel : ObservableObject
{
    private readonly ILogger<ParameterMetadataViewModel> _logger;
    private readonly IParameterMetadataService _metadataService;

    [ObservableProperty]
    private ObservableCollection<ParameterMetadata> _allMetadata = new();

    [ObservableProperty]
    private ObservableCollection<ParameterMetadata> _filteredMetadata = new();

    [ObservableProperty]
    private ObservableCollection<string> _groups = new();

    [ObservableProperty]
    private ParameterMetadata? _selectedMetadata;

    [ObservableProperty]
    private string _selectedGroup = "All";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _totalParameters;

    [ObservableProperty]
    private int _parametersWithOptions;

    [ObservableProperty]
    private int _parametersWithRanges;

    [ObservableProperty]
    private int _totalGroups;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ParameterMetadataViewModel(
        ILogger<ParameterMetadataViewModel> logger,
        IParameterMetadataService metadataService)
    {
        _logger = logger;
        _metadataService = metadataService;
    }

    /// <summary>
    /// Loads all metadata and initializes the view.
    /// </summary>
    [RelayCommand]
    public async Task LoadMetadataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading metadata...";

            await Task.Run(() =>
            {
                // Load all metadata
                var metadata = _metadataService.GetAllMetadata().ToList();
                AllMetadata = new ObservableCollection<ParameterMetadata>(metadata);
                FilteredMetadata = new ObservableCollection<ParameterMetadata>(metadata);

                // Load groups
                var groups = _metadataService.GetGroups().ToList();
                groups.Insert(0, "All");
                Groups = new ObservableCollection<string>(groups);

                // Load statistics
                var stats = _metadataService.GetStatistics();
                TotalParameters = stats.TotalParameters;
                ParametersWithOptions = stats.ParametersWithOptions;
                ParametersWithRanges = stats.ParametersWithRanges;
                TotalGroups = stats.TotalGroups;
            });

            StatusMessage = $"Loaded {TotalParameters} parameters in {TotalGroups} groups";
            _logger.LogInformation("Loaded {Count} parameter metadata entries", TotalParameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading metadata");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Filters metadata by group when selection changes.
    /// </summary>
    partial void OnSelectedGroupChanged(string value)
    {
        ApplyFilters();
    }

    /// <summary>
    /// Filters metadata when search text changes.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        ApplyFilters();
    }

    /// <summary>
    /// Applies current filters to the metadata list.
    /// </summary>
    [RelayCommand]
    public void ApplyFilters()
    {
        try
        {
            var filtered = AllMetadata.AsEnumerable();

            // Filter by group
            if (SelectedGroup != "All")
            {
                filtered = filtered.Where(m => 
                    string.Equals(m.Group, SelectedGroup, StringComparison.OrdinalIgnoreCase));
            }

            // Filter by search text
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLowerInvariant();
                filtered = filtered.Where(m =>
                    m.Name.ToLowerInvariant().Contains(searchLower) ||
                    m.DisplayName.ToLowerInvariant().Contains(searchLower) ||
                    (m.Description?.ToLowerInvariant().Contains(searchLower) ?? false));
            }

            FilteredMetadata = new ObservableCollection<ParameterMetadata>(filtered.OrderBy(m => m.Name));
            StatusMessage = $"Showing {FilteredMetadata.Count} of {TotalParameters} parameters";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying filters");
            StatusMessage = $"Filter error: {ex.Message}";
        }
    }

    /// <summary>
    /// Clears all filters and search.
    /// </summary>
    [RelayCommand]
    public void ClearFilters()
    {
        SelectedGroup = "All";
        SearchText = string.Empty;
        ApplyFilters();
    }

    /// <summary>
    /// Exports metadata to a readable format.
    /// </summary>
    [RelayCommand]
    public async Task ExportMetadataAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Exporting metadata...";

            await Task.Run(() =>
            {
                // Export logic can be implemented here
                // For now, just log
                _logger.LogInformation("Exporting {Count} parameters", FilteredMetadata.Count);
            });

            StatusMessage = "Export completed";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting metadata");
            StatusMessage = $"Export error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Gets detailed information about a specific parameter.
    /// </summary>
    [RelayCommand]
    public void ShowParameterDetails(ParameterMetadata? metadata)
    {
        if (metadata == null) return;

        try
        {
            SelectedMetadata = metadata;
            _logger.LogInformation("Showing details for parameter {Name}", metadata.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing parameter details");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets a formatted string showing parameter options.
    /// </summary>
    public string GetOptionsDisplay(ParameterMetadata metadata)
    {
        if (metadata.Values == null || metadata.Values.Count == 0)
        {
            if (metadata.MinValue.HasValue && metadata.MaxValue.HasValue)
            {
                return $"Range: {metadata.MinValue:G} to {metadata.MaxValue:G}";
            }
            return "No constraints";
        }

        var options = string.Join(", ", 
            metadata.Values
                .OrderBy(kvp => kvp.Key)
                .Take(3)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}"));

        if (metadata.Values.Count > 3)
        {
            options += $" ... ({metadata.Values.Count} total)";
        }

        return options;
    }

    /// <summary>
    /// Validates if the ViewModel is properly initialized.
    /// </summary>
    public bool IsInitialized => TotalParameters > 0;
}
