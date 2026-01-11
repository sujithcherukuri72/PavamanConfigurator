using PavamanDroneConfigurator.Core.Enums;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Represents a log file stored on the flight controller.
/// </summary>
public class LogFileInfo : INotifyPropertyChanged
{
    private int _logId;
    private string _fileName = string.Empty;
    private string _filePath = string.Empty;
    private long _fileSize;
    private DateTime? _createdDate;
    private LogFileType _fileType;
    private LogDownloadStatus _downloadStatus;
    private int _downloadProgress;
    private string? _localPath;
    private bool _isSelected;

    /// <summary>
    /// Log ID (sequential number).
    /// </summary>
    public int LogId
    {
        get => _logId;
        set { _logId = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Name of the log file.
    /// </summary>
    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Full path on the flight controller.
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set { _filePath = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Size of the file in bytes.
    /// </summary>
    public long FileSize
    {
        get => _fileSize;
        set { _fileSize = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileSizeDisplay)); OnPropertyChanged(nameof(FileSizeKB)); }
    }

    /// <summary>
    /// File size in KB for display.
    /// </summary>
    public string FileSizeKB => $"{_fileSize / 1024.0:N0} kB";

    /// <summary>
    /// Display-friendly file size.
    /// </summary>
    public string FileSizeDisplay
    {
        get
        {
            if (_fileSize < 1024)
                return $"{_fileSize} B";
            if (_fileSize < 1024 * 1024)
                return $"{_fileSize / 1024.0:F1} KB";
            if (_fileSize < 1024 * 1024 * 1024)
                return $"{_fileSize / (1024.0 * 1024.0):F1} MB";
            return $"{_fileSize / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    /// <summary>
    /// Date when the file was created (if available).
    /// </summary>
    public DateTime? CreatedDate
    {
        get => _createdDate;
        set { _createdDate = value; OnPropertyChanged(); OnPropertyChanged(nameof(CreatedDateDisplay)); OnPropertyChanged(nameof(DateTimeDisplay)); }
    }

    /// <summary>
    /// Display-friendly created date.
    /// </summary>
    public string CreatedDateDisplay => _createdDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Unknown";

    /// <summary>
    /// Date and time display for the table.
    /// </summary>
    public string DateTimeDisplay => _createdDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

    /// <summary>
    /// Type of log file.
    /// </summary>
    public LogFileType FileType
    {
        get => _fileType;
        set { _fileType = value; OnPropertyChanged(); OnPropertyChanged(nameof(FileTypeDisplay)); }
    }

    /// <summary>
    /// Display-friendly file type.
    /// </summary>
    public string FileTypeDisplay => _fileType switch
    {
        LogFileType.DataFlashLog => "DataFlash Log",
        LogFileType.TelemetryLog => "Telemetry Log",
        LogFileType.ParamFile => "Parameter File",
        LogFileType.ScriptFile => "Script File",
        LogFileType.WaypointFile => "Waypoint File",
        _ => "Unknown"
    };

    /// <summary>
    /// Current download status.
    /// </summary>
    public LogDownloadStatus DownloadStatus
    {
        get => _downloadStatus;
        set { _downloadStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(DownloadStatusDisplay)); OnPropertyChanged(nameof(StatusDisplay)); }
    }

    /// <summary>
    /// Status display for the table.
    /// </summary>
    public string StatusDisplay => _downloadStatus switch
    {
        LogDownloadStatus.Pending => "Available",
        LogDownloadStatus.Downloading => $"Downloading {_downloadProgress}%",
        LogDownloadStatus.Completed => "Downloaded",
        LogDownloadStatus.Failed => "Failed",
        LogDownloadStatus.Cancelled => "Cancelled",
        _ => "Available"
    };

    /// <summary>
    /// Display-friendly download status.
    /// </summary>
    public string DownloadStatusDisplay => _downloadStatus switch
    {
        LogDownloadStatus.Pending => "Pending",
        LogDownloadStatus.Downloading => $"Downloading... {_downloadProgress}%",
        LogDownloadStatus.Completed => "Downloaded",
        LogDownloadStatus.Failed => "Failed",
        LogDownloadStatus.Cancelled => "Cancelled",
        _ => string.Empty
    };

    /// <summary>
    /// Download progress (0-100).
    /// </summary>
    public int DownloadProgress
    {
        get => _downloadProgress;
        set 
        { 
            _downloadProgress = value; 
            OnPropertyChanged(); 
            OnPropertyChanged(nameof(DownloadStatusDisplay));
            OnPropertyChanged(nameof(StatusDisplay));
        }
    }

    /// <summary>
    /// Local path where the file was downloaded.
    /// </summary>
    public string? LocalPath
    {
        get => _localPath;
        set { _localPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDownloaded)); }
    }

    /// <summary>
    /// Whether the file has been downloaded locally.
    /// </summary>
    public bool IsDownloaded => !string.IsNullOrEmpty(_localPath) && File.Exists(_localPath);

    /// <summary>
    /// Whether this file is selected for operations.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a directory entry from the flight controller file system.
/// </summary>
public class FtpDirectoryEntry
{
    /// <summary>
    /// Entry name (file or directory).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this is a directory.
    /// </summary>
    public bool IsDirectory { get; set; }

    /// <summary>
    /// File size (0 for directories).
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Full path on the flight controller.
    /// </summary>
    public string FullPath { get; set; } = string.Empty;
}
