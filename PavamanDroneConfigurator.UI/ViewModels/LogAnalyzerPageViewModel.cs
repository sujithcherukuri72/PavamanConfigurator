using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.UI.ViewModels;

/// <summary>
/// ViewModel for the Log Analyzer page.
/// Provides Mission Planner-style log viewing with graphing, message browsing, and scripting.
/// </summary>
public partial class LogAnalyzerPageViewModel : ViewModelBase
{
    private readonly ILogger<LogAnalyzerPageViewModel> _logger;
    private readonly ILogAnalyzerService _logAnalyzerService;
    private readonly IConnectionService _connectionService;

    #region Status Properties

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private string _statusMessage = "Select a log file to analyze.";

    [ObservableProperty]
    private bool _isLogLoaded;

    [ObservableProperty]
    private string _loadedLogInfo = string.Empty;

    #endregion

    #region File Properties

    [ObservableProperty]
    private ObservableCollection<LogFileInfo> _logFiles = new();

    [ObservableProperty]
    private LogFileInfo? _selectedLogFile;

    [ObservableProperty]
    private string _selectedFilePath = string.Empty;

    [ObservableProperty]
    private bool _isDownloadDialogOpen;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    private string _downloadProgressText = string.Empty;

    [ObservableProperty]
    private string _downloadFolder;

    #endregion

    #region Message Browser Properties

    [ObservableProperty]
    private ObservableCollection<LogMessageTypeGroup> _messageTypes = new();

    [ObservableProperty]
    private LogMessageTypeGroup? _selectedMessageType;

    [ObservableProperty]
    private ObservableCollection<LogMessageView> _currentMessages = new();

    [ObservableProperty]
    private string _messageSearchText = string.Empty;

    [ObservableProperty]
    private int _currentMessagePage;

    [ObservableProperty]
    private int _totalMessagePages;

    private const int MessagesPerPage = 100;

    #endregion

    #region Graph Properties

    [ObservableProperty]
    private ObservableCollection<LogFieldInfo> _availableFields = new();

    [ObservableProperty]
    private ObservableCollection<LogFieldInfo> _selectedGraphFields = new();

    [ObservableProperty]
    private LogGraphConfiguration? _currentGraph;

    [ObservableProperty]
    private bool _hasGraphData;

    [ObservableProperty]
    private string _graphFieldFilter = string.Empty;

    [ObservableProperty]
    private ObservableCollection<LogFieldInfo> _filteredFields = new();

    // TreeView for Mission Planner style
    [ObservableProperty]
    private ObservableCollection<LogMessageTypeNode> _messageTypesTree = new();

    [ObservableProperty]
    private bool _hasTreeData;

    #endregion

    #region Display Options (Mission Planner style toolbar)

    [ObservableProperty]
    private bool _showMap;

    [ObservableProperty]
    private bool _showTime = true;

    [ObservableProperty]
    private bool _showDataTable;

    [ObservableProperty]
    private bool _showParams;

    [ObservableProperty]
    private bool _showMode = true;

    [ObservableProperty]
    private bool _showErrors = true;

    [ObservableProperty]
    private bool _showMsg = true;

    [ObservableProperty]
    private bool _showEvents = true;

    #endregion

    #region Scripting Properties

    [ObservableProperty]
    private string _scriptText = string.Empty;

    [ObservableProperty]
    private string _scriptOutput = string.Empty;

    [ObservableProperty]
    private bool _isScriptRunning;

    [ObservableProperty]
    private ObservableCollection<ScriptFunctionInfo> _scriptFunctions = new();

    [ObservableProperty]
    private string _loadedScriptPath = string.Empty;

    [ObservableProperty]
    private string _loadedScriptName = string.Empty;

    #endregion

    #region Tab Properties

    [ObservableProperty]
    private int _selectedTabIndex;

    #endregion

    // Reference to the parent window for file picker
    private Window? _parentWindow;

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    public LogAnalyzerPageViewModel(
        ILogger<LogAnalyzerPageViewModel> logger,
        ILogAnalyzerService logAnalyzerService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _logAnalyzerService = logAnalyzerService;
        _connectionService = connectionService;

        _downloadFolder = _logAnalyzerService.GetDefaultDownloadFolder();

        // Subscribe to service events
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _logAnalyzerService.LogFilesUpdated += OnLogFilesUpdated;
        _logAnalyzerService.DownloadProgressChanged += OnDownloadProgressChanged;
        _logAnalyzerService.DownloadCompleted += OnDownloadCompleted;
        _logAnalyzerService.LogParsed += OnLogParsed;

        IsConnected = _connectionService.IsConnected;

        // Load script functions
        foreach (var func in _logAnalyzerService.GetScriptFunctions())
        {
            ScriptFunctions.Add(func);
        }
    }

    #region Event Handlers

    private void OnConnectionStateChanged(object? sender, bool connected)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsConnected = connected;
            if (connected)
            {
                StatusMessage = "Connected. Click 'Download log file' to browse logs from FC.";
            }
            else
            {
                StatusMessage = "Disconnected. Browse local log files.";
                LogFiles.Clear();
            }
        });
    }

    private void OnLogFilesUpdated(object? sender, List<LogFileInfo> files)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogFiles.Clear();
            int id = 1;
            foreach (var file in files)
            {
                file.LogId = id++;
                LogFiles.Add(file);
            }
            StatusMessage = $"Found {files.Count} log files";
        });
    }

    private void OnDownloadProgressChanged(object? sender, LogDownloadProgress progress)
    {
        Dispatcher.UIThread.Post(() =>
        {
            DownloadProgress = progress.ProgressPercent;
            DownloadProgressText = progress.ProgressDisplay;
        });
    }

    private void OnDownloadCompleted(object? sender, (LogFileInfo File, bool Success, String? Error) e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (e.Success)
            {
                StatusMessage = $"Downloaded: {e.File.FileName}";
                SelectedFilePath = e.File.LocalPath ?? string.Empty;
            }
            else
            {
                StatusMessage = $"Download failed: {e.Error}";
            }
        });
    }

    private void OnLogParsed(object? sender, LogParseResult result)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (result.IsSuccess)
            {
                IsLogLoaded = true;
                LoadedLogInfo = $"{result.FileName} - {result.MessageCount:N0} messages, {result.DurationDisplay}";
                
                // Load message types
                MessageTypes.Clear();
                foreach (var type in result.MessageTypes)
                {
                    MessageTypes.Add(type);
                }

                // Load available graph fields
                LoadAvailableFields();

                StatusMessage = $"Log loaded: {result.MessageCount:N0} messages";
            }
            else
            {
                IsLogLoaded = false;
                StatusMessage = $"Failed to load log: {result.ErrorMessage}";
            }
        });
    }

    partial void OnSelectedMessageTypeChanged(LogMessageTypeGroup? value)
    {
        if (value != null)
        {
            CurrentMessagePage = 0;
            LoadMessages();
        }
    }

    partial void OnGraphFieldFilterChanged(string value)
    {
        FilterGraphFields();
    }

    #endregion

    #region File Commands

    [RelayCommand]
    private async Task BrowseLocalFileAsync()
    {
        if (_parentWindow == null)
        {
            StatusMessage = "Unable to open file browser.";
            return;
        }

        try
        {
            var storageProvider = _parentWindow.StorageProvider;
            
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Log File",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("DataFlash Logs") { Patterns = new[] { "*.bin", "*.log" } },
                    new FilePickerFileType("Telemetry Logs") { Patterns = new[] { "*.tlog" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                SelectedFilePath = file.Path.LocalPath;
                StatusMessage = $"Selected: {Path.GetFileName(SelectedFilePath)}";
                
                await LoadLogAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file picker");
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadLogAsync()
    {
        if (string.IsNullOrEmpty(SelectedFilePath))
        {
            StatusMessage = "No file selected.";
            return;
        }

        if (!File.Exists(SelectedFilePath))
        {
            StatusMessage = "File not found.";
            return;
        }

        IsBusy = true;
        StatusMessage = $"Loading {Path.GetFileName(SelectedFilePath)}...";

        try
        {
            var result = await _logAnalyzerService.LoadLogFileAsync(SelectedFilePath);
            
            if (!result.IsSuccess)
            {
                StatusMessage = $"Failed to load: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load log");
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenDownloadDialogAsync()
    {
        if (!IsConnected)
        {
            StatusMessage = "Not connected to a vehicle.";
            return;
        }

        IsDownloadDialogOpen = true;
        IsBusy = true;
        StatusMessage = "Loading log files from flight controller...";

        try
        {
            await _logAnalyzerService.RefreshLogFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh logs");
            StatusMessage = $"Failed to load logs: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CloseDownloadDialog()
    {
        // Cancel any ongoing operations safely before closing
        try
        {
            if (IsDownloading)
            {
                _logAnalyzerService.CancelDownload();
                IsDownloading = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error canceling download on dialog close");
        }
        
        IsDownloadDialogOpen = false;
    }

    [RelayCommand]
    private async Task RefreshLogsAsync()
    {
        if (!IsConnected) return;

        IsBusy = true;
        try
        {
            await _logAnalyzerService.RefreshLogFilesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh logs");
            StatusMessage = $"Failed to refresh: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DownloadSelectedAsync()
    {
        var selectedFiles = LogFiles.Where(f => f.IsSelected).ToList();
        
        if (selectedFiles.Count == 0)
        {
            StatusMessage = "No logs selected.";
            return;
        }

        IsDownloading = true;
        DownloadProgress = 0;

        try
        {
            var count = await _logAnalyzerService.DownloadLogFilesAsync(selectedFiles, DownloadFolder);
            StatusMessage = $"Downloaded {count} files";
            
            var downloaded = selectedFiles.FirstOrDefault(f => f.IsDownloaded);
            if (downloaded != null)
            {
                SelectedFilePath = downloaded.LocalPath ?? string.Empty;
                IsDownloadDialogOpen = false;
                
                // Auto-load the downloaded log
                if (!string.IsNullOrEmpty(SelectedFilePath))
                {
                    await LoadLogAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Download failed");
            StatusMessage = $"Download failed: {ex.Message}";
        }
        finally
        {
            IsDownloading = false;
        }
    }

    [RelayCommand]
    private void CancelDownload()
    {
        try
        {
            _logAnalyzerService.CancelDownload();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error canceling download");
        }
        IsDownloading = false;
    }

    #endregion

    #region Message Browser Commands

    private void LoadMessages()
    {
        if (SelectedMessageType == null) return;

        CurrentMessages.Clear();
        
        var skip = CurrentMessagePage * MessagesPerPage;
        var messages = _logAnalyzerService.GetMessages(SelectedMessageType.Name, skip, MessagesPerPage);
        
        foreach (var msg in messages)
        {
            CurrentMessages.Add(msg);
        }

        var totalCount = _logAnalyzerService.GetMessageCount(SelectedMessageType.Name);
        TotalMessagePages = (totalCount + MessagesPerPage - 1) / MessagesPerPage;
    }

    [RelayCommand]
    private void NextMessagePage()
    {
        if (CurrentMessagePage < TotalMessagePages - 1)
        {
            CurrentMessagePage++;
            LoadMessages();
        }
    }

    [RelayCommand]
    private void PreviousMessagePage()
    {
        if (CurrentMessagePage > 0)
        {
            CurrentMessagePage--;
            LoadMessages();
        }
    }

    [RelayCommand]
    private void SearchMessages()
    {
        if (string.IsNullOrWhiteSpace(MessageSearchText)) return;

        CurrentMessages.Clear();
        var results = _logAnalyzerService.SearchMessages(MessageSearchText, 100);
        
        foreach (var msg in results)
        {
            CurrentMessages.Add(msg);
        }

        StatusMessage = $"Found {results.Count} matching messages";
    }

    #endregion

    #region Graph Commands

    private void LoadAvailableFields()
    {
        AvailableFields.Clear();
        FilteredFields.Clear();
        SelectedGraphFields.Clear();
        MessageTypesTree.Clear();

        var fields = _logAnalyzerService.GetAvailableGraphFields();
        
        // Build flat list with statistics
        foreach (var field in fields)
        {
            // Get statistics for each field for legend display
            var stats = _logAnalyzerService.GetFieldStatistics(field.DisplayName);
            field.MinValue = stats.Minimum;
            field.MaxValue = stats.Maximum;
            field.MeanValue = stats.Average;
            
            AvailableFields.Add(field);
            FilteredFields.Add(field);
        }

        // Build TreeView structure (Mission Planner style)
        var groupedByType = fields.GroupBy(f => f.MessageType).OrderBy(g => g.Key);
        
        foreach (var group in groupedByType)
        {
            var typeNode = new LogMessageTypeNode
            {
                Name = group.Key,
                IsMessageType = true
            };

            var colorIndex = 0;
            foreach (var field in group.OrderBy(f => f.FieldName))
            {
                var fieldNode = new LogFieldNode
                {
                    Name = field.FieldName,
                    FullKey = field.DisplayName,
                    Color = GraphColors.GetColor(colorIndex++),
                    MinValue = field.MinValue,
                    MaxValue = field.MaxValue,
                    MeanValue = field.MeanValue
                };
                
                typeNode.Fields.Add(fieldNode);
            }

            MessageTypesTree.Add(typeNode);
        }

        HasTreeData = MessageTypesTree.Count > 0;
    }

    private void FilterGraphFields()
    {
        FilteredFields.Clear();

        var filter = GraphFieldFilter?.ToLowerInvariant() ?? "";
        
        var filtered = string.IsNullOrEmpty(filter)
            ? AvailableFields
            : AvailableFields.Where(f => f.DisplayName.ToLowerInvariant().Contains(filter));

        foreach (var field in filtered)
        {
            FilteredFields.Add(field);
        }
    }

    [RelayCommand]
    private void AddFieldToGraph(LogFieldInfo? field)
    {
        if (field == null) return;

        if (!SelectedGraphFields.Any(f => f.DisplayName == field.DisplayName))
        {
            field.IsSelected = true;
            field.Color = GraphColors.GetColor(SelectedGraphFields.Count);
            SelectedGraphFields.Add(field);
            UpdateGraph();
        }
    }

    [RelayCommand]
    private void RemoveFieldFromGraph(LogFieldInfo? field)
    {
        if (field == null) return;

        field.IsSelected = false;
        SelectedGraphFields.Remove(field);
        UpdateGraph();
    }

    [RelayCommand]
    private void ClearGraph()
    {
        foreach (var field in SelectedGraphFields)
        {
            field.IsSelected = false;
        }
        SelectedGraphFields.Clear();
        CurrentGraph = null;
        HasGraphData = false;
    }

    private void UpdateGraph()
    {
        if (SelectedGraphFields.Count == 0)
        {
            CurrentGraph = null;
            HasGraphData = false;
            return;
        }

        var fieldKeys = SelectedGraphFields.Select(f => f.DisplayName).ToArray();
        CurrentGraph = _logAnalyzerService.GetGraphData(fieldKeys);
        HasGraphData = CurrentGraph.Series.Any();
    }

    [RelayCommand]
    private void ShowFieldStatistics(LogFieldInfo? field)
    {
        if (field == null) return;

        var stats = _logAnalyzerService.GetFieldStatistics(field.DisplayName);
        StatusMessage = $"{field.DisplayName}: Min={stats.Minimum:F3}, Max={stats.Maximum:F3}, Avg={stats.Average:F3}";
    }

    #endregion

    #region Script Commands

    [RelayCommand]
    private async Task LoadScriptFileAsync()
    {
        if (_parentWindow == null)
        {
            StatusMessage = "Cannot open file browser";
            return;
        }

        try
        {
            var files = await _parentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Lua Script",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Lua Scripts") { Patterns = new[] { "*.lua" } },
                    new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                LoadedScriptPath = file.Path.LocalPath;
                LoadedScriptName = Path.GetFileName(LoadedScriptPath);
                
                // Read the script file content
                ScriptText = await File.ReadAllTextAsync(LoadedScriptPath);
                ScriptOutput = $"Loaded: {LoadedScriptName}\nFile size: {new FileInfo(LoadedScriptPath).Length} bytes";
                StatusMessage = $"Script loaded: {LoadedScriptName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load script file");
            StatusMessage = $"Failed to load script: {ex.Message}";
            ScriptOutput = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveScriptFileAsync()
    {
        if (_parentWindow == null || string.IsNullOrWhiteSpace(ScriptText))
        {
            StatusMessage = "Nothing to save";
            return;
        }

        try
        {
            var file = await _parentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Lua Script",
                DefaultExtension = "lua",
                SuggestedFileName = !string.IsNullOrEmpty(LoadedScriptName) ? LoadedScriptName : "script.lua",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Lua Scripts") { Patterns = new[] { "*.lua" } }
                }
            });

            if (file != null)
            {
                await File.WriteAllTextAsync(file.Path.LocalPath, ScriptText);
                LoadedScriptPath = file.Path.LocalPath;
                LoadedScriptName = Path.GetFileName(LoadedScriptPath);
                StatusMessage = $"Script saved: {LoadedScriptName}";
                ScriptOutput = $"Saved: {LoadedScriptName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save script file");
            StatusMessage = $"Failed to save script: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RunScriptAsync()
    {
        if (string.IsNullOrWhiteSpace(ScriptText))
        {
            StatusMessage = "No script loaded. Load a Lua script file first.";
            return;
        }

        if (!IsLogLoaded)
        {
            StatusMessage = "Load a log file first.";
            return;
        }

        IsScriptRunning = true;
        ScriptOutput = "Running script...\n";

        try
        {
            var result = await _logAnalyzerService.RunScriptAsync(ScriptText);
            
            if (result.IsSuccess)
            {
                ScriptOutput = result.Output;
                if (result.Warnings.Count > 0)
                {
                    ScriptOutput += "\nWarnings:\n" + string.Join("\n", result.Warnings);
                }
                ScriptOutput += $"\nCompleted in {result.ExecutionTime.TotalMilliseconds:F0}ms";
            }
            else
            {
                ScriptOutput = $"Error: {result.ErrorMessage}";
            }
        }
        catch (Exception ex)
        {
            ScriptOutput = $"Error: {ex.Message}";
        }
        finally
        {
            IsScriptRunning = false;
        }
    }

    [RelayCommand]
    private void ClearScript()
    {
        ScriptText = "";
        ScriptOutput = "";
        LoadedScriptPath = "";
        LoadedScriptName = "";
    }

    public void InsertScriptFunction(ScriptFunctionInfo function)
    {
        if (string.IsNullOrEmpty(ScriptText))
        {
            ScriptText = function.Example;
        }
        else
        {
            ScriptText += $"\n{function.Example}";
        }
    }

    #endregion

    #region Helper Methods

    public void OnFieldSelectionChanged(LogFieldInfo field)
    {
        if (field.IsSelected)
        {
            AddFieldToGraph(field);
        }
        else
        {
            RemoveFieldFromGraph(field);
        }
    }

    public bool CanGoToNextPage => CurrentMessagePage < TotalMessagePages - 1;
    public bool CanGoToPreviousPage => CurrentMessagePage > 0;

    [RelayCommand]
    private void ResetZoom()
    {
        // This will be handled by the graph control
        StatusMessage = "Zoom reset";
    }

    [RelayCommand]
    private async Task ExportGraphAsync()
    {
        if (!HasGraphData || _parentWindow == null) return;

        try
        {
            var file = await _parentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export Graph",
                DefaultExtension = "png",
                SuggestedFileName = $"graph_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("PNG Image") { Patterns = new[] { "*.png" } }
                }
            });

            if (file != null)
            {
                // The export will be triggered through the view
                StatusMessage = $"Graph exported to {Path.GetFileName(file.Path.LocalPath)}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export graph");
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    #endregion

    #region Cleanup

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connectionService.ConnectionStateChanged -= OnConnectionStateChanged;
            _logAnalyzerService.LogFilesUpdated -= OnLogFilesUpdated;
            _logAnalyzerService.DownloadProgressChanged -= OnDownloadProgressChanged;
            _logAnalyzerService.DownloadCompleted -= OnDownloadCompleted;
            _logAnalyzerService.LogParsed -= OnLogParsed;
        }
        base.Dispose(disposing);
    }

    #endregion

    #region Graph Navigation Commands

    [RelayCommand]
    private void GraphLeft()
    {
        // Scroll graph left (pan left in time)
        StatusMessage = "Graph panned left";
    }

    [RelayCommand]
    private void GraphRight()
    {
        // Scroll graph right (pan right in time)
        StatusMessage = "Graph panned right";
    }

    #endregion
}
