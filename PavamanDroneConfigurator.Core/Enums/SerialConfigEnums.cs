namespace PavamanDroneConfigurator.Core.Enums;

/// <summary>
/// Serial port protocol options matching ArduPilot SERIALx_PROTOCOL parameter.
/// These define what device/function is connected to each serial port.
/// </summary>
public enum SerialProtocol
{
    /// <summary>No protocol - port disabled</summary>
    None = -1,
    
    /// <summary>MAVLink 1 protocol (legacy)</summary>
    MAVLink1 = 1,
    
    /// <summary>MAVLink 2 protocol (recommended for telemetry)</summary>
    MAVLink2 = 2,
    
    /// <summary>Frsky D protocol</summary>
    FrskyD = 3,
    
    /// <summary>Frsky SPort protocol</summary>
    FrskySPort = 4,
    
    /// <summary>GPS protocol (NMEA/UBX auto-detect)</summary>
    GPS = 5,
    
    /// <summary>Alexmos gimbal serial</summary>
    AlexmosGimbal = 7,
    
    /// <summary>SToRM32 gimbal serial (MAVLink mode)</summary>
    SToRM32MAVLink = 8,
    
    /// <summary>Rangefinder serial</summary>
    Rangefinder = 9,
    
    /// <summary>FrSky SPort Passthrough</summary>
    FrSkyPassthrough = 10,
    
    /// <summary>Lidar360 protocol</summary>
    Lidar360 = 11,
    
    /// <summary>Beacon protocol</summary>
    Beacon = 13,
    
    /// <summary>Volz servo protocol</summary>
    VolzServo = 14,
    
    /// <summary>SBus servo output</summary>
    SBusServo = 15,
    
    /// <summary>ESC telemetry</summary>
    ESCTelemetry = 16,
    
    /// <summary>Devo telemetry</summary>
    DevoTelemetry = 17,
    
    /// <summary>OpticalFlow sensor</summary>
    OpticalFlow = 18,
    
    /// <summary>RobotisServo protocol</summary>
    RobotisServo = 19,
    
    /// <summary>NMEA Output</summary>
    NMEAOutput = 20,
    
    /// <summary>WindVane protocol</summary>
    WindVane = 21,
    
    /// <summary>SLCAN protocol</summary>
    SLCAN = 22,
    
    /// <summary>RCIN protocol</summary>
    RCIN = 23,
    
    /// <summary>MegaSquirt EFI</summary>
    MegaSquirtEFI = 24,
    
    /// <summary>LTM telemetry</summary>
    LTM = 25,
    
    /// <summary>RunCam protocol</summary>
    RunCam = 26,
    
    /// <summary>HottTelem protocol</summary>
    HottTelem = 27,
    
    /// <summary>Scripting protocol</summary>
    Scripting = 28,
    
    /// <summary>Crossfire VTX</summary>
    CrossfireVTX = 29,
    
    /// <summary>Generator protocol</summary>
    Generator = 30,
    
    /// <summary>Winch protocol</summary>
    Winch = 31,
    
    /// <summary>MSP protocol</summary>
    MSP = 32,
    
    /// <summary>MSP DisplayPort</summary>
    MSPDisplayPort = 33,
    
    /// <summary>MAVLink High Latency</summary>
    MAVLinkHighLatency = 34,
    
    /// <summary>DJI FPV protocol</summary>
    DJIFPV = 35,
    
    /// <summary>CRSF Crossfire</summary>
    CRSF = 36,
    
    /// <summary>EFI MS protocol</summary>
    EFIMS = 37,
    
    /// <summary>LCD display via MAVLink</summary>
    LCDDisplay = 38,
    
    /// <summary>MAVLink 2 for water depth</summary>
    WaterDepth = 39,
    
    /// <summary>IRC Tramp protocol</summary>
    IRCTramp = 40
}

/// <summary>
/// Serial port baud rate options matching ArduPilot SERIALx_BAUD parameter.
/// Values represent the actual baud rate / 1000 for storage efficiency.
/// </summary>
public enum SerialBaudRate
{
    /// <summary>1200 baud</summary>
    Baud1200 = 1,
    
    /// <summary>2400 baud</summary>
    Baud2400 = 2,
    
    /// <summary>4800 baud</summary>
    Baud4800 = 4,
    
    /// <summary>9600 baud</summary>
    Baud9600 = 9,
    
    /// <summary>19200 baud</summary>
    Baud19200 = 19,
    
    /// <summary>38400 baud</summary>
    Baud38400 = 38,
    
    /// <summary>57600 baud (common for telemetry)</summary>
    Baud57600 = 57,
    
    /// <summary>111100 baud</summary>
    Baud111100 = 111,
    
    /// <summary>115200 baud (common for GPS)</summary>
    Baud115200 = 115,
    
    /// <summary>230400 baud</summary>
    Baud230400 = 230,
    
    /// <summary>256000 baud</summary>
    Baud256000 = 256,
    
    /// <summary>400000 baud (Crossfire)</summary>
    Baud400000 = 400,
    
    /// <summary>460800 baud</summary>
    Baud460800 = 460,
    
    /// <summary>500000 baud</summary>
    Baud500000 = 500,
    
    /// <summary>921600 baud (high speed)</summary>
    Baud921600 = 921,
    
    /// <summary>1500000 baud</summary>
    Baud1500000 = 1500
}

/// <summary>
/// Serial port options flags matching ArduPilot SERIALx_OPTIONS bitmask
/// </summary>
[Flags]
public enum SerialOptions
{
    /// <summary>No options</summary>
    None = 0,
    
    /// <summary>Invert RX line</summary>
    InvertRX = 1,
    
    /// <summary>Invert TX line</summary>
    InvertTX = 2,
    
    /// <summary>Half duplex mode</summary>
    HalfDuplex = 4,
    
    /// <summary>Swap RX and TX pins</summary>
    SwapRXTX = 8,
    
    /// <summary>RX pull-down</summary>
    RXPullDown = 16,
    
    /// <summary>RX pull-up</summary>
    RXPullUp = 32,
    
    /// <summary>TX pull-down</summary>
    TXPullDown = 64,
    
    /// <summary>TX pull-up</summary>
    TXPullUp = 128,
    
    /// <summary>RS-485 DE pin</summary>
    RS485DE = 256
}

/// <summary>
/// Common serial port assignments for ArduPilot
/// </summary>
public enum SerialPortIndex
{
    /// <summary>Serial0 - Usually USB</summary>
    Serial0 = 0,
    
    /// <summary>Serial1 - TELEM1 port</summary>
    Serial1 = 1,
    
    /// <summary>Serial2 - TELEM2 port</summary>
    Serial2 = 2,
    
    /// <summary>Serial3 - GPS1 port</summary>
    Serial3 = 3,
    
    /// <summary>Serial4 - GPS2 port</summary>
    Serial4 = 4,
    
    /// <summary>Serial5 - Additional serial</summary>
    Serial5 = 5,
    
    /// <summary>Serial6 - Additional serial</summary>
    Serial6 = 6,
    
    /// <summary>Serial7 - Additional serial (if available)</summary>
    Serial7 = 7
}
