# Calibration Flow Diagram

## Before Fix (Incorrect Behavior)

```
User Clicks "Calibrate"
        ↓
Step 1 Active (RED) ← Place drone in Level position
        ↓
User Clicks "Click When In Position"
        ↓
Step 1 turns GREEN immediately ❌ (WRONG - not validated yet!)
Step 2 turns RED
        ↓
User hasn't even moved drone yet!
        ↓
All positions get marked complete without validation ❌
```

## After Fix (Correct Behavior)

```
User Clicks "Calibrate"
        ↓
Step 1 Active (RED) ← Place drone in Level position
Progress: 0%
        ↓
User Places Drone in Level Position
        ↓
User Clicks "Click When In Position"
        ↓
Step 1 stays RED (waiting for FC validation)
        ↓
        ├─→ FC Validates Position CORRECT ✓
        │       ↓
        │   Step 1 turns GREEN ✓
        │   Step 2 turns RED
        │   Progress: ~17%
        │       ↓
        │   Continue to next position...
        │
        └─→ FC Rejects Position INCORRECT ✗
                ↓
            ERROR DIALOG appears:
            "Incorrect Position"
            "The flight controller rejected the position.
             Please ensure drone is correctly placed..."
                ↓
            Step 1 stays RED
            User must adjust and try again
```

## Step Color Meanings

```
╔═══════════════╗
║   GRAY Box    ║  →  Not Started / Pending
╚═══════════════╝

╔═══════════════╗
║   RED Box     ║  →  Active / Waiting for User & FC Validation
╚═══════════════╝

╔═══════════════╗
║  GREEN Box    ║  →  Validated & Complete by FC ✓
╚═══════════════╝
```

## Progress Percentage Calculation

```
Accelerometer (6 positions):
- Position 1 waiting:  0-16%
- Position 1 sampling: 16-17%
- Position 2 waiting:  17-33%
- Position 2 sampling: 33-34%
- ...
- Position 6 complete: 100%

Compass (continuous):
- Based on FC feedback: 0-100%

Barometer (simple):
- Start: 0%
- In progress: 50%
- Complete: 100%
```

## Error Handling Flow

```
User Action → FC Validation → Result
     ↓              ↓            ↓
Click Button → Sends Command → ACK Received
     ↓              ↓            ↓
     │      ┌───────┴───────┐   │
     │      ↓               ↓   │
     │  ACCEPTED        REJECTED │
     │      ↓               ↓   │
     │  Sampling         Error  │
     │   Starts          Dialog │
     │      ↓               ↓   │
     │  Step turns    Step stays│
     │   GREEN           RED    │
     │      ↓               ↓   │
     │  Next step      Try again│
     └──────┴───────────────┘   │
            ↓                   │
        Continue or Fix Issue   │
```

## UI Components Updated

```
┌─────────────────────────────────────────┐
│  SENSORS Tab                            │
├─────────────────────────────────────────┤
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ ACCELEROMETER Tab               │   │
│  │                                 │   │
│  │  Instructions: Place Level      │   │
│  │                                 │   │
│  │  [Image of Level Position]      │   │
│  │                                 │   │
│  │  ╔════╗ ╔════╗ ╔════╗          │   │
│  │  ║ 1  ║ ║ 2  ║ ║ 3  ║ ...     │   │ ← Step Indicators
│  │  ║RED ║ ║GRAY║ ║GRAY║          │   │   (Colors change!)
│  │  ╚════╝ ╚════╝ ╚════╝          │   │
│  │                                 │   │
│  │  ┌───────────────────────────┐ │   │
│  │  │ Calibration Progress: 17% │ │   │ ← NEW! Progress Box
│  │  │ ▓▓▓░░░░░░░░░░░░░░░░░      │ │   │
│  │  └───────────────────────────┘ │   │
│  │                                 │   │
│  │  [Click When In Position]      │   │
│  │  [Cancel]                      │   │
│  └─────────────────────────────────┘   │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ PRESSURE Tab                    │   │
│  │                                 │   │
│  │       ┌─────┐                   │   │
│  │       │  ⏱  │  ← Barometer Icon │   │
│  │       └─────┘                   │   │
│  │  Barometer Sensor               │   │
│  │  Measures atmospheric pressure  │   │
│  │                                 │   │
│  │  ┌───────────────────────────┐ │   │
│  │  │ Calibrating: 50%          │ │   │ ← NEW! Progress
│  │  │ ▓▓▓▓▓▓▓▓▓▓░░░░░░░░░░      │ │   │
│  │  │ Keep vehicle still...     │ │   │
│  │  └───────────────────────────┘ │   │
│  │                                 │   │
│  │  [Cancel] [Calibrate]          │   │
│  └─────────────────────────────────┘   │
└─────────────────────────────────────────┘
```

## Key Improvements Summary

1. ✅ Steps only turn green AFTER FC validates
2. ✅ Error dialog shows when position rejected
3. ✅ Progress percentage always visible (0-100%)
4. ✅ Barometer UI clean with better icons
5. ✅ Cancel button available during calibration
6. ✅ Consistent visual design across all sensors
