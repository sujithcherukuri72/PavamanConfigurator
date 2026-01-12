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
    private readonly ILogQueryEngine? _queryEngine;
    private readonly ILogEventDetector? _eventDetector;
    private readonly ILogExportService? _exportService;

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

    [ObservableProperty]
    private int _loadProgress;

    #endregion

    #region Tab Properties

    [ObservableProperty]
    private int _selectedTabIndex;

    public const int TAB_OVERVIEW = 0;
    public const int TAB_PLOT = 1;
    public const int TAB_MAP = 2;
    public const int TAB_EVENTS = 3;
    public const int TAB_PARAMS = 4;

    #endregion

    #region Overview Properties

    [ObservableProperty]
    private string _logFileName = string.Empty;

    [ObservableProperty]
    private string _logFileSize = string.Empty;

    [ObservableProperty]
    private string _logDuration = string.Empty;

    [ObservableProperty]
    private string _logMessageCount = string.Empty;

    [ObservableProperty]
    private string _logMessageTypes = string.Empty;

    [ObservableProperty]
    private string _vehicleType = string.Empty;

    [ObservableProperty]
    private string _firmwareVersion = string.Empty;

    [ObservableProperty]
    private int _errorCount;

    [ObservableProperty]
    private int _warningCount;

    [ObservableProperty]
    private bool _hasGpsData;

    [ObservableProperty]
    private bool _hasAttitudeData;

    [ObservableProperty]
    private bool _hasVibeData;

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

    [ObservableProperty]
    private ObservableCollection<LogMessageTypeNode> _messageTypesTree = new();

    [ObservableProperty]
    private bool _hasTreeData;

    [ObservableProperty]
    private double _cursorTime;

    [ObservableProperty]
    private string _cursorTimeDisplay = "00:00:00.000";

    [ObservableProperty]
    private ObservableCollection<CursorReadout> _cursorReadouts = new();

    [ObservableProperty]
    private double _zoomStartTime;

    [ObservableProperty]
    private double _zoomEndTime;

    #endregion

    #region Events Properties

    [ObservableProperty]
    private ObservableCollection<LogEvent> _detectedEvents = new();

    [ObservableProperty]
    private ObservableCollection<LogEvent> _filteredEvents = new();

    [ObservableProperty]
    private LogEvent? _selectedEvent;

    [ObservableProperty]
    private bool _showInfoEvents = true;

    [ObservableProperty]
    private bool _showWarningEvents = true;

    [ObservableProperty]
    private bool _showErrorEvents = true;

    [ObservableProperty]
    private bool _showCriticalEvents = true;

    [ObservableProperty]
    private string _eventSearchText = string.Empty;

    [ObservableProperty]
    private EventSummary? _eventSummary;

    #endregion

    #region Parameters Properties

    [ObservableProperty]
    private ObservableCollection<ParameterChange> _parameterChanges = new();

    [ObservableProperty]
    private ParameterChange? _selectedParameterChange;

    [ObservableProperty]
    private string _parameterSearchText = string.Empty;

    #endregion

    #region Map Properties

    [ObservableProperty]
    private ObservableCollection<GpsPoint> _gpsTrack = new();

    [ObservableProperty]
    private GpsPoint? _currentMapPosition;

    [ObservableProperty]
    private double _mapCenterLat;

    [ObservableProperty]
    private double _mapCenterLng;

    [ObservableProperty]
    private int _mapZoom = 15;

    #endregion

    #region Display Options

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

    private Window? _parentWindow;

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
    }

    public LogAnalyzerPageViewModel(
        ILogger<LogAnalyzerPageViewModel> logger,
        ILogAnalyzerService logAnalyzerService,
        IConnectionService connectionService,
        ILogQueryEngine? queryEngine = null,
        ILogEventDetector? eventDetector = null,
        ILogExportService? exportService = null)
    {
        _logger = logger;
        _logAnalyzerService = logAnalyzerService;
        _connectionService = connectionService;
        _queryEngine = queryEngine;
        _eventDetector = eventDetector;
        _exportService = exportService;

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
        Dispatcher.UIThread.Post(async () =>
        {
            if (result.IsSuccess)
            {
                IsLogLoaded = true;
                LoadedLogInfo = $"{result.FileName} - {result.MessageCount:N0} messages, {result.DurationDisplay}";
                
                // Update overview
                UpdateOverview(result);
                
                // Load message types
                MessageTypes.Clear();
                foreach (var type in result.MessageTypes)
                {
                    MessageTypes.Add(type);
                }

                // Load available graph fields
                LoadAvailableFields();
                
                // Detect events in background
                await DetectEventsAsync();
                
                // Load GPS track for map
                LoadGpsTrack();

                StatusMessage = $"Log loaded: {result.MessageCount:N0} messages";
            }
            else
            {
                IsLogLoaded = false;
                StatusMessage = $"Failed to load log: {result.ErrorMessage}";
            }
        });
    }

    private void UpdateOverview(LogParseResult result)
    {
        LogFileName = result.FileName;
        LogFileSize = FormatFileSize(result.FileSize);
        LogDuration = result.DurationDisplay;
        LogMessageCount = result.MessageCount.ToString("N0");
        LogMessageTypes = result.MessageTypes.Count.ToString();
        
        // Check for specific data types
        HasGpsData = result.MessageTypes.Any(t => t.Name == "GPS");
        HasAttitudeData = result.MessageTypes.Any(t => t.Name == "ATT");
        HasVibeData = result.MessageTypes.Any(t => t.Name == "VIBE");
        
        // Set time range for zoom
        ZoomStartTime = 0;
        ZoomEndTime = result.Duration.TotalSeconds;
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024.0):F1} MB";
        return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
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

    partial void OnSelectedEventChanged(LogEvent? value)
    {
        if (value != null)
        {
            JumpToTime(value.Timestamp);
        }
    }

    partial void OnSelectedParameterChangeChanged(ParameterChange? value)
    {
        if (value != null)
        {
            JumpToTime(value.Timestamp);
        }
    }

    partial void OnShowInfoEventsChanged(bool value) => FilterEvents();
    partial void OnShowWarningEventsChanged(bool value) => FilterEvents();
    partial void OnShowErrorEventsChanged(bool value) => FilterEvents();
    partial void OnShowCriticalEventsChanged(bool value) => FilterEvents();
    partial void OnEventSearchTextChanged(string value) => FilterEvents();

    #endregion

    #region Events Commands

    private async Task DetectEventsAsync()
    {
        if (_eventDetector == null) return;

        IsAnalyzing = true;
        StatusMessage = "Detecting events...";

        try
        {
            var progress = new Progress<int>(p => LoadProgress = p);
            var events = await _eventDetector.DetectEventsAsync(progress);

            DetectedEvents.Clear();
            foreach (var evt in events)
            {
                DetectedEvents.Add(evt);
            }

            EventSummary = _eventDetector.GetEventSummary();
            ErrorCount = EventSummary.ErrorCount + EventSummary.CriticalCount;
            WarningCount = EventSummary.WarningCount;

            FilterEvents();
            StatusMessage = $"Detected {events.Count} events";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting events");
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private void FilterEvents()
    {
        FilteredEvents.Clear();

        var filtered = DetectedEvents.Where(e =>
        {
            if (e.Severity == LogEventSeverity.Info && !ShowInfoEvents) return false;
            if (e.Severity == LogEventSeverity.Warning && !ShowWarningEvents) return false;
            if (e.Severity == LogEventSeverity.Error && !ShowErrorEvents) return false;
            if (e.Severity == LogEventSeverity.Critical && !ShowCriticalEvents) return false;

            if (!string.IsNullOrWhiteSpace(EventSearchText))
            {
                var search = EventSearchText.ToLowerInvariant();
                if (!e.Title.ToLowerInvariant().Contains(search) &&
                    !e.Description.ToLowerInvariant().Contains(search))
                    return false;
            }

            return true;
        });

        foreach (var evt in filtered)
        {
            FilteredEvents.Add(evt);
        }
    }

    [RelayCommand]
    private void JumpToEvent(LogEvent? evt)
    {
        if (evt == null) return;
        JumpToTime(evt.Timestamp);
        SelectedTabIndex = TAB_PLOT;
    }

    private void JumpToTime(double timestamp)
    {
        CursorTime = timestamp;
        CursorTimeDisplay = TimeSpan.FromSeconds(timestamp).ToString(@"hh\:mm\:ss\.fff");
        
        // Update zoom window to center on this time
        var windowSize = (ZoomEndTime - ZoomStartTime);
        var halfWindow = windowSize / 2;
        ZoomStartTime = Math.Max(0, timestamp - halfWindow);
        ZoomEndTime = ZoomStartTime + windowSize;
        
        // Update cursor readouts
        UpdateCursorReadouts();
        
        // Update map position
        UpdateMapPosition(timestamp);
    }

    private void UpdateCursorReadouts()
    {
        CursorReadouts.Clear();

        foreach (var field in SelectedGraphFields)
        {
            var stats = _logAnalyzerService.GetFieldStatistics(field.DisplayName);
            // Would get actual value at cursor time from query engine
            CursorReadouts.Add(new CursorReadout
            {
                FieldName = field.DisplayName,
                Color = field.Color ?? "#FFFFFF",
                Value = stats.Average // Placeholder
            });
        }
    }

    private void UpdateMapPosition(double timestamp)
    {
        // Find GPS position at timestamp
        if (GpsTrack.Count > 0)
        {
            var nearest = GpsTrack.MinBy(p => Math.Abs(p.Timestamp - timestamp));
            if (nearest != null)
            {
                CurrentMapPosition = nearest;
                MapCenterLat = nearest.Latitude;
                MapCenterLng = nearest.Longitude;
            }
        }
    }

    #endregion

    #region Map Commands

    private void LoadGpsTrack()
    {
        GpsTrack.Clear();

        var latData = _logAnalyzerService.GetFieldData("GPS", "Lat");
        var lngData = _logAnalyzerService.GetFieldData("GPS", "Lng");
        var altData = _logAnalyzerService.GetFieldData("GPS", "Alt");

        if (latData == null || lngData == null) return;

        var minCount = Math.Min(latData.Count, lngData.Count);
        var altCount = altData?.Count ?? 0;

        for (int i = 0; i < minCount; i++)
        {
            var lat = latData[i].Value;
            var lng = lngData[i].Value;
            
            // Skip invalid coordinates
            if (Math.Abs(lat) < 0.001 && Math.Abs(lng) < 0.001)
                continue;

            var point = new GpsPoint
            {
                Latitude = lat,
                Longitude = lng,
                Altitude = i < altCount ? altData![i].Value : 0,
                Timestamp = latData[i].Timestamp / 1e6 // Convert to seconds
            };
            GpsTrack.Add(point);
        }

        if (GpsTrack.Count > 0)
        {
            MapCenterLat = GpsTrack[0].Latitude;
            MapCenterLng = GpsTrack[0].Longitude;
        }

        HasGpsData = GpsTrack.Count > 0;
    }

    #endregion

    #region Export Commands

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (_parentWindow == null || !IsLogLoaded) return;

        try
        {
            var file = await _parentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export to CSV",
                DefaultExtension = "csv",
                SuggestedFileName = $"log_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } }
                }
            });

            if (file != null && _exportService != null)
            {
                IsBusy = true;
                StatusMessage = "Exporting to CSV...";

                var seriesKeys = SelectedGraphFields.Select(f => f.DisplayName).ToList();
                if (seriesKeys.Count == 0)
                {
                    StatusMessage = "Select series to export first";
                    return;
                }

                var progress = new Progress<int>(p => LoadProgress = p);
                var result = await _exportService.ExportToCsvAsync(
                    file.Path.LocalPath, seriesKeys, ZoomStartTime, ZoomEndTime, progress);

                if (result.IsSuccess)
                {
                    StatusMessage = $"Exported {result.RecordCount} records to CSV";
                }
                else
                {
                    StatusMessage = $"Export failed: {result.ErrorMessage}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV export failed");
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ExportToKmlAsync()
    {
        if (_parentWindow == null || !IsLogLoaded || !HasGpsData) return;

        try
        {
            var file = await _parentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Export to KML",
                DefaultExtension = "kml",
                SuggestedFileName = $"flight_track_{DateTime.Now:yyyyMMdd_HHmmss}.kml",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("KML Files") { Patterns = new[] { "*.kml" } }
                }
            });

            if (file != null && _exportService != null)
            {
                IsBusy = true;
                StatusMessage = "Exporting to KML...";

                var progress = new Progress<int>(p => LoadProgress = p);
                var result = await _exportService.ExportToKmlAsync(
                    file.Path.LocalPath, true, progress);

                if (result.IsSuccess)
                {
                    StatusMessage = $"Exported GPS track to KML ({result.RecordCount} points)";
                }
                else
                {
                    StatusMessage = $"Export failed: {result.ErrorMessage}";
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "KML export failed");
            StatusMessage = $"Export failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
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
            var files = await _parentWindow.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
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
        try { _logAnalyzerService.CancelDownload(); }
        catch (Exception ex) { _logger.LogWarning(ex, "Error canceling download"); }
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
        
        foreach (var field in fields)
        {
            var stats = _logAnalyzerService.GetFieldStatistics(field.DisplayName);
            field.MinValue = stats.Minimum;
            field.MaxValue = stats.Maximum;
            field.MeanValue = stats.Average;
            
            AvailableFields.Add(field);
            FilteredFields.Add(field);
        }

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

    public void OnFieldSelectionChanged(LogFieldInfo field)
    {
        if (field.IsSelected) AddFieldToGraph(field);
        else RemoveFieldFromGraph(field);
    }

    public bool CanGoToNextPage => CurrentMessagePage < TotalMessagePages - 1;
    public bool CanGoToPreviousPage => CurrentMessagePage > 0;

    [RelayCommand]
    private void ResetZoom()
    {
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
                StatusMessage = $"Graph exported to {Path.GetFileName(file.Path.LocalPath)}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export graph");
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void GraphLeft() => StatusMessage = "Graph panned left";

    [RelayCommand]
    private void GraphRight() => StatusMessage = "Graph panned right";

    #endregion

    #region Script Commands

    [RelayCommand]
    private async Task LoadScriptFileAsync()
    {
        if (_parentWindow == null) { StatusMessage = "Cannot open file browser"; return; }

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
        if (_parentWindow == null || string.IsNullOrWhiteSpace(ScriptText)) { StatusMessage = "Nothing to save"; return; }

        try
        {
            var file = await _parentWindow.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Lua Script",
                DefaultExtension = "lua",
                SuggestedFileName = !string.IsNullOrEmpty(LoadedScriptName) ? LoadedScriptName : "script.lua",
                FileTypeChoices = new[] { new FilePickerFileType("Lua Scripts") { Patterns = new[] { "*.lua" } } }
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
        if (string.IsNullOrWhiteSpace(ScriptText)) { StatusMessage = "No script loaded."; return; }
        if (!IsLogLoaded) { StatusMessage = "Load a log file first."; return; }

        IsScriptRunning = true;
        ScriptOutput = "Running script...\n";

        try
        {
            var result = await _logAnalyzerService.RunScriptAsync(ScriptText);
            if (result.IsSuccess)
            {
                ScriptOutput = result.Output;
                if (result.Warnings.Count > 0)
                    ScriptOutput += "\nWarnings:\n" + string.Join("\n", result.Warnings);
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
        ScriptText = string.IsNullOrEmpty(ScriptText) ? function.Example : ScriptText + $"\n{function.Example}";
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
}



/// <summary>
/// Cursor readout for displaying values at cursor position.
/// </summary>
public class CursorReadout
{
    public string FieldName { get; set; } = string.Empty;
    public string Color { get; set; } = "#FFFFFF";
    public double Value { get; set; }
    public string ValueDisplay => Value.ToString("F3");
}



/// <summary>
/// GPS point for map display.
/// </summary>
public class GpsPoint
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Altitude { get; set; }
    public double Timestamp { get; set; }
}



/// <summary>
/// Parameter change record.
/// </summary>
public class ParameterChange
{
    public string Name { get; set; } = string.Empty;
    public double OldValue { get; set; }
    public double NewValue { get; set; }
    public double Timestamp { get; set; }
    public string TimestampDisplay => TimeSpan.FromSeconds(Timestamp).ToString(@"hh\:mm\:ss");
}

/// <summary>
/// Converter from bool to Yes/No string
/// </summary>
public class BoolToYesNoConverter : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
            return b ? "Yes" : "No";
        return "No";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter from bool to success/error color
/// </summary>
public class BoolToSuccessColorConverter : Avalonia.Data.Converters.IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
            return b ? "#10B981" : "#EF4444";  // Green for true, red for false
        return "#888";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
