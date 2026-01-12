using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Infrastructure.MAVLink;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Validates accelerometer orientation using IMU data.
/// 
/// CRITICAL SAFETY: This validator prevents bad calibration data from being sent to FC.
/// Incorrect accelerometer calibration can cause CRASHES.
/// 
/// Validation logic:
/// - Checks gravity vector magnitude (~9.81 m/s²)
/// - Checks gravity vector direction matches expected axis
/// - Rejects incorrect orientations
/// </summary>
public class AccelImuValidator
{
    private readonly ILogger<AccelImuValidator> _logger;
    
    // Physical constants
    private const double GRAVITY = 9.81; // m/s²
    private const double GRAVITY_TOLERANCE_PERCENT = 15.0; // ±15% tolerance
    private const double AXIS_ALIGNMENT_THRESHOLD = 0.7; // 70% of gravity on correct axis
    
    public AccelImuValidator(ILogger<AccelImuValidator> logger)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Validate position using IMU accelerometer data.
    /// Returns validation result with pass/fail and error message.
    /// </summary>
    public AccelValidationResult ValidatePosition(int position, RawImuData imuData)
    {
        if (position < 1 || position > 6)
        {
            return new AccelValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Invalid position number: {position}"
            };
        }
        
        // Convert raw IMU to m/s²
        var accel = imuData.GetAcceleration();
        
        _logger.LogDebug("Validating position {Position}: accel=({X:F2}, {Y:F2}, {Z:F2}) m/s²",
            position, accel.X, accel.Y, accel.Z);
        
        // Calculate gravity magnitude
        var magnitude = Math.Sqrt(accel.X * accel.X + accel.Y * accel.Y + accel.Z * accel.Z);
        
        // Check magnitude is approximately 1G
        var expectedGravity = GRAVITY;
        var toleranceLow = expectedGravity * (1 - GRAVITY_TOLERANCE_PERCENT / 100);
        var toleranceHigh = expectedGravity * (1 + GRAVITY_TOLERANCE_PERCENT / 100);
        
        if (magnitude < toleranceLow || magnitude > toleranceHigh)
        {
            var message = $"Position {position} ({GetPositionName(position)}): " +
                         $"Gravity magnitude {magnitude:F2} m/s² outside expected range " +
                         $"({toleranceLow:F2} - {toleranceHigh:F2} m/s²). " +
                         $"Check sensor or vehicle stability.";
            
            _logger.LogWarning(message);
            
            return new AccelValidationResult
            {
                IsValid = false,
                ErrorMessage = message,
                MeasuredMagnitude = magnitude
            };
        }
        
        // Check axis alignment for this position
        var alignmentResult = CheckAxisAlignment(position, accel.X, accel.Y, accel.Z);
        
        if (!alignmentResult.IsValid)
        {
            _logger.LogWarning("Position {Position} alignment check FAILED: {Message}",
                position, alignmentResult.ErrorMessage);
            
            return new AccelValidationResult
            {
                IsValid = false,
                ErrorMessage = alignmentResult.ErrorMessage,
                MeasuredMagnitude = magnitude,
                MeasuredX = accel.X,
                MeasuredY = accel.Y,
                MeasuredZ = accel.Z
            };
        }
        
        // Validation PASSED
        var successMessage = $"Position {position} ({GetPositionName(position)}) verified correctly.";
        
        _logger.LogInformation("Position {Position} validation PASSED: mag={Mag:F2} m/s², " +
                              "accel=({X:F2}, {Y:F2}, {Z:F2})",
                              position, magnitude, accel.X, accel.Y, accel.Z);
        
        return new AccelValidationResult
        {
            IsValid = true,
            ErrorMessage = successMessage,
            MeasuredMagnitude = magnitude,
            MeasuredX = accel.X,
            MeasuredY = accel.Y,
            MeasuredZ = accel.Z
        };
    }
    
    /// <summary>
    /// Check that gravity vector is aligned with expected axis for this position.
    /// </summary>
    private AccelValidationResult CheckAxisAlignment(int position, double x, double y, double z)
    {
        var absX = Math.Abs(x);
        var absY = Math.Abs(y);
        var absZ = Math.Abs(z);
        var threshold = GRAVITY * AXIS_ALIGNMENT_THRESHOLD;
        
        // Expected orientations:
        // 1. LEVEL:      Z ? +1G (vehicle upright, gravity pulls down)
        // 2. LEFT:       Y ? -1G (vehicle on left side)
        // 3. RIGHT:      Y ? +1G (vehicle on right side)
        // 4. NOSE DOWN:  X ? +1G (nose pointing down)
        // 5. NOSE UP:    X ? -1G (nose pointing up)
        // 6. BACK:       Z ? -1G (vehicle upside down)
        
        bool isCorrect = position switch
        {
            1 => absZ > threshold && z > 0, // LEVEL: +Z dominant
            2 => absY > threshold && y < 0, // LEFT: -Y dominant
            3 => absY > threshold && y > 0, // RIGHT: +Y dominant
            4 => absX > threshold && x > 0, // NOSE DOWN: +X dominant
            5 => absX > threshold && x < 0, // NOSE UP: -X dominant
            6 => absZ > threshold && z < 0, // BACK: -Z dominant
            _ => false
        };
        
        if (!isCorrect)
        {
            var expectedAxis = position switch
            {
                1 => "+Z (upward)",
                2 => "-Y (left side down)",
                3 => "+Y (right side down)",
                4 => "+X (nose down)",
                5 => "-X (nose up)",
                6 => "-Z (upside down)",
                _ => "unknown"
            };
            
            var message = $"Position {position} ({GetPositionName(position)}) INCORRECT:\n" +
                         $"Expected gravity on {expectedAxis} axis.\n" +
                         $"Measured: X={x:F2}, Y={y:F2}, Z={z:F2} m/s²\n" +
                         GetCorrectionAdvice(position);
            
            return new AccelValidationResult
            {
                IsValid = false,
                ErrorMessage = message
            };
        }
        
        return new AccelValidationResult
        {
            IsValid = true
        };
    }
    
    private static string GetPositionName(int position)
    {
        return position switch
        {
            1 => "LEVEL",
            2 => "LEFT",
            3 => "RIGHT",
            4 => "NOSE DOWN",
            5 => "NOSE UP",
            6 => "BACK",
            _ => "UNKNOWN"
        };
    }
    
    private static string GetCorrectionAdvice(int position)
    {
        return position switch
        {
            1 => "?? For LEVEL: Place on flat surface, ensure all corners touch evenly.",
            2 => "?? For LEFT: Place on left side, nose should point forward.",
            3 => "?? For RIGHT: Place on right side, nose should point forward.",
            4 => "?? For NOSE DOWN: Tilt forward 90°, rear should point up.",
            5 => "?? For NOSE UP: Tilt backward 90°, rear should point down.",
            6 => "?? For BACK: Flip completely upside down, top should face ground.",
            _ => ""
        };
    }
}

/// <summary>
/// Result of IMU-based position validation.
/// </summary>
public class AccelValidationResult
{
    /// <summary>Validation passed</summary>
    public bool IsValid { get; set; }
    
    /// <summary>Error or success message</summary>
    public string ErrorMessage { get; set; } = "";
    
    /// <summary>Measured gravity magnitude (m/s²)</summary>
    public double MeasuredMagnitude { get; set; }
    
    /// <summary>Measured X acceleration (m/s²)</summary>
    public double MeasuredX { get; set; }
    
    /// <summary>Measured Y acceleration (m/s²)</summary>
    public double MeasuredY { get; set; }
    
    /// <summary>Measured Z acceleration (m/s²)</summary>
    public double MeasuredZ { get; set; }
}
