# Fix remaining 3 logic errors in CalibrationService.cs event handlers
$filePath = "C:\Pavaman\Final-repo\PavamanDroneConfigurator.Infrastructure\Services\CalibrationService.cs"

Write-Host "Reading CalibrationService.cs..."
$content = Get-Content $filePath -Raw

# Find and replace the OnAccelPositionRequested body - it has wrong implementation
$oldOnAccelPositionRequested = @'
    private void OnAccelPositionRequested(object? sender, AccelPositionRequestedEventArgs e)
    {
        // This event is raised when the accelerometer calibration service requests a position
        // We forward this request to our calibration flow
        
        if (_isCalibrating && _currentCalibrationType == CalibrationType.Accelerometer)
        {
            _logger.LogInformation("Forwarding accelerometer position request to calibration flow: Position {Position}", e.Position);
            
            // Simulate the position request as if it came from the flight controller
            HandleAccelCalVehiclePosAck((byte)e.Result);
        }
    }
'@

$newOnAccelPositionRequested = @'
    private void OnAccelPositionRequested(object? sender, AccelPositionRequestedEventArgs e)
    {
        _logger.LogInformation("FC requested position {Position}: {Name}", e.Position, e.PositionName);
        
        lock (_lock) { _currentPositionNumber = e.Position; }
        
        var step = GetCalibrationStep(e.Position);
        var instruction = GetPositionInstruction(e.Position);
        
        RaiseCalibrationStepRequired(step, instruction);
    }
'@

$content = $content -replace [regex]::Escape($oldOnAccelPositionRequested), $newOnAccelPositionRequested

# Find and replace the OnAccelPositionValidated body
$oldOnAccelPositionValidated = @'
    private void OnAccelPositionValidated(object? sender, AccelPositionValidationEventArgs e)
    {
        // This event is raised when the accelerometer calibration service validates a position
        // We update our state based on the validation result
        
        if (_isCalibrating && _currentCalibrationType == CalibrationType.Accelerometer)
        {
            _logger.LogInformation("Accelerometer position validation result: {Position} - {IsValid}", e.Position, e.IsValid);
            
            if (e.IsValid)
            {
                // Position validation PASSED
                // Notify the accelerometer calibration service to proceed
                _accelCalibrationService.ConfirmPosition(e.Position);
            }
            else
            {
                // Position validation FAILED
                // Update our diagnostics and transition to rejected state
                _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Warning,
                    $"Position {e.Position} rejected by accelerometer calibration service");
                
                TransitionState(CalibrationStateMachine.PositionRejected);
                UpdateState(CalibrationState.InProgress, _currentState.Progress,
                    $"Position {e.Position}/6: Accelerometer calibration rejected. Adjust position and try again.",
                    canConfirm: true);
            }
        }
    }
'@

$newOnAccelPositionValidated = @'
    private void OnAccelPositionValidated(object? sender, AccelPositionValidationEventArgs e)
    {
        if (!e.IsValid)
        {
            UpdateState(CalibrationState.InProgress, _currentState.Progress,
                $"? {e.Message}", canConfirm: true);
        }
    }
'@

$content = $content -replace [regex]::Escape($oldOnAccelPositionValidated), $newOnAccelPositionValidated

# Find and replace the OnAccelCalibrationCompleted body
$oldOnAccelCalibrationCompleted = @'
    private void OnAccelCalibrationCompleted(object? sender, AccelCalibrationCompletedEventArgs e)
    {
        // This event is raised when the accelerometer calibration service completes the calibration
        // We finalize our calibration state and diagnostics
        
        if (_isCalibrating && _currentCalibrationType == CalibrationType.Accelerometer)
        {
            _logger.LogInformation("Accelerometer calibration completed: {Result} - {Message}", e.Result, e.Message);
            
            // Update diagnostics with the final result
            _currentDiagnostics?.AddDiagnostic(CalibrationDiagnosticSeverity.Info,
                $"Accelerometer calibration completed: {e.Message}");
            
            // Complete the calibration with the result from the accelerometer service
            FinishCalibration(e.Result, e.Message);
        }
    }
'@

$newOnAccelCalibrationCompleted = @'
    private void OnAccelCalibrationCompleted(object? sender, AccelCalibrationCompletedEventArgs e)
    {
        _logger.LogInformation("Accelerometer calibration completed: {Result} - {Message} ({Duration})",
            e.Result, e.Message, e.Duration);
        
        var result = e.Result switch
        {
            AccelCalibrationResult.Success => CalibrationResult.Success,
            AccelCalibrationResult.Failed => CalibrationResult.Failed,
            AccelCalibrationResult.Cancelled => CalibrationResult.Cancelled,
            AccelCalibrationResult.Rejected => CalibrationResult.Rejected,
            _ => CalibrationResult.Failed
        };
        
        FinishCalibration(result, e.Message);
    }
'@

$content = $content -replace [regex]::Escape($oldOnAccelCalibrationCompleted), $newOnAccelCalibrationCompleted

Write-Host "Writing fixes..."
Set-Content $filePath $content -NoNewline

Write-Host "Done! Fixed 3 event handler implementations."
