# Sensor Calibration Backend Implementation

This implementation provides a new sensor calibration backend for the drone configurator using the existing MAVLink infrastructure.

## Architecture

### Core Components

1. **CalibrationModels.cs** - Data models matching the UI specification
   - `Category` - Top-level calibration category with commands and steps
   - `Command` - MAVLink command definition with retry policy
   - `CalibrationStepInfo` - Individual calibration step with instructions
   - `Status` - Calibration status enum (NotDetected, NotCalibrated, InProgress, Complete, Error)
   - `SensorCategory` - Sensor types (Accelerometer, Compass, LevelHorizon, Pressure, Flow)

2. **INewCalibrationService.cs** - Service interface
   - `StartCalibrationAsync` - Begin calibration for a sensor category
   - `NextStepAsync` - Advance to next calibration step
   - `AbortCalibrationAsync` - Cancel current calibration
   - `CommitCalibrationAsync` - Finalize calibration results
   - `GetCategoryState` - Retrieve current category state
   - `RebootDroneAsync` - Reboot drone after calibration

3. **NewCalibrationService.cs** - Implementation
   - Uses existing `IConnectionService` for MAVLink communication
   - Manages calibration state for all sensor categories
   - Sends appropriate MAV_CMD_PREFLIGHT_CALIBRATION commands

### Supported Calibrations

#### Accelerometer (6-axis calibration)
- **Command**: MAV_CMD_PREFLIGHT_CALIBRATION (param5=4)
- **Steps**: LEVEL, LEFT, RIGHT, NOSE DOWN, NOSE UP, BACK
- **Additional Command**: MAV_CMD_ACCELCAL_VEHICLE_POS for position confirmation

#### Compass/Magnetometer
- **Command**: MAV_CMD_PREFLIGHT_CALIBRATION (param2=1)
- **Steps**: ROTATE (continuous rotation until complete)

#### Level Horizon
- **Command**: MAV_CMD_PREFLIGHT_CALIBRATION (param5=2)
- **Steps**: LEVEL (place on flat surface)

#### Pressure/Barometer
- **Command**: MAV_CMD_PREFLIGHT_CALIBRATION (param3=1)
- **Steps**: STILL (keep stationary)

#### Optical Flow
- **Method**: Parameter configuration (FLOW_FXSCALER, FLOW_FYSCALER)
- **Steps**: CONFIGURE (set scale factors via UI)

## MAVLink Command Mapping

The service uses the following MAVLink commands defined in ArduPilot:

- `MAV_CMD_PREFLIGHT_CALIBRATION (241)` - Main calibration command
  - param1: Gyroscope calibration (1=yes, 0=no)
  - param2: Magnetometer calibration (1=yes, 0=no, 76=onboard)
  - param3: Ground pressure calibration (1=yes, 0=no)
  - param4: Airspeed calibration (1=yes, 0=no)
  - param5: Accelerometer calibration (1=simple, 2=level, 4=full 6-axis)

- `MAV_CMD_ACCELCAL_VEHICLE_POS (42429)` - Confirm accelerometer position
  - param1: Position index (1=LEVEL, 2=LEFT, 3=RIGHT, 4=NOSE DOWN, 5=NOSE UP, 6=BACK)

- `MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN (246)` - Reboot flight controller
  - param1: 1=Reboot autopilot

## Integration

The service integrates with existing infrastructure:

- **IConnectionService** - Uses existing MAVLink connection
  - `SendPreflightCalibration()` - Sends calibration commands
  - `SendAccelCalVehiclePos()` - Confirms accelerometer positions
  - `SendPreflightReboot()` - Reboots flight controller

- **Asv.Mavlink v3.9** - MAVLink protocol library (already installed)

## Usage Example

```csharp
// Inject the service
INewCalibrationService calibrationService = serviceProvider.GetRequiredService<INewCalibrationService>();

// Start accelerometer calibration
await calibrationService.StartCalibrationAsync(SensorCategory.Accelerometer, cancellationToken);

// Get current state
var category = calibrationService.GetCategoryState(SensorCategory.Accelerometer);
Console.WriteLine($"Status: {category.Status}");
Console.WriteLine($"Current step: {category.CalibrationSteps[0].Label}");

// User positions vehicle and advances to next step
await calibrationService.NextStepAsync(SensorCategory.Accelerometer, cancellationToken);

// Repeat for all 6 positions...

// Commit calibration
await calibrationService.CommitCalibrationAsync(SensorCategory.Accelerometer, cancellationToken);

// Reboot drone
await calibrationService.RebootDroneAsync(cancellationToken);
```

## State Machine

Each calibration follows this state flow:

1. **NotCalibrated** - Initial state
2. **InProgress** - Calibration started, waiting for steps
3. **Complete** - All steps completed successfully
4. **Error** - Calibration failed or aborted

Individual steps follow:
- **NotCalibrated** - Step not started
- **InProgress** - Currently executing this step
- **Complete** - Step completed
- **Error** - Step failed

## UI Integration

The service is designed to work with the existing UI without modifications:
- UI calls service methods to control calibration
- UI reads `Category` objects to display current state
- UI displays step instructions from `CalibrationStepInfo.InstructionText`
- UI updates based on `Status` and `StepStatus` properties

## Testing

To test the implementation:
1. Connect to a drone via the existing connection UI
2. Navigate to the calibration page
3. Select a sensor category
4. Follow the calibration steps
5. Verify MAVLink commands are sent correctly
6. Confirm calibration completes successfully

## Future Enhancements

Potential improvements:
1. Add telemetry monitoring for real-time validation
2. Implement timeout handling for stuck calibrations
3. Add precondition checks (disarmed, stable connection)
4. Implement retry logic with exponential backoff
5. Add progress percentage tracking
6. Monitor STATUSTEXT messages from flight controller
7. Add postcondition validation after calibration
