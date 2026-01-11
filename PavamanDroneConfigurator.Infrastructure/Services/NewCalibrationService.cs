using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// New calibration service implementation using existing ConnectionService infrastructure
/// Implements the backend logic for sensor calibration according to the specified UI data model
/// </summary>
public class NewCalibrationService : INewCalibrationService
{
    private readonly ILogger<NewCalibrationService> _logger;
    private readonly IConnectionService _connectionService;
    private readonly CalibrationTelemetryMonitor _telemetryMonitor;
    private readonly CalibrationParameterHelper _parameterHelper;
    
    private readonly Dictionary<SensorCategory, Category> _categories;
    private readonly Dictionary<SensorCategory, int> _currentStepIndex;

    public NewCalibrationService(
        ILogger<NewCalibrationService> logger,
        IConnectionService connectionService,
        CalibrationTelemetryMonitor telemetryMonitor,
        CalibrationParameterHelper parameterHelper)
    {
        _logger = logger;
        _connectionService = connectionService;
        _telemetryMonitor = telemetryMonitor;
        _parameterHelper = parameterHelper;
        
        _categories = InitializeCategories();
        _currentStepIndex = new Dictionary<SensorCategory, int>();
    }

    private Dictionary<SensorCategory, Category> InitializeCategories()
    {
        var categories = new Dictionary<SensorCategory, Category>();

        // Accelerometer - 6-axis calibration
        categories[SensorCategory.Accelerometer] = new Category
        {
            Id = "accelerometer",
            DisplayName = "Accelerometer",
            Icon = "accel_icon",
            Required = true,
            Status = Status.NotCalibrated,
            Commands = new List<Command>
            {
                new Command
                {
                    CommandId = 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    Name = "StartAccelerometerCalibration",
                    TimeoutMs = 5000,
                    Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 }
                },
                new Command
                {
                    CommandId = 42429, // MAV_CMD_ACCELCAL_VEHICLE_POS
                    Name = "SetAccelOrientation",
                    TimeoutMs = 3000
                }
            },
            CalibrationSteps = new List<CalibrationStepInfo>
            {
                new CalibrationStepInfo
                {
                    StepIndex = 0,
                    Label = "LEVEL",
                    InstructionText = "Place vehicle level on a flat surface, then click Next",
                    StepStatus = Status.NotCalibrated
                },
                new CalibrationStepInfo
                {
                    StepIndex = 1,
                    Label = "LEFT",
                    InstructionText = "Place vehicle on its left side, then click Next",
                    StepStatus = Status.NotCalibrated
                },
                new CalibrationStepInfo
                {
                    StepIndex = 2,
                    Label = "RIGHT",
                    InstructionText = "Place vehicle on its right side, then click Next",
                    StepStatus = Status.NotCalibrated
                },
                new CalibrationStepInfo
                {
                    StepIndex = 3,
                    Label = "NOSE DOWN",
                    InstructionText = "Place vehicle nose down, then click Next",
                    StepStatus = Status.NotCalibrated
                },
                new CalibrationStepInfo
                {
                    StepIndex = 4,
                    Label = "NOSE UP",
                    InstructionText = "Place vehicle nose up, then click Next",
                    StepStatus = Status.NotCalibrated
                },
                new CalibrationStepInfo
                {
                    StepIndex = 5,
                    Label = "BACK",
                    InstructionText = "Place vehicle on its back (upside down), then click Next",
                    StepStatus = Status.NotCalibrated
                }
            }
        };

        // Compass/Magnetometer calibration
        categories[SensorCategory.Compass] = new Category
        {
            Id = "compass",
            DisplayName = "Compass",
            Icon = "compass_icon",
            Required = true,
            Status = Status.NotCalibrated,
            Commands = new List<Command>
            {
                new Command
                {
                    CommandId = 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    Name = "StartCompassCalibration",
                    TimeoutMs = 5000,
                    Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 }
                }
            },
            CalibrationSteps = new List<CalibrationStepInfo>
            {
                new CalibrationStepInfo
                {
                    StepIndex = 0,
                    Label = "ROTATE",
                    InstructionText = "Slowly rotate the vehicle in all directions until calibration completes",
                    StepStatus = Status.NotCalibrated
                }
            }
        };

        // Level Horizon calibration
        categories[SensorCategory.LevelHorizon] = new Category
        {
            Id = "level_horizon",
            DisplayName = "Level Horizon",
            Icon = "level_icon",
            Required = true,
            Status = Status.NotCalibrated,
            Commands = new List<Command>
            {
                new Command
                {
                    CommandId = 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    Name = "StartLevelCalibration",
                    TimeoutMs = 5000,
                    Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 }
                }
            },
            CalibrationSteps = new List<CalibrationStepInfo>
            {
                new CalibrationStepInfo
                {
                    StepIndex = 0,
                    Label = "LEVEL",
                    InstructionText = "Place vehicle on a perfectly level surface and keep it still",
                    StepStatus = Status.NotCalibrated
                }
            }
        };

        // Pressure/Barometer calibration
        categories[SensorCategory.Pressure] = new Category
        {
            Id = "pressure",
            DisplayName = "Pressure",
            Icon = "pressure_icon",
            Required = false,
            Status = Status.NotCalibrated,
            Commands = new List<Command>
            {
                new Command
                {
                    CommandId = 241, // MAV_CMD_PREFLIGHT_CALIBRATION
                    Name = "StartPressureCalibration",
                    TimeoutMs = 5000,
                    Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 }
                }
            },
            CalibrationSteps = new List<CalibrationStepInfo>
            {
                new CalibrationStepInfo
                {
                    StepIndex = 0,
                    Label = "STILL",
                    InstructionText = "Keep vehicle stationary with no airflow disturbance",
                    StepStatus = Status.NotCalibrated
                }
            }
        };

        // Flow sensor configuration
        categories[SensorCategory.Flow] = new Category
        {
            Id = "flow",
            DisplayName = "Flow",
            Icon = "flow_icon",
            Required = false,
            Status = Status.NotCalibrated,
            Commands = new List<Command>
            {
                new Command
                {
                    CommandId = 0, // Custom - uses PARAM_SET
                    Name = "SetFlowScale",
                    TimeoutMs = 5000
                }
            },
            CalibrationSteps = new List<CalibrationStepInfo>
            {
                new CalibrationStepInfo
                {
                    StepIndex = 0,
                    Label = "CONFIGURE",
                    InstructionText = "Configure optical flow scale factors via parameters",
                    StepStatus = Status.NotCalibrated
                }
            }
        };

        return categories;
    }

    public async Task StartCalibrationAsync(SensorCategory category, CancellationToken ct)
    {
        _logger.LogInformation("Starting calibration for {Category}", category);

        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        // Check if connected
        if (!_connectionService.IsConnected)
        {
            throw new InvalidOperationException("Not connected to drone");
        }

        // Start telemetry monitoring
        _telemetryMonitor.StartMonitoring(category);

        // Reset step index
        _currentStepIndex[category] = 0;

        // Update status
        cat.Status = Status.InProgress;
        foreach (var step in cat.CalibrationSteps)
        {
            step.StepStatus = Status.NotCalibrated;
        }

        if (cat.CalibrationSteps.Count > 0)
        {
            cat.CalibrationSteps[0].StepStatus = Status.InProgress;
        }

        // Send calibration start command based on category
        switch (category)
        {
            case SensorCategory.Accelerometer:
                // MAV_CMD_PREFLIGHT_CALIBRATION: param5 = 4 (full 6-axis accel calibration)
                _connectionService.SendPreflightCalibration(
                    gyro: 0, mag: 0, groundPressure: 0, airspeed: 0, accel: 4);
                break;

            case SensorCategory.Compass:
                // MAV_CMD_PREFLIGHT_CALIBRATION: param2 = 1 (mag calibration)
                _connectionService.SendPreflightCalibration(
                    gyro: 0, mag: 1, groundPressure: 0, airspeed: 0, accel: 0);
                break;

            case SensorCategory.LevelHorizon:
                // MAV_CMD_PREFLIGHT_CALIBRATION: param5 = 2 (level/trim calibration)
                _connectionService.SendPreflightCalibration(
                    gyro: 0, mag: 0, groundPressure: 0, airspeed: 0, accel: 2);
                break;

            case SensorCategory.Pressure:
                // MAV_CMD_PREFLIGHT_CALIBRATION: param3 = 1 (ground pressure calibration)
                _connectionService.SendPreflightCalibration(
                    gyro: 0, mag: 0, groundPressure: 1, airspeed: 0, accel: 0);
                break;

            case SensorCategory.Flow:
                // Flow calibration is done via parameter setting
                _logger.LogInformation("Flow sensor calibration - use parameter interface");
                break;
        }

        _logger.LogInformation("Calibration started for {Category}", category);
        await Task.CompletedTask;
    }

    public async Task NextStepAsync(SensorCategory category, CancellationToken ct)
    {
        _logger.LogInformation("Advancing to next step for {Category}", category);

        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        if (!_currentStepIndex.TryGetValue(category, out var currentIndex))
        {
            throw new InvalidOperationException("Calibration not started");
        }

        // For accelerometer, send position confirmation
        if (category == SensorCategory.Accelerometer)
        {
            // MAV_CMD_ACCELCAL_VEHICLE_POS: param1 = position index (1-6)
            int position = currentIndex + 1; // Position is 1-indexed
            _connectionService.SendAccelCalVehiclePos(position);
            
            _logger.LogInformation("Sent accelerometer position {Position}", position);

            // Mark current step as complete
            if (currentIndex < cat.CalibrationSteps.Count)
            {
                cat.CalibrationSteps[currentIndex].StepStatus = Status.Complete;
            }

            // Move to next step
            currentIndex++;
            _currentStepIndex[category] = currentIndex;

            if (currentIndex < cat.CalibrationSteps.Count)
            {
                cat.CalibrationSteps[currentIndex].StepStatus = Status.InProgress;
            }
            else
            {
                // All steps completed
                cat.Status = Status.Complete;
                _logger.LogInformation("All calibration steps completed for {Category}", category);
            }
        }
        else
        {
            // For other calibrations, just mark as complete
            cat.Status = Status.Complete;
            foreach (var step in cat.CalibrationSteps)
            {
                step.StepStatus = Status.Complete;
            }
        }

        await Task.CompletedTask;
    }

    public async Task AbortCalibrationAsync(SensorCategory category, CancellationToken ct)
    {
        _logger.LogInformation("Aborting calibration for {Category}", category);

        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        // Stop telemetry monitoring
        _telemetryMonitor.StopMonitoring(category);

        // Send abort command (all params = 0 aborts calibration)
        _connectionService.SendPreflightCalibration(
            gyro: 0, mag: 0, groundPressure: 0, airspeed: 0, accel: 0);

        // Update status
        cat.Status = Status.Error;
        foreach (var step in cat.CalibrationSteps)
        {
            if (step.StepStatus == Status.InProgress)
            {
                step.StepStatus = Status.Error;
            }
        }

        _logger.LogInformation("Calibration aborted for {Category}", category);
        await Task.CompletedTask;
    }

    public async Task CommitCalibrationAsync(SensorCategory category, CancellationToken ct)
    {
        _logger.LogInformation("Committing calibration for {Category}", category);

        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        // Stop telemetry monitoring
        _telemetryMonitor.StopMonitoring(category);

        // Verify calibration if possible
        bool verified = await VerifyCalibrationAsync(category, ct);
        
        if (!verified)
        {
            _logger.LogWarning("Calibration verification failed for {Category}", category);
        }

        // Mark as complete
        cat.Status = Status.Complete;
        foreach (var step in cat.CalibrationSteps)
        {
            step.StepStatus = Status.Complete;
        }

        _logger.LogInformation("Calibration committed for {Category} (Verified: {Verified})", 
            category, verified);
        await Task.CompletedTask;
    }

    private async Task<bool> VerifyCalibrationAsync(SensorCategory category, CancellationToken ct)
    {
        try
        {
            switch (category)
            {
                case SensorCategory.Accelerometer:
                    return await _parameterHelper.VerifyAccelCalibrationAsync(ct);
                    
                case SensorCategory.Compass:
                    return await _parameterHelper.VerifyCompassCalibrationAsync(1, ct);
                    
                default:
                    _logger.LogInformation("No verification available for {Category}", category);
                    return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying calibration for {Category}", category);
            return false;
        }
    }

    public Category GetCategoryState(SensorCategory category)
    {
        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        return cat;
    }

    public async Task RebootDroneAsync(CancellationToken ct)
    {
        _logger.LogInformation("Rebooting drone");

        if (!_connectionService.IsConnected)
        {
            throw new InvalidOperationException("Not connected to drone");
        }

        // MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN: param1 = 1 (reboot autopilot)
        _connectionService.SendPreflightReboot(autopilot: 1, companion: 0);

        _logger.LogInformation("Reboot command sent");
        await Task.CompletedTask;
    }
}
