using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Helper service for validating telemetry during calibration
/// Monitors STATUSTEXT messages and telemetry data to track calibration progress
/// </summary>
public class CalibrationTelemetryMonitor
{
    private readonly ILogger<CalibrationTelemetryMonitor> _logger;
    private readonly IConnectionService _connectionService;
    private readonly Dictionary<SensorCategory, CalibrationProgress> _progress;
    private readonly object _lock = new();

    public CalibrationTelemetryMonitor(
        ILogger<CalibrationTelemetryMonitor> logger,
        IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
        _progress = new Dictionary<SensorCategory, CalibrationProgress>();

        // Subscribe to STATUSTEXT messages
        _connectionService.StatusTextReceived += OnStatusTextReceived;
        _connectionService.CommandAckReceived += OnCommandAckReceived;
    }

    private void OnStatusTextReceived(object? sender, StatusTextEventArgs e)
    {
        _logger.LogDebug("STATUSTEXT [{Severity}]: {Text}", e.Severity, e.Text);

        // Parse calibration-related messages
        var text = e.Text.ToLowerInvariant();

        // Check for completion keywords
        if (text.Contains("calibration successful") || 
            text.Contains("calibration complete") ||
            text.Contains("cal complete"))
        {
            _logger.LogInformation("Calibration completed: {Text}", e.Text);
            UpdateProgress(null, isComplete: true);
        }

        // Check for failure keywords
        if (text.Contains("calibration failed") || 
            text.Contains("cal failed") ||
            text.Contains("error"))
        {
            _logger.LogWarning("Calibration failed: {Text}", e.Text);
            UpdateProgress(null, isFailed: true);
        }

        // Extract progress percentage if present
        if (text.Contains("%"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, @"(\d+)%");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var percent))
            {
                _logger.LogDebug("Calibration progress: {Percent}%", percent);
                UpdateProgress(null, progressPercent: percent);
            }
        }

        // Accelerometer position requests
        if (text.Contains("place") || text.Contains("position"))
        {
            _logger.LogInformation("Position request: {Text}", e.Text);
        }
    }

    private void OnCommandAckReceived(object? sender, CommandAckEventArgs e)
    {
        _logger.LogDebug("COMMAND_ACK: Command={Command}, Result={Result}", e.Command, e.Result);
        
        // Track command acknowledgments
        UpdateProgress(null, lastAckResult: e.Result);
    }

    public void StartMonitoring(SensorCategory category)
    {
        lock (_lock)
        {
            _progress[category] = new CalibrationProgress
            {
                Category = category,
                StartTime = DateTime.UtcNow,
                IsInProgress = true
            };
        }

        _logger.LogInformation("Started monitoring calibration for {Category}", category);
    }

    public void StopMonitoring(SensorCategory category)
    {
        lock (_lock)
        {
            if (_progress.TryGetValue(category, out var prog))
            {
                prog.IsInProgress = false;
                prog.EndTime = DateTime.UtcNow;
            }
        }

        _logger.LogInformation("Stopped monitoring calibration for {Category}", category);
    }

    public CalibrationProgress? GetProgress(SensorCategory category)
    {
        lock (_lock)
        {
            return _progress.TryGetValue(category, out var prog) ? prog : null;
        }
    }

    private void UpdateProgress(SensorCategory? category, 
        bool isComplete = false, 
        bool isFailed = false,
        int? progressPercent = null,
        byte? lastAckResult = null)
    {
        lock (_lock)
        {
            // Update all in-progress calibrations if category not specified
            IEnumerable<SensorCategory> categoriesToUpdate;
            if (category.HasValue)
            {
                categoriesToUpdate = new[] { category.Value };
            }
            else
            {
                categoriesToUpdate = _progress.Keys.ToList();
            }

            foreach (var cat in categoriesToUpdate)
            {
                if (_progress.TryGetValue(cat, out var prog) && prog.IsInProgress)
                {
                    if (isComplete) prog.IsComplete = true;
                    if (isFailed) prog.IsFailed = true;
                    if (progressPercent.HasValue) prog.ProgressPercent = progressPercent.Value;
                    if (lastAckResult.HasValue) prog.LastAckResult = lastAckResult.Value;
                    prog.LastUpdateTime = DateTime.UtcNow;
                }
            }
        }
    }

    public class CalibrationProgress
    {
        public SensorCategory Category { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
        public bool IsInProgress { get; set; }
        public bool IsComplete { get; set; }
        public bool IsFailed { get; set; }
        public int ProgressPercent { get; set; }
        public byte LastAckResult { get; set; }
        public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
    }
}
