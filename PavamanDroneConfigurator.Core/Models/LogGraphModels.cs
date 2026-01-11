using System.Collections.ObjectModel;
using System.ComponentModel;

namespace PavamanDroneConfigurator.Core.Models;

/// <summary>
/// Represents a graph configuration for log data visualization.
/// </summary>
public class LogGraphConfiguration
{
    /// <summary>
    /// Title of the graph.
    /// </summary>
    public string Title { get; set; } = "Log Graph";
    
    /// <summary>
    /// Data series to plot.
    /// </summary>
    public List<LogGraphSeries> Series { get; set; } = new();
    
    /// <summary>
    /// X-axis configuration.
    /// </summary>
    public LogGraphAxis XAxis { get; set; } = new() { Label = "Time (s)" };
    
    /// <summary>
    /// Y-axis configuration.
    /// </summary>
    public LogGraphAxis YAxis { get; set; } = new() { Label = "Value" };
    
    /// <summary>
    /// Whether to show legend.
    /// </summary>
    public bool ShowLegend { get; set; } = true;
    
    /// <summary>
    /// Whether to auto-scale axes.
    /// </summary>
    public bool AutoScale { get; set; } = true;
}

/// <summary>
/// Represents a single data series on a graph.
/// </summary>
public class LogGraphSeries
{
    /// <summary>
    /// Display name for the series.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Message type (e.g., "GPS", "ATT", "IMU").
    /// </summary>
    public string MessageType { get; set; } = string.Empty;
    
    /// <summary>
    /// Field name (e.g., "Alt", "Roll", "AccX").
    /// </summary>
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// Full series key (MessageType.FieldName).
    /// </summary>
    public string Key => $"{MessageType}.{FieldName}";
    
    /// <summary>
    /// Line color (hex format).
    /// </summary>
    public string Color { get; set; } = "#3B82F6";
    
    /// <summary>
    /// Line thickness.
    /// </summary>
    public double LineWidth { get; set; } = 1.5;
    
    /// <summary>
    /// Whether the series is visible.
    /// </summary>
    public bool IsVisible { get; set; } = true;
    
    /// <summary>
    /// Data points for this series.
    /// </summary>
    public List<GraphPoint> Points { get; set; } = new();
    
    /// <summary>
    /// Minimum value in the series.
    /// </summary>
    public double MinValue => Points.Count > 0 ? Points.Min(p => p.Y) : 0;
    
    /// <summary>
    /// Maximum value in the series.
    /// </summary>
    public double MaxValue => Points.Count > 0 ? Points.Max(p => p.Y) : 0;
    
    /// <summary>
    /// Average value in the series.
    /// </summary>
    public double Average => Points.Count > 0 ? Points.Average(p => p.Y) : 0;
}

/// <summary>
/// Represents a point on a graph.
/// </summary>
public class GraphPoint
{
    public double X { get; set; }
    public double Y { get; set; }
    
    public GraphPoint() { }
    
    public GraphPoint(double x, double y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Axis configuration for graphs.
/// </summary>
public class LogGraphAxis
{
    /// <summary>
    /// Axis label.
    /// </summary>
    public string Label { get; set; } = string.Empty;
    
    /// <summary>
    /// Minimum value (null for auto).
    /// </summary>
    public double? Minimum { get; set; }
    
    /// <summary>
    /// Maximum value (null for auto).
    /// </summary>
    public double? Maximum { get; set; }
    
    /// <summary>
    /// Number of grid lines.
    /// </summary>
    public int GridLines { get; set; } = 5;
}

/// <summary>
/// Represents an available field for selection in the UI.
/// </summary>
public class LogFieldInfo
{
    /// <summary>
    /// Message type name.
    /// </summary>
    public string MessageType { get; set; } = string.Empty;
    
    /// <summary>
    /// Field name.
    /// </summary>
    public string FieldName { get; set; } = string.Empty;
    
    /// <summary>
    /// Display name.
    /// </summary>
    public string DisplayName => $"{MessageType}.{FieldName}";
    
    /// <summary>
    /// Number of data points.
    /// </summary>
    public int DataPointCount { get; set; }
    
    /// <summary>
    /// Whether this field is currently selected for graphing.
    /// </summary>
    public bool IsSelected { get; set; }
    
    /// <summary>
    /// Assigned color when selected.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Minimum value in the data series (for legend display).
    /// </summary>
    public double MinValue { get; set; }

    /// <summary>
    /// Maximum value in the data series (for legend display).
    /// </summary>
    public double MaxValue { get; set; }

    /// <summary>
    /// Mean/average value in the data series (for legend display).
    /// </summary>
    public double MeanValue { get; set; }
}

/// <summary>
/// Represents a group of fields from a message type.
/// </summary>
public class LogMessageTypeGroup
{
    /// <summary>
    /// Message type name (e.g., "GPS", "ATT", "IMU").
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of messages of this type.
    /// </summary>
    public int MessageCount { get; set; }
    
    /// <summary>
    /// Available fields.
    /// </summary>
    public List<LogFieldInfo> Fields { get; set; } = new();
    
    /// <summary>
    /// Whether this group is expanded in UI.
    /// </summary>
    public bool IsExpanded { get; set; }
}

/// <summary>
/// Tree node for hierarchical message type display (Mission Planner style)
/// </summary>
public class LogMessageTypeNode : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public bool IsMessageType { get; set; } = true;
    public ObservableCollection<LogFieldNode> Fields { get; set; } = new();
    public bool HasChildren => Fields.Count > 0;
    
    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Field node for TreeView (child of message type)
/// </summary>
public class LogFieldNode : INotifyPropertyChanged
{
    public string Name { get; set; } = string.Empty;
    public string FullKey { get; set; } = string.Empty; // MessageType.FieldName
    public string Color { get; set; } = "#FFFFFF";
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double MeanValue { get; set; }
    
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

/// <summary>
/// Predefined graph colors for series.
/// </summary>
public static class GraphColors
{
    public static readonly string[] DefaultColors = new[]
    {
        "#3B82F6", // Blue
        "#EF4444", // Red
        "#10B981", // Green
        "#F59E0B", // Orange
        "#8B5CF6", // Purple
        "#EC4899", // Pink
        "#06B6D4", // Cyan
        "#84CC16", // Lime
        "#F97316", // Orange-Dark
        "#6366F1", // Indigo
    };
    
    public static string GetColor(int index)
    {
        return DefaultColors[index % DefaultColors.Length];
    }
}
