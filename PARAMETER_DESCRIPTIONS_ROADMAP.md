# Complete ArduPilot Parameter Descriptions Framework

## Current Status
- **Implemented:** ~150 parameters with comprehensive descriptions
- **Remaining:** ~900 parameters across major groups
- **Priority:** High-importance groups done first (ACRO, ATC, AHRS, Battery, etc.)

---

## Parameter Groups Overview

### ? **Completed Groups** (150 parameters)
1. **ACRO** (11 params) - Acro mode behavior
2. **ADSB** (15 params) - ADS-B traffic avoidance
3. **AHRS** (14 params) - Attitude and heading reference
4. **ANGLE** (1 param) - Maximum lean angle
5. **ARMING** (2 params) - Arming checks and requirements
6. **ATC** (50+ params) - **NEW!** Attitude control PID tuning
7. **BATT/BATT2** (18 params) - Battery monitoring and failsafe
8. **COMPASS** (3 params) - Compass configuration
9. **FAILSAFE** (5 params) - Failsafe actions
10. **FLTMODE** (8 params) - Flight mode configuration
11. **FRAME** (2 params) - Frame type and class
12. **GPS** (2 params) - GPS configuration
13. **LOG** (4 params) - Logging configuration
14. **MOT** (7 params) - Motor configuration
15. **PILOT** (5 params) - Pilot input parameters
16. **RCMAP** (4 params) - RC channel mapping
17. **RTL** (6 params) - Return to launch
18. **SCR** (3 params) - Scripting
19. **SERIAL** (4 params) - Serial port configuration
20. **TERRAIN** (3 params) - Terrain following

### ? **High Priority - To Be Added** (300+ parameters)

#### INS (Inertial Navigation System) - 80+ parameters
- `INS_ACCOFFS_*` - Accelerometer offsets and calibration
- `INS_GYROFFS_*` - Gyroscope offsets  
- `INS_ACC_ID`, `INS_GYR_ID` - IMU identification
- `INS_ACC_CALTEMP`, `INS_GYR_CALTEMP` - Temperature calibration
- `INS_ACCEL_FILTER`, `INS_GYRO_FILTER` - IMU filtering
- `INS_HNTCH_*`, `INS_HNTC2_*` - Harmonic notch filters
- `INS_LOG_BAT_*` - IMU batch logging
- `INS_POS*_*` - IMU position offsets
- `INS_USE`, `INS_USE2`, `INS_USE3` - IMU enable/disable

#### EK3 (Extended Kalman Filter 3) - 100+ parameters
- `EK3_ENABLE` - Enable EKF3
- `EK3_PRIMARY` - Primary EKF core
- `EK3_IMU_MASK` - IMU selection
- `EK3_GPS_CHECK` - GPS checks before use
- `EK3_GPS_VACC_MAX` - GPS vertical accuracy threshold
- `EK3_GLITCH_RAD` - GPS glitch radius
- `EK3_POSNE_M_NSE`, `EK3_ALT_M_NSE` - Position/altitude measurement noise
- `EK3_VELNE_M_NSE`, `EK3_VELD_M_NSE` - Velocity measurement noise
- `EK3_GYRO_P_NSE`, `EK3_ACC_P_NSE` - Gyro/accel process noise
- `EK3_GBIAS_P_NSE`, `EK3_ABIAS_P_NSE` - Gyro/accel bias process noise
- `EK3_MAG_*` - Magnetometer fusion
- `EK3_WIND_*` - Wind estimation
- `EK3_DRAG_*` - Drag estimation
- `EK3_FLOW_*` - Optical flow
- `EK3_RNG_*` - Rangefinder
- `EK3_BCN_*` - Beacon
- `EK3_YAW_*` - Yaw estimation
- `EK3_SRC*_*` - Source selection for each core
- `EK3_GSF_*` - Gaussian sum filter (yaw estimator)

#### PSC (Position Control) - 30+ parameters
- `PSC_POSXY_P` - XY position controller P gain
- `PSC_POSZ_P` - Z position controller P gain
- `PSC_VELXY_*` - XY velocity controller PID
- `PSC_VELZ_*` - Z velocity controller PID
- `PSC_ACCZ_*` - Z acceleration controller PID
- `PSC_ANGLE_MAX` - Maximum lean angle
- `PSC_JERK_XY`, `PSC_JERK_Z` - Jerk limits

#### WPNAV (Waypoint Navigation) - 15+ parameters
- `WPNAV_SPEED` - Horizontal speed
- `WPNAV_SPEED_UP`, `WPNAV_SPEED_DN` - Vertical speeds
- `WPNAV_ACCEL`, `WPNAV_ACCEL_Z` - Accelerations
- `WPNAV_RADIUS` - Waypoint radius
- `WPNAV_JERK` - Jerk limit
- `WPNAV_TER_MARGIN` - Terrain margin
- `WPNAV_RFND_USE` - Rangefinder usage

#### SERVO (Servo/ESC Output) - 150+ parameters
- `SERVO1-16_FUNCTION` - Servo function assignment
- `SERVO1-16_MIN`, `SERVO1-16_MAX` - PWM limits
- `SERVO1-16_TRIM` - Neutral position
- `SERVO1-16_REVERSED` - Reverse direction
- `SERVO_RATE` - Default servo refresh rate
- `SERVO_DSHOT_*` - DShot ESC configuration
- `SERVO_BLH_*` - BLHeli ESC configuration
- `SERVO_GPIO_MASK` - GPIO output mask

#### RC (RC Input) - 160+ parameters
- `RC1-16_MIN`, `RC1-16_MAX` - RC channel ranges
- `RC1-16_TRIM` - RC channel trim
- `RC1-16_DZ` - RC channel deadzone
- `RC1-16_OPTION` - RC channel auxiliary functions
- `RC1-16_REVERSED` - RC channel reverse
- `RC_PROTOCOLS` - Enabled RC protocols
- `RC_OPTIONS` - RC options bitmask
- `RC_SPEED` - RC output rate

#### BARO (Barometer) - 15+ parameters
- `BARO_PRIMARY` - Primary barometer
- `BARO_ALT_OFFSET` - Altitude offset
- `BARO_GND_TEMP` - Ground temperature
- `BARO*_DEVID` - Barometer device ID
- `BARO*_GND_PRESS` - Ground pressure calibration
- `BARO_OPTIONS` - Barometer options

### ?? **Medium Priority - To Be Added** (300+ parameters)

#### AUTO/GUIDED Mode Parameters
- `AUTO_OPTIONS` - Auto mode options
- `GUID_*` - Guided mode parameters

#### Landing Parameters  
- `LAND_*` - Landing configuration

#### Loiter Parameters
- `LOIT_*` - Loiter mode configuration

#### Position Hold Parameters
- `PHLD_*` - Position hold configuration

#### Flow Hold Parameters
- `FHLD_*` - Flow hold (optical flow)

#### Circle Mode Parameters
- `CIRCLE_*` - Circle mode configuration

#### Follow Mode Parameters
- `FOLL_*` - Follow mode configuration

#### Avoid Parameters
- `AVOID_*` - Object avoidance
- `OA_*` - Object avoidance
- `AVD_*` - ADS-B avoidance

#### Fence Parameters
- `FENCE_*` - Geofencing configuration

#### Rally Points
- `RALLY_*` - Rally point configuration

#### Precision Landing
- `PLND_*` - Precision landing
- `PLDP_*` - Precision landing with depth camera

#### Camera/Gimbal
- `CAM_*`, `CAM1_*`, `CAM2_*` - Camera trigger
- `MNT1_*`, `MNT2_*` - Gimbal/mount control

#### Sprayer
- `SPRAY_*` - Agricultural sprayer

#### Parachute
- `CHUTE_*` - Parachute deployment

#### Winch
- `WINCH_*` - Winch control

#### Landing Gear
- `LGR_*` - Landing gear control

#### Buttons
- `BTN_*` - Button input

#### Relays
- `RELAY*_FUNCTION` - Relay outputs

### ?? **Low Priority - To Be Added** (250+ parameters)

#### Board/Hardware
- `BRD_*` - Board configuration
- `CAN_*` - CAN bus configuration
- `NET_*` - Network configuration

#### Developer/Debug
- `DEV_OPTIONS` - Developer options
- `SCHED_DEBUG` - Scheduler debug
- `GCS_PID_MASK` - PID debugging

#### Notifications
- `NTF_*` - Notification LED/buzzer

#### Telemetry Rates
- `SR0-6_*` - Telemetry stream rates

#### External Systems
- `EAHRS_*` - External AHRS
- `EFI_*` - Electronic fuel injection
- `GEN_*` - Generator
- `KDE_*` - KDE ESC
- `RPM*_*` - RPM sensors
- `RNGFND*_*` - Rangefinders
- `FLOW_*` - Optical flow
- `PRX*_*` - Proximity sensors
- `VISO_*` - Visual odometry
- `BCN_*` - Beacons

#### Statistics
- `STAT_*` - Flight statistics

#### Format Version
- `FORMAT_VERSION` - Parameter format version

#### System ID
- `SYSID_*` - System identification

---

## Implementation Strategy

### Phase 1: Critical Parameters (DONE ?)
- Flight control (ACRO, ATC, PILOT)
- Safety (ARMING, FAILSAFE, FENCE)
- Battery monitoring
- Basic navigation (RTL, WPNAV basics)

### Phase 2: Advanced Flight Control (IN PROGRESS)
- Complete INS parameters
- Complete EK3 parameters
- Complete PSC parameters
- Complete WPNAV parameters

### Phase 3: Hardware Configuration
- Complete SERVO parameters
- Complete RC parameters
- Complete BARO parameters
- Motor/ESC parameters

### Phase 4: Advanced Features
- Precision landing
- Object avoidance
- Camera/gimbal
- Follow mode
- Auxiliary features

### Phase 5: System Configuration
- Board configuration
- CAN bus
- Telemetry
- External sensors

---

## Quick Add Template

```csharp
// Group Name Parameters
Add(db, "PARAM_NAME", "Display Name", "Group", "Description explaining what this parameter does, recommended values, and warnings.", minValue, maxValue, defaultValue, "units");

// For enum parameters
Add(db, "PARAM_NAME", "Display Name", "Group", "Description", minValue, maxValue, defaultValue, values: new() { 
    [0] = "Option0", 
    [1] = "Option1" 
});
```

---

## Description Writing Guidelines

1. **Start with what it does** - "Controls...", "Sets...", "Enables..."
2. **Explain the impact** - "Higher values cause...", "Lower values result in..."
3. **Give context** - When used, where applicable, interactions with other params
4. **Add warnings** - Safety concerns, common mistakes
5. **Suggest values** - Typical ranges, recommended settings
6. **Keep it concise** - 1-3 sentences max per parameter

---

## Sources for Descriptions

1. **ArduPilot Documentation**: https://ardupilot.org/copter/docs/parameters.html
2. **Mission Planner**: Built-in parameter descriptions
3. **ArduPilot Wiki**: Parameter explanations in various topics
4. **ArduPilot Discourse**: Community explanations
5. **Parameter Metadata XML**: ArduPilot GitHub repository

---

## Auto-Generation Script (Future)

For repetitive parameters (RC1-16, SERVO1-16, etc.), consider creating a helper:

```csharp
void AddParameterSeries(string prefix, int count, string desc, string group)
{
    for (int i = 1; i <= count; i++)
    {
        Add(db, $"{prefix}{i}_MIN", $"{prefix}{i} Minimum", group, 
            $"Minimum PWM value for {prefix} channel {i}. Typically 1000-1100us.", 
            800, 2200, 1000, "PWM");
        Add(db, $"{prefix}{i}_MAX", $"{prefix}{i} Maximum", group, 
            $"Maximum PWM value for {prefix} channel {i}. Typically 1900-2000us.", 
            800, 2200, 2000, "PWM");
        // ... etc
    }
}
```

---

## Next Steps

1. ? Complete ATC parameter group (DONE)
2. ? Add INS parameter descriptions (80+ params)
3. ? Add EK3 parameter descriptions (100+ params)
4. ? Add PSC parameter descriptions (30+ params)
5. ? Add WPNAV parameter descriptions (15+ params)
6. ? Add SERVO parameter descriptions (150+ params)
7. ? Add RC parameter descriptions (160+ params)
8. ? Continue with remaining groups...

---

## Contribution Guide

To add descriptions for a parameter group:

1. Research the parameters in ArduPilot docs
2. Group related parameters together
3. Write clear, consistent descriptions
4. Test that metadata loads correctly
5. Verify descriptions appear in UI

**Target:** All 1000+ parameters with comprehensive descriptions

---

**Last Updated:** January 2026  
**Status:** ~150/1000 parameters complete (15%)  
**Current Focus:** ATC, INS, EK3 groups
