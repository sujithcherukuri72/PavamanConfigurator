using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Calibration validation helper for diagnostic purposes.
/// 
/// CRITICAL: Firmware is the SINGLE SOURCE OF TRUTH for all calibration validation.
/// This class provides diagnostic information ONLY - it does NOT make pass/fail decisions.
/// All actual validation is performed by ArduPilot firmware.
/// 
/// Validation helpers for:
/// - Accelerometer: Gravity magnitude, orientation consistency
/// - Compass: Coverage, interference detection, Earth field strength
/// - Gyroscope: Bias levels, noise
/// - Barometer: Pressure variance, stability
/// </summary>
public class CalibrationValidationHelper
{
    private readonly ILogger<CalibrationValidationHelper> _logger;

    // Physical constants
    private const float GRAVITY_MAGNITUDE = 9.80665f; // m/s²
    private const float GRAVITY_TOLERANCE_PERCENT = 5.0f; // ±5%
    
    // Earth's magnetic field typical range (in milliGauss)
    private const float EARTH_FIELD_MIN = 250f;
    private const float EARTH_FIELD_MAX = 650f;

    public CalibrationValidationHelper(ILogger<CalibrationValidationHelper> logger)
    {
        _logger = logger;
    }

    #region Accelerometer Validation (Diagnostic Only)

    /// <summary>
    /// Validates accelerometer calibration results for diagnostic purposes.
    /// DOES NOT determine calibration success - firmware does that.
    /// </summary>
    public AccelCalibrationDiagnostic ValidateAccelCalibration(
        (float X, float Y, float Z) offsets,
        (float X, float Y, float Z) scales)
    {
        var diagnostic = new AccelCalibrationDiagnostic
        {
            OffsetsX = offsets.X,
            OffsetsY = offsets.Y,
            OffsetsZ = offsets.Z,
            ScalesX = scales.X,
            ScalesY = scales.Y,
            ScalesZ = scales.Z
        };

        // Check offset magnitudes (typical range: ±2.0 m/s²)
        var maxOffset = Math.Max(Math.Abs(offsets.X), Math.Max(Math.Abs(offsets.Y), Math.Abs(offsets.Z)));
        if (maxOffset > 2.0f)
        {
            diagnostic.AddWarning($"Large offset detected: {maxOffset:F2} m/s². Sensor may have hardware issues.");
        }

        // Check scale factors (should be close to 1.0, typical range: 0.95-1.05)
        if (scales.X < 0.9f || scales.X > 1.1f ||
            scales.Y < 0.9f || scales.Y > 1.1f ||
            scales.Z < 0.9f || scales.Z > 1.1f)
        {
            diagnostic.AddWarning($"Unusual scale factors: X={scales.X:F3}, Y={scales.Y:F3}, Z={scales.Z:F3}");
        }

        // Check if calibration data exists
        if (offsets.X == 0 && offsets.Y == 0 && offsets.Z == 0 &&
            scales.X == 1.0f && scales.Y == 1.0f && scales.Z == 1.0f)
        {
            diagnostic.AddInfo("Accelerometer appears uncalibrated (default values).");
        }

        diagnostic.IsValid = diagnostic.Errors.Count == 0;
        return diagnostic;
    }

    /// <summary>
    /// Validates a single accelerometer position during calibration.
    /// Checks gravity magnitude is approximately 1G.
    /// </summary>
    public AccelPositionDiagnostic ValidateAccelPosition(
        int position, 
        float accelX, 
        float accelY, 
        float accelZ)
    {
        var result = new AccelPositionDiagnostic
        {
            Position = position,
            AccelX = accelX,
            AccelY = accelY,
            AccelZ = accelZ
        };

        // Calculate gravity magnitude
        result.GravityMagnitude = MathF.Sqrt(accelX * accelX + accelY * accelY + accelZ * accelZ);
        
        // Check gravity magnitude (should be ~9.8 m/s²)
        var expectedGravity = GRAVITY_MAGNITUDE;
        var toleranceLow = expectedGravity * (1 - GRAVITY_TOLERANCE_PERCENT / 100);
        var toleranceHigh = expectedGravity * (1 + GRAVITY_TOLERANCE_PERCENT / 100);

        if (result.GravityMagnitude < toleranceLow || result.GravityMagnitude > toleranceHigh)
        {
            result.AddWarning($"Gravity magnitude {result.GravityMagnitude:F2} m/s² outside expected range " +
                             $"({toleranceLow:F2} - {toleranceHigh:F2})");
        }

        // Check which axis should be dominant for each position
        ValidatePositionOrientation(result, position, accelX, accelY, accelZ);

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    private void ValidatePositionOrientation(AccelPositionDiagnostic result, int position, 
        float x, float y, float z)
    {
        // Expected dominant axis for each position:
        // 1=Level: -Z dominant (gravity down)
        // 2=Left: +Y dominant
        // 3=Right: -Y dominant
        // 4=NoseDown: +X dominant
        // 5=NoseUp: -X dominant
        // 6=Back: +Z dominant

        var absX = MathF.Abs(x);
        var absY = MathF.Abs(y);
        var absZ = MathF.Abs(z);
        var dominant = Math.Max(absX, Math.Max(absY, absZ));
        var threshold = GRAVITY_MAGNITUDE * 0.7f; // At least 70% of gravity

        bool orientationCorrect = position switch
        {
            1 => absZ > threshold && z < 0, // Level: -Z
            2 => absY > threshold && y > 0, // Left: +Y
            3 => absY > threshold && y < 0, // Right: -Y
            4 => absX > threshold && x > 0, // NoseDown: +X
            5 => absX > threshold && x < 0, // NoseUp: -X
            6 => absZ > threshold && z > 0, // Back: +Z
            _ => true
        };

        if (!orientationCorrect)
        {
            result.AddWarning($"Position {position} orientation may be incorrect. " +
                             $"Accel: X={x:F2}, Y={y:F2}, Z={z:F2}");
        }
    }

    #endregion

    #region Compass Validation (Diagnostic Only)

    /// <summary>
    /// Validates compass calibration results for diagnostic purposes.
    /// </summary>
    public CompassCalibrationDiagnostic ValidateCompassCalibration(
        int compassIndex,
        (float X, float Y, float Z) offsets,
        float fitness)
    {
        var diagnostic = new CompassCalibrationDiagnostic
        {
            CompassIndex = compassIndex,
            OffsetsX = offsets.X,
            OffsetsY = offsets.Y,
            OffsetsZ = offsets.Z,
            Fitness = fitness
        };

        // Calculate offset magnitude
        diagnostic.OffsetMagnitude = MathF.Sqrt(
            offsets.X * offsets.X + 
            offsets.Y * offsets.Y + 
            offsets.Z * offsets.Z);

        // Check offset magnitude (typical acceptable: <600 milliGauss for internal, <1500 for external)
        if (diagnostic.OffsetMagnitude > 1500)
        {
            diagnostic.AddWarning($"Large compass offset magnitude: {diagnostic.OffsetMagnitude:F0} mG. " +
                                 "Check for magnetic interference.");
        }
        else if (diagnostic.OffsetMagnitude > 600)
        {
            diagnostic.AddInfo($"Moderate compass offset: {diagnostic.OffsetMagnitude:F0} mG. " +
                              "May be acceptable for external compass.");
        }

        // Check fitness (lower is better, typical acceptable: <25)
        if (fitness > 50)
        {
            diagnostic.AddWarning($"Poor calibration fitness: {fitness:F1}. Recommend recalibration.");
        }
        else if (fitness > 25)
        {
            diagnostic.AddInfo($"Moderate calibration fitness: {fitness:F1}. May be acceptable.");
        }

        // Check if calibration data exists
        if (offsets.X == 0 && offsets.Y == 0 && offsets.Z == 0)
        {
            diagnostic.AddInfo("Compass appears uncalibrated (zero offsets).");
        }

        diagnostic.IsValid = diagnostic.Errors.Count == 0;
        return diagnostic;
    }

    /// <summary>
    /// Validates Earth field strength during compass calibration.
    /// </summary>
    public bool ValidateEarthFieldStrength(float fieldMagnitude, out string message)
    {
        if (fieldMagnitude < EARTH_FIELD_MIN)
        {
            message = $"Magnetic field too weak ({fieldMagnitude:F0} mG). " +
                     $"Expected {EARTH_FIELD_MIN}-{EARTH_FIELD_MAX} mG. Check for shielding.";
            return false;
        }
        
        if (fieldMagnitude > EARTH_FIELD_MAX)
        {
            message = $"Magnetic field too strong ({fieldMagnitude:F0} mG). " +
                     $"Expected {EARTH_FIELD_MIN}-{EARTH_FIELD_MAX} mG. Magnetic interference present.";
            return false;
        }

        message = $"Earth field strength normal: {fieldMagnitude:F0} mG.";
        return true;
    }

    #endregion

    #region Level Horizon Validation (Diagnostic Only)

    /// <summary>
    /// Validates level horizon calibration results.
    /// </summary>
    public LevelCalibrationDiagnostic ValidateLevelCalibration(float trimX, float trimY)
    {
        var diagnostic = new LevelCalibrationDiagnostic
        {
            TrimX = trimX,
            TrimY = trimY
        };

        // Convert to degrees (trims are in radians)
        var rollDeg = trimX * 180.0f / MathF.PI;
        var pitchDeg = trimY * 180.0f / MathF.PI;
        
        diagnostic.RollDegrees = rollDeg;
        diagnostic.PitchDegrees = pitchDeg;

        // Check trim magnitudes (typical acceptable: ±5 degrees)
        if (MathF.Abs(rollDeg) > 5.0f)
        {
            diagnostic.AddWarning($"Large roll trim: {rollDeg:F2}°. Vehicle frame may not be level.");
        }

        if (MathF.Abs(pitchDeg) > 5.0f)
        {
            diagnostic.AddWarning($"Large pitch trim: {pitchDeg:F2}°. Vehicle frame may not be level.");
        }

        // Check if calibrated
        if (trimX == 0 && trimY == 0)
        {
            diagnostic.AddInfo("Level horizon appears uncalibrated or vehicle is perfectly level.");
        }

        diagnostic.IsValid = diagnostic.Errors.Count == 0;
        return diagnostic;
    }

    #endregion

    #region Barometer Validation (Diagnostic Only)

    /// <summary>
    /// Validates barometer calibration results.
    /// </summary>
    public BaroCalibrationDiagnostic ValidateBaroCalibration(float groundPressure)
    {
        var diagnostic = new BaroCalibrationDiagnostic
        {
            GroundPressure = groundPressure
        };

        // Sea level standard pressure: 101325 Pa (1013.25 hPa)
        // Typical range at ground level: 870-1084 hPa (87000-108400 Pa)
        var pressureHPa = groundPressure / 100.0f;

        if (pressureHPa < 870 || pressureHPa > 1084)
        {
            diagnostic.AddWarning($"Ground pressure {pressureHPa:F1} hPa outside normal range (870-1084 hPa).");
        }

        if (groundPressure == 0)
        {
            diagnostic.AddInfo("Barometer appears uncalibrated (zero ground pressure).");
        }

        diagnostic.IsValid = diagnostic.Errors.Count == 0;
        return diagnostic;
    }

    #endregion
}

#region Diagnostic Result Classes

/// <summary>
/// Base class for calibration diagnostics.
/// </summary>
public abstract class CalibrationDiagnosticBase
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public List<string> Info { get; } = new();

    public void AddError(string message) => Errors.Add(message);
    public void AddWarning(string message) => Warnings.Add(message);
    public void AddInfo(string message) => Info.Add(message);
}

/// <summary>
/// Accelerometer calibration diagnostic results.
/// </summary>
public class AccelCalibrationDiagnostic : CalibrationDiagnosticBase
{
    public float OffsetsX { get; set; }
    public float OffsetsY { get; set; }
    public float OffsetsZ { get; set; }
    public float ScalesX { get; set; }
    public float ScalesY { get; set; }
    public float ScalesZ { get; set; }
}

/// <summary>
/// Single position validation result during accel calibration.
/// </summary>
public class AccelPositionDiagnostic : CalibrationDiagnosticBase
{
    public int Position { get; set; }
    public float AccelX { get; set; }
    public float AccelY { get; set; }
    public float AccelZ { get; set; }
    public float GravityMagnitude { get; set; }
}

/// <summary>
/// Compass calibration diagnostic results.
/// </summary>
public class CompassCalibrationDiagnostic : CalibrationDiagnosticBase
{
    public int CompassIndex { get; set; }
    public float OffsetsX { get; set; }
    public float OffsetsY { get; set; }
    public float OffsetsZ { get; set; }
    public float OffsetMagnitude { get; set; }
    public float Fitness { get; set; }
}

/// <summary>
/// Level horizon calibration diagnostic results.
/// </summary>
public class LevelCalibrationDiagnostic : CalibrationDiagnosticBase
{
    public float TrimX { get; set; }
    public float TrimY { get; set; }
    public float RollDegrees { get; set; }
    public float PitchDegrees { get; set; }
}

/// <summary>
/// Barometer calibration diagnostic results.
/// </summary>
public class BaroCalibrationDiagnostic : CalibrationDiagnosticBase
{
    public float GroundPressure { get; set; }
}

#endregion
