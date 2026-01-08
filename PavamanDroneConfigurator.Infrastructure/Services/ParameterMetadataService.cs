using PavamanDroneConfigurator.Core.Interfaces;
using PavamanDroneConfigurator.Core.Models;
using System.Collections.ObjectModel;

namespace PavamanDroneConfigurator.Infrastructure.Services;

public class ParameterMetadataService : IParameterMetadataService
{
    private readonly Dictionary<string, ParameterMetadata> _metadata;

    public ParameterMetadataService()
    {
        _metadata = BuildMetadataDatabase();
    }

    public ParameterMetadata? GetMetadata(string parameterName)
    {
        if (string.IsNullOrEmpty(parameterName)) return null;
        _metadata.TryGetValue(parameterName.ToUpperInvariant(), out var meta);
        return meta;
    }

    public IEnumerable<ParameterMetadata> GetAllMetadata() => _metadata.Values;

    public IEnumerable<ParameterMetadata> GetParametersByGroup(string group)
    {
        return _metadata.Values.Where(m => string.Equals(m.Group, group, StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> GetGroups()
    {
        return _metadata.Values.Where(m => !string.IsNullOrEmpty(m.Group)).Select(m => m.Group!).Distinct().OrderBy(g => g);
    }

    public void EnrichParameter(DroneParameter parameter)
    {
        var meta = GetMetadata(parameter.Name);
        if (meta == null) return;

        parameter.Description = meta.Description;
        parameter.MinValue = meta.MinValue;
        parameter.MaxValue = meta.MaxValue;
        parameter.DefaultValue = meta.DefaultValue;
        parameter.Units = meta.Units;

        if (meta.Values != null && meta.Values.Count > 0)
        {
            parameter.Options = new ObservableCollection<ParameterOption>(
                meta.Values.OrderBy(kvp => kvp.Key).Select(kvp => new ParameterOption { Value = kvp.Key, Label = kvp.Value }));
        }
    }

    private static Dictionary<string, ParameterMetadata> BuildMetadataDatabase()
    {
        var db = new Dictionary<string, ParameterMetadata>(StringComparer.OrdinalIgnoreCase);
        
        var flightModes = new Dictionary<int, string> { [0] = "Stabilize", [1] = "Acro", [2] = "AltHold", [3] = "Auto", [4] = "Guided", [5] = "Loiter", [6] = "RTL", [7] = "Circle", [9] = "Land", [13] = "Sport", [15] = "AutoTune", [16] = "PosHold" };

        Add(db, "ACRO_BAL_PITCH", "Acro Balance Pitch", "Acro", "Rate at which pitch returns to level", null, null, 1);
        Add(db, "ACRO_BAL_ROLL", "Acro Balance Roll", "Acro", "Rate at which roll returns to level", null, null, 1);
        Add(db, "ACRO_OPTIONS", "Acro Options", "Acro", "Options for acro mode", 0, 3, 0, values: new() { [0] = "Disabled", [1] = "AirMode", [2] = "RateLoopOnly", [3] = "AirMode+RateLoop" });
        Add(db, "ACRO_RP_EXPO", "Acro Roll Pitch Expo", "Acro", "Acro roll pitch expo", -0.5f, 0.95f, 0.3f);
        Add(db, "ACRO_RP_RATE", "Acro Roll Pitch Rate", "Acro", "Maximum roll pitch rate", 1, 1080, 360, "deg/s");
        Add(db, "ACRO_YAW_RATE", "Acro Yaw Rate", "Acro", "Maximum yaw rate", 1, 360, 135, "deg/s");
        Add(db, "ACRO_TRAINER", "Acro Trainer", "Acro", "Trainer mode", 0, 2, 2, values: new() { [0] = "Disabled", [1] = "Leveling", [2] = "Leveling and Limited" });
        
        Add(db, "ANGLE_MAX", "Max Lean Angle", "Attitude", "Maximum lean angle", 1000, 8000, 3000, "cdeg");
        
        Add(db, "ARMING_CHECK", "Arming Checks", "Arming", "Checks before arming", 0, 32767, 1, values: new() { [0] = "Disabled", [1] = "Enabled" });
        Add(db, "ARMING_RUDDER", "Rudder Arm Disarm", "Arming", "Arm disarm with rudder", 0, 2, 2, values: new() { [0] = "Disabled", [1] = "ArmOnly", [2] = "ArmOrDisarm" });
        
        Add(db, "BATT_MONITOR", "Battery Monitor Type", "Battery", "Type of battery monitor", 0, 24, 4, values: new() { [0] = "Disabled", [3] = "Analog Voltage Only", [4] = "Analog Voltage and Current", [5] = "Solo", [6] = "Bebop", [7] = "SMBus-Generic", [8] = "DroneCAN-BatteryInfo", [9] = "ESC", [10] = "Sum Of Selected Monitors" });
        Add(db, "BATT_CAPACITY", "Battery Capacity", "Battery", "Battery capacity", 0, 100000, 3300, "mAh");
        Add(db, "BATT_LOW_VOLT", "Low Battery Voltage", "Battery", "Low voltage threshold", 0, 120, 0, "V");
        Add(db, "BATT_CRT_VOLT", "Critical Battery Voltage", "Battery", "Critical voltage threshold", 0, 120, 0, "V");
        Add(db, "BATT_ARM_VOLT", "Arm Voltage", "Battery", "Minimum arm voltage", 0, 100, 0, "V");
        Add(db, "BATT_FS_LOW_ACT", "Low Battery Failsafe Action", "Battery", "Low battery action", 0, 7, 0, values: new() { [0] = "None", [1] = "Land", [2] = "RTL", [3] = "SmartRTL or RTL", [4] = "SmartRTL or Land" });
        Add(db, "BATT_FS_CRT_ACT", "Critical Battery Failsafe Action", "Battery", "Critical battery action", 0, 7, 0, values: new() { [0] = "None", [1] = "Land", [2] = "RTL", [3] = "SmartRTL or RTL", [4] = "SmartRTL or Land" });
        
        Add(db, "COMPASS_USE", "Use Compass", "Compass", "Enable compass", 0, 1, 1, values: new() { [0] = "Disabled", [1] = "Enabled" });
        Add(db, "COMPASS_AUTODEC", "Auto Declination", "Compass", "Auto declination", 0, 1, 1, values: new() { [0] = "Disabled", [1] = "Enabled" });
        Add(db, "COMPASS_LEARN", "Compass Learn", "Compass", "Compass learning", 0, 3, 0, values: new() { [0] = "Disabled", [1] = "Internal", [2] = "EKF", [3] = "InFlight" });
        
        Add(db, "FS_THR_ENABLE", "Throttle Failsafe Enable", "Failsafe", "Throttle failsafe", 0, 5, 1, values: new() { [0] = "Disabled", [1] = "Enabled always RTL", [2] = "Enabled Continue Mission", [3] = "Enabled always Land" });
        Add(db, "FS_GCS_ENABLE", "GCS Failsafe Enable", "Failsafe", "GCS failsafe", 0, 5, 1, values: new() { [0] = "Disabled", [1] = "Enabled always RTL", [2] = "Continue Mission", [3] = "Always Land" });
        Add(db, "FS_CRASH_CHECK", "Crash Check Enable", "Failsafe", "Crash check", 0, 1, 1, values: new() { [0] = "Disabled", [1] = "Enabled" });
        
        Add(db, "FLTMODE1", "Flight Mode 1", "Flight Modes", "Flight mode 1", 0, 27, 0, values: flightModes);
        Add(db, "FLTMODE2", "Flight Mode 2", "Flight Modes", "Flight mode 2", 0, 27, 0, values: flightModes);
        Add(db, "FLTMODE3", "Flight Mode 3", "Flight Modes", "Flight mode 3", 0, 27, 0, values: flightModes);
        Add(db, "FLTMODE4", "Flight Mode 4", "Flight Modes", "Flight mode 4", 0, 27, 0, values: flightModes);
        Add(db, "FLTMODE5", "Flight Mode 5", "Flight Modes", "Flight mode 5", 0, 27, 0, values: flightModes);
        Add(db, "FLTMODE6", "Flight Mode 6", "Flight Modes", "Flight mode 6", 0, 27, 0, values: flightModes);
        
        Add(db, "FRAME_TYPE", "Frame Type", "Frame", "Frame type", 0, 22, 1, values: new() { [0] = "Plus", [1] = "X", [2] = "V", [3] = "H", [10] = "Y6B" });
        Add(db, "FRAME_CLASS", "Frame Class", "Frame", "Frame class", 0, 18, 1, values: new() { [0] = "Undefined", [1] = "Quad", [2] = "Hexa", [3] = "Octa", [5] = "Y6" });
        
        Add(db, "GPS_TYPE", "GPS Type", "GPS", "GPS type", 0, 26, 1, values: new() { [0] = "None", [1] = "Auto", [2] = "uBlox", [5] = "NMEA", [9] = "DroneCAN" });
        
        Add(db, "MOT_PWM_TYPE", "Motor PWM Type", "Motors", "PWM type", 0, 10, 0, values: new() { [0] = "Normal", [1] = "OneShot", [2] = "OneShot125", [3] = "Brushed", [4] = "DShot150", [5] = "DShot300", [6] = "DShot600", [7] = "DShot1200" });
        Add(db, "MOT_SPIN_ARM", "Motor Spin Armed", "Motors", "Spin when armed", 0, 1, 0.1f);
        Add(db, "MOT_SPIN_MIN", "Motor Spin Min", "Motors", "Min spin", 0, 1, 0.15f);
        Add(db, "MOT_SPIN_MAX", "Motor Spin Max", "Motors", "Max spin", 0, 1, 0.95f);
        Add(db, "MOT_THST_HOVER", "Motor Thrust Hover", "Motors", "Hover thrust", 0, 1, 0.35f);
        
        Add(db, "PILOT_SPEED_UP", "Pilot Max Speed Up", "Pilot", "Max climb speed", 50, 500, 250, "cm/s");
        Add(db, "PILOT_SPEED_DN", "Pilot Max Speed Down", "Pilot", "Max descend speed", 50, 500, 150, "cm/s");
        
        Add(db, "RCMAP_ROLL", "Roll Channel", "RC Mapping", "Roll channel", 1, 16, 1);
        Add(db, "RCMAP_PITCH", "Pitch Channel", "RC Mapping", "Pitch channel", 1, 16, 2);
        Add(db, "RCMAP_THROTTLE", "Throttle Channel", "RC Mapping", "Throttle channel", 1, 16, 3);
        Add(db, "RCMAP_YAW", "Yaw Channel", "RC Mapping", "Yaw channel", 1, 16, 4);
        
        Add(db, "RTL_ALT", "RTL Altitude", "RTL", "RTL altitude", 0, 800000, 1500, "cm");
        Add(db, "RTL_SPEED", "RTL Speed", "RTL", "RTL speed", 0, 2000, 0, "cm/s");
        Add(db, "RTL_CONE_SLOPE", "RTL Cone Slope", "RTL", "Cone slope", 0.5f, 10, 3);
        
        Add(db, "SERIAL0_PROTOCOL", "Serial0 Protocol", "Serial", "Serial 0 protocol", -1, 40, 2, values: new() { [-1] = "None", [1] = "MAVLink1", [2] = "MAVLink2", [4] = "GPS", [5] = "GPS2" });
        Add(db, "SERIAL1_PROTOCOL", "Serial1 Protocol", "Serial", "Serial 1 protocol", -1, 40, 2, values: new() { [-1] = "None", [1] = "MAVLink1", [2] = "MAVLink2", [4] = "GPS", [5] = "GPS2" });
        Add(db, "SERIAL0_BAUD", "Serial0 Baud Rate", "Serial", "Serial 0 baud", 1, 2000000, 115, values: new() { [57] = "57600", [115] = "115200", [230] = "230400", [921] = "921600" });
        
        Add(db, "LOG_BACKEND_TYPE", "Log Backend Type", "Logging", "Log backend", 0, 5, 1, values: new() { [0] = "None", [1] = "File", [2] = "MAVLink", [3] = "Both" });
        Add(db, "LOG_DISARMED", "Log While Disarmed", "Logging", "Log when disarmed", 0, 1, 0, values: new() { [0] = "Disabled", [1] = "Enabled" });

        return db;
    }

    private static void Add(Dictionary<string, ParameterMetadata> db, string name, string displayName, string group, string description, float? min = null, float? max = null, float? defaultVal = null, string? units = null, Dictionary<int, string>? values = null)
    {
        db[name] = new ParameterMetadata { Name = name, DisplayName = displayName, Description = description, Group = group, MinValue = min, MaxValue = max, DefaultValue = defaultVal, Units = units, Values = values, Range = min.HasValue && max.HasValue ? $"{min} - {max}" : null };
    }
}
