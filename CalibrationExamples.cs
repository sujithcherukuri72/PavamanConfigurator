using System;
using System.Threading;
using System.Threading.Tasks;
using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;

namespace PavamanDroneConfigurator.Examples;

/// <summary>
/// Example usage of the new calibration service
/// This demonstrates how to perform sensor calibration according to the UI data model
/// </summary>
public class CalibrationExample
{
    private readonly INewCalibrationService _calibrationService;
    private readonly IConnectionService _connectionService;

    public CalibrationExample(
        INewCalibrationService calibrationService,
        IConnectionService connectionService)
    {
        _calibrationService = calibrationService;
        _connectionService = connectionService;
    }

    /// <summary>
    /// Example: Complete accelerometer calibration workflow
    /// </summary>
    public async Task AccelerometerCalibrationExampleAsync()
    {
        Console.WriteLine("=== Accelerometer Calibration Example ===\n");

        // Ensure drone is connected
        if (!_connectionService.IsConnected)
        {
            Console.WriteLine("Error: Drone not connected!");
            return;
        }

        var ct = CancellationToken.None;

        try
        {
            // Step 1: Start calibration
            Console.WriteLine("Starting accelerometer calibration...");
            await _calibrationService.StartCalibrationAsync(SensorCategory.Accelerometer, ct);
            
            // Step 2: Get current state and display to user
            var category = _calibrationService.GetCategoryState(SensorCategory.Accelerometer);
            Console.WriteLine($"Calibration Status: {category.Status}");
            Console.WriteLine($"Steps to complete: {category.CalibrationSteps.Count}");
            Console.WriteLine();

            // Step 3: Guide user through each position
            for (int i = 0; i < category.CalibrationSteps.Count; i++)
            {
                var step = category.CalibrationSteps[i];
                
                Console.WriteLine($"Step {i + 1}/{category.CalibrationSteps.Count}");
                Console.WriteLine($"Position: {step.Label}");
                Console.WriteLine($"Instruction: {step.InstructionText}");
                Console.WriteLine();
                
                // Wait for user to position vehicle
                Console.WriteLine("Press Enter when vehicle is in position...");
                Console.ReadLine();
                
                // Advance to next step (sends MAV_CMD_ACCELCAL_VEHICLE_POS)
                await _calibrationService.NextStepAsync(SensorCategory.Accelerometer, ct);
                Console.WriteLine($"Position {step.Label} confirmed!\n");
                
                // Small delay to allow FC to process
                await Task.Delay(500, ct);
            }

            // Step 4: Commit calibration
            Console.WriteLine("All positions completed! Committing calibration...");
            await _calibrationService.CommitCalibrationAsync(SensorCategory.Accelerometer, ct);
            
            // Step 5: Check final status
            category = _calibrationService.GetCategoryState(SensorCategory.Accelerometer);
            Console.WriteLine($"Final Status: {category.Status}");
            
            if (category.Status == Status.Complete)
            {
                Console.WriteLine("✓ Accelerometer calibration successful!");
                
                // Optional: Reboot drone
                Console.WriteLine("\nReboot recommended. Reboot now? (y/n)");
                var response = Console.ReadLine();
                if (response?.ToLower() == "y")
                {
                    Console.WriteLine("Rebooting drone...");
                    await _calibrationService.RebootDroneAsync(ct);
                    Console.WriteLine("Reboot command sent.");
                }
            }
            else
            {
                Console.WriteLine("✗ Calibration failed or incomplete.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during calibration: {ex.Message}");
            
            // Abort on error
            await _calibrationService.AbortCalibrationAsync(SensorCategory.Accelerometer, ct);
        }
    }

    /// <summary>
    /// Example: Simple compass calibration workflow
    /// </summary>
    public async Task CompassCalibrationExampleAsync()
    {
        Console.WriteLine("=== Compass Calibration Example ===\n");

        if (!_connectionService.IsConnected)
        {
            Console.WriteLine("Error: Drone not connected!");
            return;
        }

        var ct = CancellationToken.None;

        try
        {
            // Start compass calibration
            Console.WriteLine("Starting compass calibration...");
            await _calibrationService.StartCalibrationAsync(SensorCategory.Compass, ct);
            
            var category = _calibrationService.GetCategoryState(SensorCategory.Compass);
            var step = category.CalibrationSteps[0];
            
            Console.WriteLine(step.InstructionText);
            Console.WriteLine("\nPress Enter when calibration is complete (watch for FC messages)...");
            Console.ReadLine();
            
            // Advance (marks as complete for simple calibrations)
            await _calibrationService.NextStepAsync(SensorCategory.Compass, ct);
            
            // Commit
            await _calibrationService.CommitCalibrationAsync(SensorCategory.Compass, ct);
            
            category = _calibrationService.GetCategoryState(SensorCategory.Compass);
            Console.WriteLine($"Compass calibration {category.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            await _calibrationService.AbortCalibrationAsync(SensorCategory.Compass, ct);
        }
    }

    /// <summary>
    /// Example: Level horizon calibration
    /// </summary>
    public async Task LevelHorizonCalibrationExampleAsync()
    {
        Console.WriteLine("=== Level Horizon Calibration Example ===\n");

        if (!_connectionService.IsConnected)
        {
            Console.WriteLine("Error: Drone not connected!");
            return;
        }

        var ct = CancellationToken.None;

        try
        {
            // Start level calibration
            Console.WriteLine("Starting level horizon calibration...");
            await _calibrationService.StartCalibrationAsync(SensorCategory.LevelHorizon, ct);
            
            var category = _calibrationService.GetCategoryState(SensorCategory.LevelHorizon);
            Console.WriteLine(category.CalibrationSteps[0].InstructionText);
            Console.WriteLine("\nCalibrating... (this usually takes 2-3 seconds)");
            
            // Wait for calibration to complete
            await Task.Delay(3000, ct);
            
            // Mark as complete
            await _calibrationService.NextStepAsync(SensorCategory.LevelHorizon, ct);
            await _calibrationService.CommitCalibrationAsync(SensorCategory.LevelHorizon, ct);
            
            category = _calibrationService.GetCategoryState(SensorCategory.LevelHorizon);
            Console.WriteLine($"Level horizon calibration {category.Status}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            await _calibrationService.AbortCalibrationAsync(SensorCategory.LevelHorizon, ct);
        }
    }

    /// <summary>
    /// Example: Display all calibration categories and their status
    /// </summary>
    public void DisplayAllCategoriesStatus()
    {
        Console.WriteLine("=== Calibration Status Overview ===\n");

        var categories = new[]
        {
            SensorCategory.Accelerometer,
            SensorCategory.Compass,
            SensorCategory.LevelHorizon,
            SensorCategory.Pressure,
            SensorCategory.Flow
        };

        foreach (var sensorCategory in categories)
        {
            var category = _calibrationService.GetCategoryState(sensorCategory);
            
            Console.WriteLine($"{category.DisplayName}:");
            Console.WriteLine($"  Status: {category.Status}");
            Console.WriteLine($"  Required: {category.Required}");
            Console.WriteLine($"  Commands: {category.Commands.Count}");
            Console.WriteLine($"  Steps: {category.CalibrationSteps.Count}");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Example: Abort calibration if something goes wrong
    /// </summary>
    public async Task AbortCalibrationExampleAsync(SensorCategory category)
    {
        Console.WriteLine($"Aborting {category} calibration...");
        
        try
        {
            await _calibrationService.AbortCalibrationAsync(category, CancellationToken.None);
            Console.WriteLine("Calibration aborted successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error aborting calibration: {ex.Message}");
        }
    }
}
