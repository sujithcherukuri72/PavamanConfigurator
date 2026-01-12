# Sensor Calibration UI/UX Improvements

## Overview
This document describes the improvements made to the sensor calibration flow to address user feedback about position validation, progress tracking, and UI clarity.

## Problem Statement
The original issue reported:
> "The calibration positions are already marked and when keeping the FC control in the same position only it is calibrating all the positions. It should check all the position when clicking the button calibrate. It should check the position if correct then mark it green and go to next one. If in wrong position give alert. Show percentage 0-100% of calibration completion. Check barometer UI and keep it neat and clean."

## Key Issues Identified
1. **Position Validation**: Steps were being marked complete before FC (Flight Controller) validated the position
2. **No Error Feedback**: No clear alert when FC rejects an incorrect position
3. **Progress Visibility**: Calibration progress percentage not prominently displayed
4. **Barometer UI**: Needed cleanup and better progress indication

## Solutions Implemented

### 1. Proper Position Validation Flow

#### Changes Made
- **Added `_validatedSteps` HashSet**: Tracks which positions have been validated by the FC
- **Modified `UpdateStepIndicators()` Method**: Now accepts a `markCurrentAsComplete` parameter
  - Steps only turn green AFTER FC validates the position (not when user clicks button)
  - Uses FC's `CalibrationStateMachine.Sampling` state as validation signal

#### Flow:
1. User clicks "Calibrate" → Step 1 becomes active (red border)
2. User places drone in position and clicks "Click When In Position"
3. Position stays red (waiting for FC validation)
4. FC validates position → Step 1 turns green, Step 2 becomes active
5. If FC rejects → Step stays red, error dialog shown

#### Code Changes:
```csharp
// Track validated steps
private readonly HashSet<int> _validatedSteps = new();

// Only mark complete after FC validation
private void UpdateStepIndicators(int step, bool markCurrentAsComplete = false)
{
    if (markCurrentAsComplete && step >= 1 && step <= 6)
    {
        _validatedSteps.Add(step);
    }
    
    IsStep1Complete = _validatedSteps.Contains(1);
    // ... similar for other steps
}

// Mark complete when FC is sampling (validated)
bool shouldMarkComplete = e.StateMachine == CalibrationStateMachine.Sampling;
if (shouldMarkComplete && e.CurrentStep.HasValue)
{
    UpdateStepIndicators(e.CurrentStep.Value, markCurrentAsComplete: true);
}
```

### 2. Position Rejection Error Handling

#### Changes Made
- **Added Position Rejection Detection**: Monitors `CalibrationStateMachine.PositionRejected` state
- **Error Dialog**: Shows clear message when FC rejects position

#### Code Changes:
```csharp
// Detect position rejection
if (state.Type == CalibrationType.Accelerometer && 
    state.StateMachine == CalibrationStateMachine.PositionRejected)
{
    ShowError("Incorrect Position", 
        $"The flight controller rejected the position. Please ensure the drone is correctly placed in the {GetPositionName(state.CurrentPosition)} position as shown in the image, then click 'Click When In Position' again.");
}
```

### 3. Enhanced Progress Visibility

#### Accelerometer Calibration
- **Added Progress Box**: Blue-tinted box with prominent percentage display
- **Shows**: "Calibration Progress" with large percentage number (0-100%)
- **Progress Bar**: Visual indicator below percentage

#### Barometer Calibration
- **Added `PressureCalibrationProgress` Property**: Tracks 0-100% progress
- **Progress Display**: Shows during calibration with percentage and bar
- **Better Visual Design**: Blue-tinted box matching accelerometer style

#### Compass Calibration
- **Enhanced Display**: Percentage shown alongside instructions
- **Yellow/Amber Theme**: Maintains visual consistency

### 4. Barometer UI Improvements

#### Visual Enhancements
- **Better Icon**: Replaced simple ellipse with styled barometer icon (⏱)
- **Descriptive Text**: Added "Measures atmospheric pressure for altitude"
- **Progress Section**: 
  - Shows progress box during calibration
  - Displays percentage prominently
  - Includes helpful text: "Please keep the vehicle still during calibration"
- **Cancel Button**: Added during active calibration

#### Layout Changes
```xml
<!-- Before: Simple ellipse -->
<Ellipse Width="60" Height="60" Stroke="#3B82F6" StrokeThickness="4" Fill="White"/>

<!-- After: Styled icon with description -->
<Border Width="80" Height="80" CornerRadius="40" 
        Background="White" BorderBrush="#3B82F6" BorderThickness="4">
    <TextBlock Text="⏱" FontSize="36"/>
</Border>
<TextBlock Text="Barometer Sensor" FontWeight="Bold"/>
<TextBlock Text="Measures atmospheric pressure for altitude" 
           TextWrapping="Wrap" MaxWidth="300"/>
```

## Visual Indicators

### Step Indicator Colors
- **Gray** (#E2E8F0 border, #F8FAFC background): Pending/Not started
- **Red** (#EF4444 border, #FEE2E2 background): Active/Waiting for position
- **Green** (#10B981 border, #D1FAE5 background): Validated/Complete

### Progress Displays
- **Accelerometer**: Blue theme (#22D3EE, #E7F3FF)
- **Compass**: Yellow/Amber theme (#F59E0B, #FEF3C7)
- **Barometer**: Blue theme (#3B82F6, #E7F3FF)

## User Experience Flow

### Before Changes
1. Click Calibrate → All steps immediately marked
2. No feedback if position incorrect
3. Progress hidden or unclear
4. Barometer UI minimal

### After Changes
1. Click Calibrate → Step 1 active (red)
2. Place drone → Click "Click When In Position"
3. Step stays red while FC validates
4. **If correct**: Step turns green, moves to next (red)
5. **If incorrect**: Error dialog shown, step stays red
6. Progress percentage clearly visible throughout
7. Barometer shows progress with cancel option

## Technical Details

### Key Methods Modified
1. **`UpdateStepIndicators()`**: Now properly tracks validation state
2. **`OnCalibrationStateChanged()`**: Detects position rejection
3. **`OnCalibrationProgressChanged()`**: Updates progress for all sensor types
4. **`CalibrateAccelerometerAsync()`**: Clears validation state on start

### Files Modified
1. **SensorsCalibrationPageViewModel.cs**: Core logic for validation and progress
2. **SensorsCalibrationPage.axaml**: UI improvements for all sensor tabs

## Testing Recommendations

When testing with actual hardware:

1. **Position Validation Test**:
   - Start accelerometer calibration
   - Place drone in WRONG position for step 1
   - Click "Click When In Position"
   - **Expected**: Error dialog, step stays red
   - Place drone in CORRECT position
   - Click "Click When In Position"
   - **Expected**: Step turns green, moves to step 2

2. **Progress Display Test**:
   - Observe percentage updates during calibration
   - Verify 0% at start, 100% at completion
   - Check all three sensor types show progress

3. **Barometer UI Test**:
   - Navigate to Pressure tab
   - Verify clean, informative layout
   - Start calibration
   - Verify progress box appears with percentage
   - Verify cancel button works

## Benefits

1. **User Confidence**: Users know immediately if position is correct
2. **Clear Feedback**: Error messages guide users to correct issues
3. **Progress Awareness**: Always know how far along calibration is
4. **Professional UI**: Consistent, clean interface across all sensor types
5. **Safety**: Ensures proper calibration by validating each position

## Future Enhancements (Optional)

1. Sound feedback on position acceptance/rejection
2. Animation when step transitions from red to green
3. Detailed help text per position
4. Video tutorials linked from UI
5. Calibration history/log viewer
