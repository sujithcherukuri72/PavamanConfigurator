# Fix the 3 build errors in CalibrationService.cs
$filePath = "C:\Pavaman\Final-repo\PavamanDroneConfigurator.Infrastructure\Services\CalibrationService.cs"

Write-Host "Reading CalibrationService.cs..."
$content = Get-Content $filePath -Raw

# Fix 1: OnAccelStateChanged - Change EventArgs to AccelCalibrationStateChangedEventArgs
$content = $content -replace 'private void OnAccelStateChanged\(object\? sender, EventArgs e\)', 'private void OnAccelStateChanged(object? sender, AccelCalibrationStateChangedEventArgs e)'

# Fix 2: OnAccelPositionRequested - Change AccelerometerPositionEventArgs to AccelPositionRequestedEventArgs  
$content = $content -replace 'private void OnAccelPositionRequested\(object\? sender, AccelerometerPositionEventArgs e\)', 'private void OnAccelPositionRequested(object? sender, AccelPositionRequestedEventArgs e)'

# Fix 3: OnAccelPositionValidated - Change AccelerometerPositionValidationEventArgs to AccelPositionValidationEventArgs
$content = $content -replace 'private void OnAccelPositionValidated\(object\? sender, AccelerometerPositionValidationEventArgs e\)', 'private void OnAccelPositionValidated(object? sender, AccelPositionValidationEventArgs e)'

# Fix 4: OnAccelCalibrationCompleted - Change CalibrationResultEventArgs to AccelCalibrationCompletedEventArgs
$content = $content -replace 'private void OnAccelCalibrationCompleted\(object\? sender, CalibrationResultEventArgs e\)', 'private void OnAccelCalibrationCompleted(object? sender, AccelCalibrationCompletedEventArgs e)'

Write-Host "Writing fixes..."
Set-Content $filePath $content -NoNewline

Write-Host "Done! Fixed 3 event handler signatures."
