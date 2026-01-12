using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.Infrastructure.Services;

/// <summary>
/// Validates accelerometer positions by checking actual IMU readings.
/// This ensures the vehicle is actually in the correct orientation.
/// 
/// IMPORTANT: This is CRITICAL for flight safety. Bad accel calibration = crash risk.
/// </summary>
public class AccelPositionValidator
{
    private readonly ILogger<AccelPositionValidator> _logger;
    private readonly IConnectionService _connectionService;
    
    // Gravity constant (m/s²)
    private const double GRAVITY = 9.81;
    
    // Tolerance for axis alignment (degrees)
    private const double ANGLE_TOLERANCE_DEG = 15.0;
    
    // Expected accelerations for each position (in terms of gravity: 1G = 9.81 m/s²)
    private static readonly Dictionary<int, ExpectedAcceleration> ExpectedReadings = new()
    {
        // Position 1: LEVEL - Z should read +1G (up), X and Y near 0
        [1] = new ExpectedAcceleration { X = 0, Y = 0, Z = 1.0, Name = "LEVEL" },
        
        // Position 2: LEFT SIDE - Y should read +1G (left side down), X and Z near 0
        [2] = new ExpectedAcceleration { X = 0, Y = -1.0, Z = 0, Name = "LEFT" },
        
        // Position 3: RIGHT SIDE - Y should read -1G (right side down), X and Z near 0
        [3] = new ExpectedAcceleration { X = 0, Y = 1.0, Z = 0, Name = "RIGHT" },
        
        // Position 4: NOSE DOWN - X should read -1G (nose down), Y and Z near 0
        [4] = new ExpectedAcceleration { X = -1.0, Y = 0, Z = 0, Name = "NOSE DOWN" },
        
        // Position 5: NOSE UP - X should read +1G (nose up), Y and Z near 0
        [5] = new ExpectedAcceleration { X = 1.0, Y = 0, Z = 0, Name = "NOSE UP" },
        
        // Position 6: BACK (upside down) - Z should read -1G (down), X and Y near 0
        [6] = new ExpectedAcceleration { X = 0, Y = 0, Z = -1.0, Name = "BACK" }
    };
    
    public AccelPositionValidator(
        ILogger<AccelPositionValidator> logger,
        IConnectionService connectionService)
    {
        _logger = logger;
        _connectionService = connectionService;
    }
    
    /// <summary>
    /// Validates if the current IMU reading matches the expected position.
    /// Returns validation result with detailed feedback.
    /// </summary>
    public PositionValidationResult ValidatePosition(int position, ImuData? imuData)
    {
        if (!ExpectedReadings.TryGetValue(position, out var expected))
        {
            return new PositionValidationResult
            {
                IsValid = false,
                ErrorMessage = $"Invalid position number: {position}",
                Position = position
            };
        }
        
        if (imuData == null)
        {
            return new PositionValidationResult
            {
                IsValid = false,
                ErrorMessage = "No IMU data available. Check sensor connection.",
                Position = position,
                PositionName = expected.Name
            };
        }
        
        // Check if reading matches expected orientation
        var result = CheckOrientation(position, expected, imuData);
        
        _logger.LogInformation(
            "Position {Position} ({Name}) validation: {Result} | " +
            "IMU=[{X:F2}, {Y:F2}, {Z:F2}] m/s² | " +
            "Expected=[{ExpX:F2}, {ExpY:F2}, {ExpZ:F2}]G",
            position, expected.Name, result.IsValid ? "PASS" : "FAIL",
            imuData.AccelX, imuData.AccelY, imuData.AccelZ,
            expected.X, expected.Y, expected.Z);
        
        return result;
    }
    
    private PositionValidationResult CheckOrientation(
        int position, 
        ExpectedAcceleration expected, 
        ImuData imuData)
    {
        var result = new PositionValidationResult
        {
            Position = position,
            PositionName = expected.Name,
            ActualAccelX = imuData.AccelX,
            ActualAccelY = imuData.AccelY,
            ActualAccelZ = imuData.AccelZ
        };
        
        // Convert expected G values to m/s²
        var expectedX = expected.X * GRAVITY;
        var expectedY = expected.Y * GRAVITY;
        var expectedZ = expected.Z * GRAVITY;
        
        // Calculate errors
        var errorX = Math.Abs(imuData.AccelX - expectedX);
        var errorY = Math.Abs(imuData.AccelY - expectedY);
        var errorZ = Math.Abs(imuData.AccelZ - expectedZ);
        
        // Maximum allowed error per axis (m/s²) - allow ~25% deviation
        const double maxErrorPerAxis = GRAVITY * 0.25;
        
        // Check primary axis (the one that should read ±1G)
        bool primaryAxisCorrect = false;
        string primaryAxisName = "";
        double primaryError = 0;
        
        if (Math.Abs(expected.X) > 0.5) // X is primary
        {
            primaryAxisName = "X";
            primaryError = errorX;
            primaryAxisCorrect = errorX < maxErrorPerAxis;
        }
        else if (Math.Abs(expected.Y) > 0.5) // Y is primary
        {
            primaryAxisName = "Y";
            primaryError = errorY;
            primaryAxisCorrect = errorY < maxErrorPerAxis;
        }
        else if (Math.Abs(expected.Z) > 0.5) // Z is primary
        {
            primaryAxisName = "Z";
            primaryError = errorZ;
            primaryAxisCorrect = errorZ < maxErrorPerAxis;
        }
        
        // Check that other axes are near zero
        var otherAxesCorrect = true;
        var problems = new List<string>();
        
        if (Math.Abs(expected.X) < 0.5 && errorX > maxErrorPerAxis)
        {
            otherAxesCorrect = false;
            problems.Add($"X-axis too tilted ({imuData.AccelX:F2} m/s², expected ~0)");
        }
        
        if (Math.Abs(expected.Y) < 0.5 && errorY > maxErrorPerAxis)
        {
            otherAxesCorrect = false;
            problems.Add($"Y-axis too tilted ({imuData.AccelY:F2} m/s², expected ~0)");
        }
        
        if (Math.Abs(expected.Z) < 0.5 && errorZ > maxErrorPerAxis)
        {
            otherAxesCorrect = false;
            problems.Add($"Z-axis too tilted ({imuData.AccelZ:F2} m/s², expected ~0)");
        }
        
        // Calculate overall magnitude - should be close to 1G
        var magnitude = Math.Sqrt(
            imuData.AccelX * imuData.AccelX +
            imuData.AccelY * imuData.AccelY +
            imuData.AccelZ * imuData.AccelZ);
        
        var magnitudeError = Math.Abs(magnitude - GRAVITY);
        bool magnitudeCorrect = magnitudeError < GRAVITY * 0.15; // Allow 15% error
        
        // Determine if position is valid
        result.IsValid = primaryAxisCorrect && otherAxesCorrect && magnitudeCorrect;
        
        if (!result.IsValid)
        {
            var message = $"Position {position} ({expected.Name}) INCORRECT:\n";
            
            if (!primaryAxisCorrect)
            {
                var sign = expected.GetPrimaryAxisValue() > 0 ? "positive" : "negative";
                message += $"• {primaryAxisName}-axis should be {sign} 1G " +
                          $"(expected {expectedX:F1}, {expectedY:F1}, {expectedZ:F1} m/s², " +
                          $"got {imuData.AccelX:F1}, {imuData.AccelY:F1}, {imuData.AccelZ:F1})\n";
            }
            
            if (!otherAxesCorrect)
            {
                message += $"• Vehicle is tilted: {string.Join(", ", problems)}\n";
            }
            
            if (!magnitudeCorrect)
            {
                message += $"• Acceleration magnitude wrong: {magnitude:F2} m/s² (expected ~{GRAVITY:F2})\n";
            }
            
            message += GetCorrectionAdvice(position, imuData);
            result.ErrorMessage = message;
        }
        else
        {
            result.ErrorMessage = $"Position {position} ({expected.Name}) verified correctly.";
        }
        
        return result;
    }
    
    private string GetCorrectionAdvice(int position, ImuData imuData)
    {
        return position switch
        {
            1 => "\n?? For LEVEL: Place on flat surface, ensure all 4 corners touch evenly",
            2 => "\n?? For LEFT: Place on left side, nose should point forward",
            3 => "\n?? For RIGHT: Place on right side, nose should point forward",
            4 => "\n?? For NOSE DOWN: Tilt forward 90°, rear should point up",
            5 => "\n?? For NOSE UP: Tilt backward 90°, rear should point down",
            6 => "\n?? For BACK: Flip completely upside down, top should face ground",
            _ => ""
        };
    }
    
    private class ExpectedAcceleration
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public string Name { get; set; } = "";
        
        public double GetPrimaryAxisValue()
        {
            if (Math.Abs(X) > 0.5) return X;
            if (Math.Abs(Y) > 0.5) return Y;
            if (Math.Abs(Z) > 0.5) return Z;
            return 0;
        }
    }
}

/// <summary>
/// Result of position validation.
/// </summary>
public class PositionValidationResult
{
    public bool IsValid { get; set; }
    public int Position { get; set; }
    public string PositionName { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public double ActualAccelX { get; set; }
    public double ActualAccelY { get; set; }
    public double ActualAccelZ { get; set; }
}

/// <summary>
/// IMU sensor data.
/// </summary>
public class ImuData
{
    public double AccelX { get; set; } // m/s²
    public double AccelY { get; set; } // m/s²
    public double AccelZ { get; set; } // m/s²
    public double GyroX { get; set; }  // rad/s
    public double GyroY { get; set; }  // rad/s
    public double GyroZ { get; set; }  // rad/s
    public double Temperature { get; set; } // °C
    public uint TimeBootMs { get; set; }
}
