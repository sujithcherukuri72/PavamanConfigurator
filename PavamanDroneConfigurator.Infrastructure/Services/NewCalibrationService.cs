using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asv.Mavlink.V2.Common;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// New calibration service implementation using asv-mavlink v3.9
/// Implements the backend logic for sensor calibration according to the specified UI data model
/// </summary>
public class NewCalibrationService : INewCalibrationService
{
    private readonly ILogger<NewCalibrationService> _logger;
    private readonly IMavlinkTransport _transport;
    private readonly AckService _ackService;
    private readonly MavlinkParameterService _parameterService;
    
    private readonly Dictionary<SensorCategory, Category> _categories;
    private readonly byte _targetSystem = 1;
    private readonly byte _targetComponent = 1;

    public NewCalibrationService(
        ILogger<NewCalibrationService> logger,
        IMavlinkTransport transport,
        AckService ackService,
        MavlinkParameterService parameterService)
    {
        _logger = logger;
        _transport = transport;
        _ackService = ackService;
        _parameterService = parameterService;
        
        _categories = InitializeCategories();
    }

    private Dictionary<SensorCategory, Category> InitializeCategories()
    {
        var categories = new Dictionary<SensorCategory, Category>();

        // Accelerometer
        categories[SensorCategory.Accelerometer] = new Category
        {
            Id = "accelerometer",
            DisplayName = "Accelerometer",
            Icon = "accel_icon",
            Required = true,
            Status = Status.NotCalibrated,
            Commands = CreateAccelerometerCommands(),
            CalibrationSteps = CreateAccelerometerSteps()
        };

        // Compass
        categories[SensorCategory.Compass] = new Category
        {
            Id = "compass",
            DisplayName = "Compass",
            Icon = "compass_icon",
            Required = true,
            Status = Status.NotCalibrated,
            Commands = CreateCompassCommands(),
            CalibrationSteps = CreateCompassSteps()
        };

        // Level Horizon
        categories[SensorCategory.LevelHorizon] = new Category
        {
            Id = "level_horizon",
            DisplayName = "Level Horizon",
            Icon = "level_icon",
            Required = true,
            Status = Status.NotCalibrated,
            Commands = CreateLevelHorizonCommands(),
            CalibrationSteps = CreateLevelHorizonSteps()
        };

        // Pressure
        categories[SensorCategory.Pressure] = new Category
        {
            Id = "pressure",
            DisplayName = "Pressure",
            Icon = "pressure_icon",
            Required = false,
            Status = Status.NotCalibrated,
            Commands = CreatePressureCommands(),
            CalibrationSteps = CreatePressureSteps()
        };

        // Flow
        categories[SensorCategory.Flow] = new Category
        {
            Id = "flow",
            DisplayName = "Flow",
            Icon = "flow_icon",
            Required = false,
            Status = Status.NotCalibrated,
            Commands = CreateFlowCommands(),
            CalibrationSteps = CreateFlowSteps()
        };

        return categories;
    }

    #region Command Creation

    private List<Command> CreateAccelerometerCommands()
    {
        return new List<Command>
        {
            new Command
            {
                CommandId = (int)MavCmd.MavCmdPreflightCalibration,
                Name = "StartAccelerometerCalibration",
                TimeoutMs = 5000,
                Schema = new PayloadSchema
                {
                    Parameters = new Dictionary<string, object>
                    {
                        { "param1", 0f }, // gyro
                        { "param2", 0f }, // mag
                        { "param3", 0f }, // ground_pressure
                        { "param4", 0f }, // airspeed
                        { "param5", 4f }  // accel (4 = full 6-axis calibration)
                    }
                },
                Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 },
                Preconditions = new List<Precondition>
                {
                    new Precondition { Type = "disarmed", Description = "Vehicle must be disarmed" },
                    new Precondition { Type = "connected", Description = "MAVLink connection required" }
                }
            },
            new Command
            {
                CommandId = (int)MavCmd.MavCmdAccelcalVehiclePos,
                Name = "SetAccelOrientation",
                TimeoutMs = 3000,
                Retry = new RetryPolicy { MaxRetries = 1, RetryDelayMs = 500 }
            },
            new Command
            {
                CommandId = (int)MavCmd.MavCmdAccelcalVehiclePos,
                Name = "NextAccelStep",
                TimeoutMs = 3000
            }
        };
    }

    private List<Command> CreateCompassCommands()
    {
        return new List<Command>
        {
            new Command
            {
                CommandId = (int)MavCmd.MavCmdPreflightCalibration,
                Name = "StartCompassCalibration",
                TimeoutMs = 5000,
                Schema = new PayloadSchema
                {
                    Parameters = new Dictionary<string, object>
                    {
                        { "param1", 0f }, // gyro
                        { "param2", 1f }, // mag (1 = start, 76 = onboard)
                        { "param3", 0f }, // ground_pressure
                        { "param4", 0f }, // airspeed
                        { "param5", 0f }  // accel
                    }
                },
                Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 }
            },
            new Command
            {
                CommandId = (int)MavCmd.MavCmdDoAcceptMagCal,
                Name = "FinishCompassCalibration",
                TimeoutMs = 5000
            }
        };
    }

    private List<Command> CreateLevelHorizonCommands()
    {
        return new List<Command>
        {
            new Command
            {
                CommandId = (int)MavCmd.MavCmdPreflightCalibration,
                Name = "StartLevelCalibration",
                TimeoutMs = 5000,
                Schema = new PayloadSchema
                {
                    Parameters = new Dictionary<string, object>
                    {
                        { "param1", 0f }, // gyro
                        { "param2", 0f }, // mag
                        { "param3", 0f }, // ground_pressure
                        { "param4", 0f }, // airspeed
                        { "param5", 2f }  // accel (2 = level/trim calibration)
                    }
                },
                Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 }
            }
        };
    }

    private List<Command> CreatePressureCommands()
    {
        return new List<Command>
        {
            new Command
            {
                CommandId = (int)MavCmd.MavCmdPreflightCalibration,
                Name = "StartPressureCalibration",
                TimeoutMs = 5000,
                Schema = new PayloadSchema
                {
                    Parameters = new Dictionary<string, object>
                    {
                        { "param1", 0f }, // gyro
                        { "param2", 0f }, // mag
                        { "param3", 1f }, // ground_pressure (1 = calibrate)
                        { "param4", 0f }, // airspeed
                        { "param5", 0f }  // accel
                    }
                },
                Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 }
            }
        };
    }

    private List<Command> CreateFlowCommands()
    {
        return new List<Command>
        {
            new Command
            {
                CommandId = 0, // Custom - will use PARAM_SET
                Name = "SetFlowScale",
                TimeoutMs = 5000,
                Retry = new RetryPolicy { MaxRetries = 2, RetryDelayMs = 1000 }
            }
        };
    }

    #endregion

    #region Step Creation

    private List<CalibrationStepInfo> CreateAccelerometerSteps()
    {
        return new List<CalibrationStepInfo>
        {
            new CalibrationStepInfo
            {
                StepIndex = 0,
                Label = "LEVEL",
                InstructionText = "Place vehicle level on a flat surface",
                StepStatus = Status.NotCalibrated,
                ExpectedTelemetry = new TelemetryExpectation
                {
                    MessageType = "SCALED_IMU",
                    TimeoutMs = 10000
                }
            },
            new CalibrationStepInfo
            {
                StepIndex = 1,
                Label = "LEFT",
                InstructionText = "Place vehicle on its left side",
                StepStatus = Status.NotCalibrated
            },
            new CalibrationStepInfo
            {
                StepIndex = 2,
                Label = "RIGHT",
                InstructionText = "Place vehicle on its right side",
                StepStatus = Status.NotCalibrated
            },
            new CalibrationStepInfo
            {
                StepIndex = 3,
                Label = "NOSE DOWN",
                InstructionText = "Place vehicle nose down",
                StepStatus = Status.NotCalibrated
            },
            new CalibrationStepInfo
            {
                StepIndex = 4,
                Label = "NOSE UP",
                InstructionText = "Place vehicle nose up",
                StepStatus = Status.NotCalibrated
            },
            new CalibrationStepInfo
            {
                StepIndex = 5,
                Label = "BACK",
                InstructionText = "Place vehicle on its back (upside down)",
                StepStatus = Status.NotCalibrated
            }
        };
    }

    private List<CalibrationStepInfo> CreateCompassSteps()
    {
        return new List<CalibrationStepInfo>
        {
            new CalibrationStepInfo
            {
                StepIndex = 0,
                Label = "ROTATE",
                InstructionText = "Slowly rotate the vehicle in all directions to cover all orientations",
                StepStatus = Status.NotCalibrated,
                ExpectedTelemetry = new TelemetryExpectation
                {
                    MessageType = "MAG_CAL_PROGRESS",
                    TimeoutMs = 60000
                }
            }
        };
    }

    private List<CalibrationStepInfo> CreateLevelHorizonSteps()
    {
        return new List<CalibrationStepInfo>
        {
            new CalibrationStepInfo
            {
                StepIndex = 0,
                Label = "LEVEL",
                InstructionText = "Place vehicle on a perfectly level surface and keep it still",
                StepStatus = Status.NotCalibrated,
                ExpectedTelemetry = new TelemetryExpectation
                {
                    MessageType = "ATTITUDE",
                    TimeoutMs = 10000
                }
            }
        };
    }

    private List<CalibrationStepInfo> CreatePressureSteps()
    {
        return new List<CalibrationStepInfo>
        {
            new CalibrationStepInfo
            {
                StepIndex = 0,
                Label = "STILL",
                InstructionText = "Keep vehicle stationary with no airflow disturbance",
                StepStatus = Status.NotCalibrated,
                ExpectedTelemetry = new TelemetryExpectation
                {
                    MessageType = "SCALED_PRESSURE",
                    TimeoutMs = 10000
                }
            }
        };
    }

    private List<CalibrationStepInfo> CreateFlowSteps()
    {
        return new List<CalibrationStepInfo>
        {
            new CalibrationStepInfo
            {
                StepIndex = 0,
                Label = "CONFIGURE",
                InstructionText = "Configure optical flow scale factors",
                StepStatus = Status.NotCalibrated
            }
        };
    }

    #endregion

    #region INewCalibrationService Implementation

    public async Task StartCalibrationAsync(SensorCategory category, CancellationToken ct)
    {
        _logger.LogInformation("Starting calibration for {Category}", category);

        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        // Check preconditions
        if (!CheckPreconditions(cat))
        {
            throw new InvalidOperationException("Preconditions not met");
        }

        // Update status
        cat.Status = Status.InProgress;
        foreach (var step in cat.CalibrationSteps)
        {
            step.StepStatus = Status.NotCalibrated;
        }

        // Get start command
        var startCommand = cat.Commands.FirstOrDefault();
        if (startCommand == null)
        {
            throw new InvalidOperationException("No start command found");
        }

        // Send calibration command based on category
        await SendCalibrationCommandAsync(category, startCommand, ct);
    }

    private async Task SendCalibrationCommandAsync(SensorCategory category, Command command, CancellationToken ct)
    {
        switch (category)
        {
            case SensorCategory.Accelerometer:
                await SendAccelerometerCalibrationAsync(ct);
                break;
            case SensorCategory.Compass:
                await SendCompassCalibrationAsync(ct);
                break;
            case SensorCategory.LevelHorizon:
                await SendLevelHorizonCalibrationAsync(ct);
                break;
            case SensorCategory.Pressure:
                await SendPressureCalibrationAsync(ct);
                break;
            case SensorCategory.Flow:
                await SendFlowCalibrationAsync(ct);
                break;
        }
    }

    private async Task SendAccelerometerCalibrationAsync(CancellationToken ct)
    {
        var cmd = new CommandLongPayload
        {
            TargetSystem = _targetSystem,
            TargetComponent = _targetComponent,
            Command = MavCmd.MavCmdPreflightCalibration,
            Param1 = 0f, // gyro
            Param2 = 0f, // mag
            Param3 = 0f, // ground_pressure
            Param4 = 0f, // airspeed
            Param5 = 4f  // accel (4 = full 6-axis)
        };

        await _transport.SendMessageAsync(cmd, ct);
        
        var ackReceived = await _ackService.WaitForAckAsync(
            (int)MavCmd.MavCmdPreflightCalibration,
            TimeSpan.FromSeconds(5),
            ct);

        if (!ackReceived)
        {
            throw new InvalidOperationException("Failed to start accelerometer calibration");
        }

        _logger.LogInformation("Accelerometer calibration started successfully");
    }

    private async Task SendCompassCalibrationAsync(CancellationToken ct)
    {
        var cmd = new CommandLongPayload
        {
            TargetSystem = _targetSystem,
            TargetComponent = _targetComponent,
            Command = MavCmd.MavCmdPreflightCalibration,
            Param1 = 0f, // gyro
            Param2 = 1f, // mag (1 = start calibration)
            Param3 = 0f, // ground_pressure
            Param4 = 0f, // airspeed
            Param5 = 0f  // accel
        };

        await _transport.SendMessageAsync(cmd, ct);
        
        var ackReceived = await _ackService.WaitForAckAsync(
            (int)MavCmd.MavCmdPreflightCalibration,
            TimeSpan.FromSeconds(5),
            ct);

        if (!ackReceived)
        {
            throw new InvalidOperationException("Failed to start compass calibration");
        }

        _logger.LogInformation("Compass calibration started successfully");
    }

    private async Task SendLevelHorizonCalibrationAsync(CancellationToken ct)
    {
        var cmd = new CommandLongPayload
        {
            TargetSystem = _targetSystem,
            TargetComponent = _targetComponent,
            Command = MavCmd.MavCmdPreflightCalibration,
            Param1 = 0f, // gyro
            Param2 = 0f, // mag
            Param3 = 0f, // ground_pressure
            Param4 = 0f, // airspeed
            Param5 = 2f  // accel (2 = level/trim calibration)
        };

        await _transport.SendMessageAsync(cmd, ct);
        
        var ackReceived = await _ackService.WaitForAckAsync(
            (int)MavCmd.MavCmdPreflightCalibration,
            TimeSpan.FromSeconds(5),
            ct);

        if (!ackReceived)
        {
            throw new InvalidOperationException("Failed to start level horizon calibration");
        }

        _logger.LogInformation("Level horizon calibration started successfully");
    }

    private async Task SendPressureCalibrationAsync(CancellationToken ct)
    {
        var cmd = new CommandLongPayload
        {
            TargetSystem = _targetSystem,
            TargetComponent = _targetComponent,
            Command = MavCmd.MavCmdPreflightCalibration,
            Param1 = 0f, // gyro
            Param2 = 0f, // mag
            Param3 = 1f, // ground_pressure (1 = calibrate)
            Param4 = 0f, // airspeed
            Param5 = 0f  // accel
        };

        await _transport.SendMessageAsync(cmd, ct);
        
        var ackReceived = await _ackService.WaitForAckAsync(
            (int)MavCmd.MavCmdPreflightCalibration,
            TimeSpan.FromSeconds(5),
            ct);

        if (!ackReceived)
        {
            throw new InvalidOperationException("Failed to start pressure calibration");
        }

        _logger.LogInformation("Pressure calibration started successfully");
    }

    private async Task SendFlowCalibrationAsync(CancellationToken ct)
    {
        // Flow calibration is done via parameter writes
        // FLOW_FXSCALER, FLOW_FYSCALER
        _logger.LogInformation("Flow sensor calibration - parameters will be set via UI");
        await Task.CompletedTask;
    }

    public async Task NextStepAsync(SensorCategory category, CancellationToken ct)
    {
        _logger.LogInformation("Advancing to next step for {Category}", category);

        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        // For accelerometer, send position confirmation
        if (category == SensorCategory.Accelerometer)
        {
            var currentStep = cat.CalibrationSteps.FirstOrDefault(s => s.StepStatus == Status.InProgress);
            if (currentStep != null)
            {
                await SendAccelPositionAsync(currentStep.StepIndex + 1, ct);
            }
        }
    }

    private async Task SendAccelPositionAsync(int positionIndex, CancellationToken ct)
    {
        var cmd = new CommandLongPayload
        {
            TargetSystem = _targetSystem,
            TargetComponent = _targetComponent,
            Command = MavCmd.MavCmdAccelcalVehiclePos,
            Param1 = positionIndex
        };

        await _transport.SendMessageAsync(cmd, ct);
        
        var ackReceived = await _ackService.WaitForAckAsync(
            (int)MavCmd.MavCmdAccelcalVehiclePos,
            TimeSpan.FromSeconds(3),
            ct);

        if (!ackReceived)
        {
            throw new InvalidOperationException($"Failed to confirm position {positionIndex}");
        }

        _logger.LogInformation("Accelerometer position {Position} confirmed", positionIndex);
    }

    public async Task AbortCalibrationAsync(SensorCategory category, CancellationToken ct)
    {
        _logger.LogInformation("Aborting calibration for {Category}", category);

        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        // Send abort command (param1 = 0 aborts all)
        var cmd = new CommandLongPayload
        {
            TargetSystem = _targetSystem,
            TargetComponent = _targetComponent,
            Command = MavCmd.MavCmdPreflightCalibration,
            Param1 = 0f,
            Param2 = 0f,
            Param3 = 0f,
            Param4 = 0f,
            Param5 = 0f
        };

        await _transport.SendMessageAsync(cmd, ct);
        
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
    }

    public async Task CommitCalibrationAsync(SensorCategory category, CancellationToken ct)
    {
        _logger.LogInformation("Committing calibration for {Category}", category);

        if (!_categories.TryGetValue(category, out var cat))
        {
            throw new ArgumentException($"Unknown category: {category}");
        }

        // For compass, send accept command
        if (category == SensorCategory.Compass)
        {
            var cmd = new CommandLongPayload
            {
                TargetSystem = _targetSystem,
                TargetComponent = _targetComponent,
                Command = MavCmd.MavCmdDoAcceptMagCal
            };

            await _transport.SendMessageAsync(cmd, ct);
            
            var ackReceived = await _ackService.WaitForAckAsync(
                (int)MavCmd.MavCmdDoAcceptMagCal,
                TimeSpan.FromSeconds(5),
                ct);

            if (!ackReceived)
            {
                throw new InvalidOperationException("Failed to commit compass calibration");
            }
        }

        // Update status
        cat.Status = Status.Complete;
        foreach (var step in cat.CalibrationSteps)
        {
            step.StepStatus = Status.Complete;
        }

        _logger.LogInformation("Calibration committed for {Category}", category);
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

        var cmd = new CommandLongPayload
        {
            TargetSystem = _targetSystem,
            TargetComponent = _targetComponent,
            Command = MavCmd.MavCmdPreflightRebootShutdown,
            Param1 = 1f // Reboot autopilot
        };

        await _transport.SendMessageAsync(cmd, ct);
        
        var ackReceived = await _ackService.WaitForAckAsync(
            (int)MavCmd.MavCmdPreflightRebootShutdown,
            TimeSpan.FromSeconds(5),
            ct);

        if (!ackReceived)
        {
            _logger.LogWarning("Reboot command may not have been acknowledged");
        }

        _logger.LogInformation("Reboot command sent");
    }

    private bool CheckPreconditions(Category category)
    {
        // Check if transport is connected
        if (!_transport.IsConnected)
        {
            _logger.LogWarning("Precondition failed: Not connected");
            return false;
        }

        // Check command preconditions
        var startCommand = category.Commands.FirstOrDefault();
        if (startCommand?.Preconditions != null)
        {
            foreach (var precondition in startCommand.Preconditions)
            {
                if (precondition.CheckFunction != null && !precondition.CheckFunction())
                {
                    _logger.LogWarning("Precondition failed: {Description}", precondition.Description);
                    return false;
                }
            }
        }

        return true;
    }

    #endregion
}
