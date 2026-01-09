using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Service implementation for motor testing and ESC configuration.
/// Uses MAVLink DO_MOTOR_TEST command (MAV_CMD_DO_MOTOR_TEST = 209).
/// 
/// ArduPilot Parameters:
/// - MOT_PWM_TYPE: Motor output type (Normal, OneShot, DShot, etc.)
/// - MOT_PWM_MIN: Minimum PWM output
/// - MOT_PWM_MAX: Maximum PWM output
/// - MOT_SPIN_ARM: Motor spin when armed
/// - MOT_SPIN_MIN: Minimum motor spin
/// - MOT_SPIN_MAX: Maximum motor spin
/// - MOT_THST_HOVER: Hover throttle
/// - MOT_THST_EXPO: Thrust curve expo
/// - ESC_CALIBRATION: ESC calibration mode
/// </summary>
public class MotorEscService : IMotorEscService
{
    private readonly ILogger<MotorEscService> _logger;
    private readonly IParameterService _parameterService;
    private readonly IConnectionService _connectionService;

    private MotorEscSettings? _cachedSettings;
    private readonly List<MotorStatus> _motorStatuses = new();
    private bool _safetyAcknowledged;

    // Parameter names
    private const string PARAM_MOT_PWM_TYPE = "MOT_PWM_TYPE";
    private const string PARAM_MOT_PWM_MIN = "MOT_PWM_MIN";
    private const string PARAM_MOT_PWM_MAX = "MOT_PWM_MAX";
    private const string PARAM_MOT_SPIN_ARM = "MOT_SPIN_ARM";
    private const string PARAM_MOT_SPIN_MIN = "MOT_SPIN_MIN";
    private const string PARAM_MOT_SPIN_MAX = "MOT_SPIN_MAX";
    private const string PARAM_MOT_THST_HOVER = "MOT_THST_HOVER";
    private const string PARAM_MOT_THST_EXPO = "MOT_THST_EXPO";
    private const string PARAM_MOT_BAT_VOLT_MAX = "MOT_BAT_VOLT_MAX";
    private const string PARAM_MOT_BAT_VOLT_MIN = "MOT_BAT_VOLT_MIN";
    private const string PARAM_MOT_SLEWRATE = "MOT_SLEWRATE";
    private const string PARAM_ESC_CALIBRATION = "ESC_CALIBRATION";
    private const string PARAM_FRAME_CLASS = "FRAME_CLASS";

    public event EventHandler<MotorEscSettings>? SettingsChanged;
    public event EventHandler<MotorStatus>? MotorStatusChanged;
    public event EventHandler<(int MotorNumber, bool Success, string Message)>? MotorTestCompleted;

    public bool IsSafeToTest => _safetyAcknowledged && _connectionService.IsConnected;
    public bool SafetyAcknowledged => _safetyAcknowledged;

    public MotorEscService(
        ILogger<MotorEscService> logger,
        IParameterService parameterService,
        IConnectionService connectionService)
    {
        _logger = logger;
        _parameterService = parameterService;
        _connectionService = connectionService;

        // Initialize motor statuses for up to 8 motors
        for (int i = 1; i <= 8; i++)
        {
            _motorStatuses.Add(new MotorStatus { MotorNumber = i });
        }
    }

    public async Task<MotorEscSettings?> GetSettingsAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot get motor/ESC settings - not connected");
            return null;
        }

        try
        {
            var settings = new MotorEscSettings();

            // Read motor output type
            var pwmType = await _parameterService.GetParameterAsync(PARAM_MOT_PWM_TYPE);
            if (pwmType != null) settings.PwmType = (MotorOutputType)(int)pwmType.Value;

            // Read PWM range
            var pwmMin = await _parameterService.GetParameterAsync(PARAM_MOT_PWM_MIN);
            if (pwmMin != null) settings.PwmMin = (int)pwmMin.Value;

            var pwmMax = await _parameterService.GetParameterAsync(PARAM_MOT_PWM_MAX);
            if (pwmMax != null) settings.PwmMax = (int)pwmMax.Value;

            // Read spin parameters
            var spinArm = await _parameterService.GetParameterAsync(PARAM_MOT_SPIN_ARM);
            if (spinArm != null) settings.SpinArmed = spinArm.Value;

            var spinMin = await _parameterService.GetParameterAsync(PARAM_MOT_SPIN_MIN);
            if (spinMin != null) settings.SpinMin = spinMin.Value;

            var spinMax = await _parameterService.GetParameterAsync(PARAM_MOT_SPIN_MAX);
            if (spinMax != null) settings.SpinMax = spinMax.Value;

            // Read thrust parameters
            var thrustHover = await _parameterService.GetParameterAsync(PARAM_MOT_THST_HOVER);
            if (thrustHover != null) settings.ThrustHover = thrustHover.Value;

            var thrustExpo = await _parameterService.GetParameterAsync(PARAM_MOT_THST_EXPO);
            if (thrustExpo != null) settings.ThrustExpo = thrustExpo.Value;

            // Read battery voltage compensation
            var battVoltMax = await _parameterService.GetParameterAsync(PARAM_MOT_BAT_VOLT_MAX);
            if (battVoltMax != null) settings.BattVoltMax = battVoltMax.Value;

            var battVoltMin = await _parameterService.GetParameterAsync(PARAM_MOT_BAT_VOLT_MIN);
            if (battVoltMin != null) settings.BattVoltMin = battVoltMin.Value;

            // Read slew rate
            var slewRate = await _parameterService.GetParameterAsync(PARAM_MOT_SLEWRATE);
            if (slewRate != null) settings.SlewRate = slewRate.Value;

            // Read ESC calibration
            var escCal = await _parameterService.GetParameterAsync(PARAM_ESC_CALIBRATION);
            if (escCal != null) settings.EscCalibration = (EscCalibrationMode)(int)escCal.Value;

            // Detect motor count from frame class
            settings.MotorCount = await GetMotorCountAsync();

            _cachedSettings = settings;
            SettingsChanged?.Invoke(this, settings);

            _logger.LogInformation("Motor/ESC settings loaded successfully");
            return settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting motor/ESC settings");
            return null;
        }
    }

    public async Task<bool> UpdateSettingsAsync(MotorEscSettings settings)
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot update motor/ESC settings - not connected");
            return false;
        }

        try
        {
            _logger.LogInformation("Updating motor/ESC settings");

            await _parameterService.SetParameterAsync(PARAM_MOT_PWM_TYPE, (float)settings.PwmType);
            await _parameterService.SetParameterAsync(PARAM_MOT_PWM_MIN, settings.PwmMin);
            await _parameterService.SetParameterAsync(PARAM_MOT_PWM_MAX, settings.PwmMax);
            await _parameterService.SetParameterAsync(PARAM_MOT_SPIN_ARM, settings.SpinArmed);
            await _parameterService.SetParameterAsync(PARAM_MOT_SPIN_MIN, settings.SpinMin);
            await _parameterService.SetParameterAsync(PARAM_MOT_SPIN_MAX, settings.SpinMax);
            await _parameterService.SetParameterAsync(PARAM_MOT_THST_HOVER, settings.ThrustHover);
            await _parameterService.SetParameterAsync(PARAM_MOT_THST_EXPO, settings.ThrustExpo);

            if (settings.BattVoltMax > 0)
                await _parameterService.SetParameterAsync(PARAM_MOT_BAT_VOLT_MAX, settings.BattVoltMax);

            if (settings.BattVoltMin > 0)
                await _parameterService.SetParameterAsync(PARAM_MOT_BAT_VOLT_MIN, settings.BattVoltMin);

            await _parameterService.SetParameterAsync(PARAM_MOT_SLEWRATE, settings.SlewRate);

            _cachedSettings = settings;
            SettingsChanged?.Invoke(this, settings);

            _logger.LogInformation("Motor/ESC settings updated successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating motor/ESC settings");
            return false;
        }
    }

    public async Task<bool> StartMotorTestAsync(MotorTestRequest request)
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start motor test - not connected");
            return false;
        }

        if (!_safetyAcknowledged)
        {
            _logger.LogWarning("Cannot start motor test - safety not acknowledged");
            return false;
        }

        try
        {
            _logger.LogInformation("Starting motor test: Motor {Motor}, Throttle {Throttle}%, Duration {Duration}s",
                request.MotorNumber, request.ThrottleValue, request.DurationSeconds);

            // Update motor status
            var status = _motorStatuses.FirstOrDefault(m => m.MotorNumber == request.MotorNumber);
            if (status != null)
            {
                status.IsTesting = true;
                status.TestState = MotorTestState.Testing;
                status.ThrottlePercent = request.ThrottleValue;
                MotorStatusChanged?.Invoke(this, status);
            }

            // Send DO_MOTOR_TEST MAVLink command
            var success = await SendMotorTestCommandAsync(
                request.MotorNumber,
                request.ThrottleType,
                request.ThrottleValue,
                request.DurationSeconds,
                request.TestCount,
                request.TestOrder);

            if (!success)
            {
                if (status != null)
                {
                    status.IsTesting = false;
                    status.TestState = MotorTestState.Failed;
                    MotorStatusChanged?.Invoke(this, status);
                }
                MotorTestCompleted?.Invoke(this, (request.MotorNumber, false, "Failed to send command"));
                return false;
            }

            // Schedule test completion
            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(request.DurationSeconds + 0.5));
                
                if (status != null)
                {
                    status.IsTesting = false;
                    status.TestState = MotorTestState.Completed;
                    status.ThrottlePercent = 0;
                    MotorStatusChanged?.Invoke(this, status);
                }
                MotorTestCompleted?.Invoke(this, (request.MotorNumber, true, "Test completed"));
            });

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting motor test for motor {Motor}", request.MotorNumber);
            return false;
        }
    }

    private async Task<bool> SendMotorTestCommandAsync(
        int motorNumber, 
        MotorTestThrottleType throttleType, 
        float throttleValue, 
        float duration,
        int testCount,
        MotorTestOrder testOrder)
    {
        // DO_MOTOR_TEST (MAV_CMD = 209)
        // param1: Motor instance (1-based)
        // param2: Throttle type (enum MOTOR_TEST_THROTTLE_TYPE)
        // param3: Throttle value (0-100% or PWM value)
        // param4: Timeout in seconds
        // param5: Motor count (0 = single motor)
        // param6: Motor test order
        // param7: Empty

        _connectionService.SendMotorTest(
            motorInstance: motorNumber,
            throttleType: (int)throttleType,
            throttleValue: throttleValue,
            timeout: duration,
            motorCount: testCount,
            testOrder: (int)testOrder);

        // Allow time for command to be sent
        await Task.Delay(100);
        return true;
    }

    public async Task<bool> StopAllMotorTestsAsync()
    {
        _logger.LogInformation("Stopping all motor tests");

        try
        {
            // Send 0 throttle to all motors
            for (int i = 1; i <= (_cachedSettings?.MotorCount ?? 4); i++)
            {
                await SendMotorTestCommandAsync(i, MotorTestThrottleType.ThrottlePercent, 0, 0, 0, MotorTestOrder.Default);
                
                var status = _motorStatuses.FirstOrDefault(m => m.MotorNumber == i);
                if (status != null)
                {
                    status.IsTesting = false;
                    status.TestState = MotorTestState.Idle;
                    status.ThrottlePercent = 0;
                    MotorStatusChanged?.Invoke(this, status);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping motor tests");
            return false;
        }
    }

    public async Task<bool> StopMotorTestAsync(int motorNumber)
    {
        _logger.LogInformation("Stopping motor test for motor {Motor}", motorNumber);

        try
        {
            await SendMotorTestCommandAsync(motorNumber, MotorTestThrottleType.ThrottlePercent, 0, 0, 0, MotorTestOrder.Default);
            
            var status = _motorStatuses.FirstOrDefault(m => m.MotorNumber == motorNumber);
            if (status != null)
            {
                status.IsTesting = false;
                status.TestState = MotorTestState.Idle;
                status.ThrottlePercent = 0;
                MotorStatusChanged?.Invoke(this, status);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping motor test for motor {Motor}", motorNumber);
            return false;
        }
    }

    public async Task<bool> TestAllMotorsSequentialAsync(float throttlePercent, float durationSeconds, int delayBetweenMs = 500)
    {
        if (!_safetyAcknowledged)
        {
            _logger.LogWarning("Cannot test motors - safety not acknowledged");
            return false;
        }

        var motorCount = _cachedSettings?.MotorCount ?? 4;
        _logger.LogInformation("Testing all {Count} motors sequentially at {Throttle}%", motorCount, throttlePercent);

        try
        {
            for (int i = 1; i <= motorCount; i++)
            {
                var request = new MotorTestRequest
                {
                    MotorNumber = i,
                    ThrottleType = MotorTestThrottleType.ThrottlePercent,
                    ThrottleValue = throttlePercent,
                    DurationSeconds = durationSeconds
                };

                await StartMotorTestAsync(request);
                await Task.Delay(TimeSpan.FromSeconds(durationSeconds) + TimeSpan.FromMilliseconds(delayBetweenMs));
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during sequential motor test");
            return false;
        }
    }

    public async Task<bool> StartEscCalibrationAsync()
    {
        if (!_connectionService.IsConnected)
        {
            _logger.LogWarning("Cannot start ESC calibration - not connected");
            return false;
        }

        try
        {
            _logger.LogInformation("Starting ESC calibration - setting ESC_CALIBRATION to PassthroughOnNextBoot");
            
            // ESC_CALIBRATION = 3 (PassthroughOnNextBoot) means calibration will happen on next power cycle
            var success = await _parameterService.SetParameterAsync(PARAM_ESC_CALIBRATION, (float)EscCalibrationMode.PassthroughOnNextBoot);
            
            if (success)
            {
                _logger.LogInformation("ESC calibration armed - power cycle vehicle to complete calibration");
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting ESC calibration");
            return false;
        }
    }

    public async Task<bool> CancelEscCalibrationAsync()
    {
        try
        {
            _logger.LogInformation("Cancelling ESC calibration");
            return await _parameterService.SetParameterAsync(PARAM_ESC_CALIBRATION, 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling ESC calibration");
            return false;
        }
    }

    public async Task<int> GetMotorCountAsync()
    {
        // Detect motor count from FRAME_CLASS parameter
        var frameClass = await _parameterService.GetParameterAsync(PARAM_FRAME_CLASS);
        
        if (frameClass == null) return 4; // Default to quad

        return (int)frameClass.Value switch
        {
            0 => 4,  // Undefined - default to quad
            1 => 4,  // Quad
            2 => 6,  // Hexa
            3 => 8,  // Octa
            4 => 8,  // OctaQuad
            5 => 6,  // Y6 (has 6 motors)
            6 => 1,  // Heli (single rotor)
            7 => 3,  // Tri
            8 => 1,  // SingleCopter
            9 => 2,  // CoaxCopter
            10 => 2, // BiCopter
            11 => 2, // Heli_Dual
            12 => 12,// DodecaHexa (12 motors)
            13 => 4, // HeliQuad
            14 => 10,// Deca (10 motors)
            _ => 4   // Default quad
        };
    }

    public async Task<bool> SetMotorOutputTypeAsync(MotorOutputType outputType)
    {
        _logger.LogInformation("Setting motor output type to {Type}", outputType);
        return await _parameterService.SetParameterAsync(PARAM_MOT_PWM_TYPE, (float)outputType);
    }

    public async Task<bool> SetPwmRangeAsync(int minPwm, int maxPwm)
    {
        _logger.LogInformation("Setting PWM range: {Min} - {Max}", minPwm, maxPwm);
        
        var success1 = await _parameterService.SetParameterAsync(PARAM_MOT_PWM_MIN, minPwm);
        var success2 = await _parameterService.SetParameterAsync(PARAM_MOT_PWM_MAX, maxPwm);
        
        return success1 && success2;
    }

    public Task<List<MotorStatus>> GetAllMotorStatusAsync()
    {
        var motorCount = _cachedSettings?.MotorCount ?? 4;
        return Task.FromResult(_motorStatuses.Take(motorCount).ToList());
    }

    public void AcknowledgeSafetyWarning(bool propsRemoved)
    {
        _safetyAcknowledged = propsRemoved;
        _logger.LogInformation("Safety acknowledgement: {Acknowledged}", propsRemoved);
    }

    public MotorEscSettings GetRecommendedSettings(int motorCount, MotorOutputType outputType)
    {
        return new MotorEscSettings
        {
            MotorCount = motorCount,
            PwmType = outputType,
            PwmMin = outputType == MotorOutputType.Normal ? 1000 : 1000,
            PwmMax = outputType == MotorOutputType.Normal ? 2000 : 2000,
            SpinArmed = 0.1f,
            SpinMin = 0.15f,
            SpinMax = 0.95f,
            ThrustHover = 0.35f,
            ThrustExpo = 0.65f,
            SlewRate = 0,
            EscCalibration = EscCalibrationMode.Disabled
        };
    }

    public List<string> ValidateSettings(MotorEscSettings settings)
    {
        var warnings = new List<string>();

        if (settings.PwmMin >= settings.PwmMax)
        {
            warnings.Add("PWM Min must be less than PWM Max");
        }

        if (settings.PwmMin < 900 || settings.PwmMin > 1200)
        {
            warnings.Add("PWM Min should typically be between 900-1200");
        }

        if (settings.PwmMax < 1800 || settings.PwmMax > 2100)
        {
            warnings.Add("PWM Max should typically be between 1800-2100");
        }

        if (settings.SpinArmed > settings.SpinMin)
        {
            warnings.Add("Spin Armed should be less than Spin Min");
        }

        if (settings.SpinMin > settings.SpinMax)
        {
            warnings.Add("Spin Min should be less than Spin Max");
        }

        if (settings.ThrustHover < 0.1f || settings.ThrustHover > 0.8f)
        {
            warnings.Add("Thrust Hover should be between 0.1 and 0.8");
        }

        return warnings;
    }
}
