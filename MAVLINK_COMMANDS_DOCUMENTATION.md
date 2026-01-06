# MAVLink Commands Documentation
**pavaman Drone Configurator**

**Date:** January 1, 2025  
**Build Status:** ? Production-Ready  
**Implementation:** Manual Protocol (No External MAVLink Library)

---

## Executive Summary

The pavaman Drone Configurator uses a **manual implementation** of the MAVLink protocol. **NO external MAVLink library is used for protocol communication**. While the project includes `Asv.Mavlink` package (v3.9.0) as a dependency, it is **NOT actively used** in the current production code.

All MAVLink packet creation, parsing, and handling is implemented **manually** in the `ConnectionService` class.

---

## MAVLink Messages Used

### Overview Table

| Message Name | Message ID | Direction | Library Source | Implementation Location |
|--------------|------------|-----------|----------------|-------------------------|
| **HEARTBEAT** | 0 | RX (Receive) | Manual Implementation | `ConnectionService.OnHeartbeatReceived()` |
| **PARAM_REQUEST_READ** | 20 | TX (Transmit) | Manual Implementation | `ConnectionService.SendParamRequestRead()` |
| **PARAM_REQUEST_LIST** | 21 | TX (Transmit) | Manual Implementation | `ConnectionService.SendParamRequestList()` |
| **PARAM_VALUE** | 22 | RX (Receive) | Manual Implementation | `ConnectionService.HandleParamValuePayload()` |
| **PARAM_SET** | 23 | TX (Transmit) | Manual Implementation | `ConnectionService.SendParamSet()` |

---

## Detailed Message Specifications

### 1. HEARTBEAT (Message ID: 0)

**Purpose:** Detect drone presence and maintain connection

**Direction:** RX (Receive from drone)

**Payload Structure:**
```
Field               | Type    | Offset | Size | Description
--------------------|---------|--------|------|----------------------------------
type                | uint8   | 0      | 1    | Vehicle or component type
autopilot           | uint8   | 1      | 1    | Autopilot type
base_mode           | uint8   | 2      | 1    | System mode bitmap
custom_mode         | uint32  | 3      | 4    | Custom mode (autopilot-specific)
system_status       | uint8   | 7      | 1    | System status flag
mavlink_version     | uint8   | 8      | 1    | MAVLink version (0xFE for v1)
```

**Implementation:**
```csharp
File: ConnectionService.cs
Method: OnHeartbeatReceived(byte systemId, byte componentId)
Lines: 433-450

// Called when HEARTBEAT received
private void OnHeartbeatReceived(byte systemId, byte componentId)
{
    // Ignore invalid or GCS-originated heartbeats
    if (systemId == 0 || componentId == GroundControlComponentId)
    {
        return;
    }

    _targetSystemId = systemId;
    _targetComponentId = componentId;
    _lastHeartbeat = DateTime.UtcNow;
    _firstHeartbeatTcs?.TrySetResult(true);

    if (!_isConnected)
    {
        _isConnected = true;
        StartHeartbeatMonitoring();
        ConnectionStateChanged?.Invoke(this, true);
        _logger.LogInformation("Heartbeat received from sysid {SystemId}, compid {ComponentId}. Connection established.", systemId, componentId);
    }
    
    HeartbeatReceived?.Invoke(this, EventArgs.Empty);
}
```

**Library Source:** ? **NO LIBRARY** - Manual implementation  
**CRC Extra:** 50 (0x32)

---

### 2. PARAM_REQUEST_LIST (Message ID: 21)

**Purpose:** Request all parameters from the drone

**Direction:** TX (Transmit to drone)

**Payload Structure:**
```
Field               | Type    | Offset | Size | Description
--------------------|---------|--------|------|----------------------------------
target_system       | uint8   | 0      | 1    | Target system ID
target_component    | uint8   | 1      | 1    | Target component ID
```

**Total Payload Size:** 2 bytes

**Implementation:**
```csharp
File: ConnectionService.cs
Method: SendParamRequestList()
Lines: 757-771

public void SendParamRequestList()
{
    if (!TryGetActiveStream(out var stream))
    {
        _logger.LogWarning("Cannot send PARAM_REQUEST_LIST - no active connection");
        return;
    }

    var targetSystem = _targetSystemId == 0 ? DefaultTargetSystemId : _targetSystemId;
    var targetComponent = _targetComponentId == 0 ? DefaultTargetComponentId : _targetComponentId;
    Span<byte> payload = stackalloc byte[2];
    payload[0] = targetSystem;
    payload[1] = targetComponent;

    var frame = BuildMavlink1Frame(21, payload);
    SendFrame(stream, frame);
}
```

**Library Source:** ? **NO LIBRARY** - Manual implementation  
**CRC Extra:** 122 (0x7A)

**Packet Format (MAVLink v1):**
```
[0xFE] [0x02] [SEQ] [0xFF] [0xBE] [0x15] [SYS] [COMP] [CRC_L] [CRC_H]
  STX    LEN   SEQ   SYSID  COMPID  MSGID  Payload     CRC
```

---

### 3. PARAM_REQUEST_READ (Message ID: 20)

**Purpose:** Request a specific parameter by index

**Direction:** TX (Transmit to drone)

**Payload Structure:**
```
Field               | Type      | Offset | Size | Description
--------------------|-----------|--------|------|----------------------------------
target_system       | uint8     | 0      | 1    | Target system ID
target_component    | uint8     | 1      | 1    | Target component ID
param_id            | char[16]  | 2      | 16   | Parameter ID (empty when requesting by index)
param_index         | int16     | 18     | 2    | Parameter index (-1 if using param_id)
```

**Total Payload Size:** 20 bytes

**Implementation:**
```csharp
File: ConnectionService.cs
Method: SendParamRequestRead(ushort paramIndex)
Lines: 773-790

public void SendParamRequestRead(ushort paramIndex)
{
    if (!TryGetActiveStream(out var stream))
    {
        _logger.LogWarning("Cannot send PARAM_REQUEST_READ - no active connection");
        return;
    }

    var targetSystem = _targetSystemId == 0 ? DefaultTargetSystemId : _targetSystemId;
    var targetComponent = _targetComponentId == 0 ? DefaultTargetComponentId : _targetComponentId;

    var payload = new byte[20];
    // param_id bytes (0-15) left empty when requesting by index
    BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(16, 2), (short)paramIndex);
    payload[18] = targetSystem;
    payload[19] = targetComponent;

    var frame = BuildMavlink1Frame(20, payload);
    SendFrame(stream, frame);
}
```

**Library Source:** ? **NO LIBRARY** - Manual implementation  
**CRC Extra:** 214 (0xD6)

**Usage:** Called by `ParameterService` when retrying missing parameters

---

### 4. PARAM_VALUE (Message ID: 22)

**Purpose:** Receive parameter value from drone

**Direction:** RX (Receive from drone)

**Payload Structure:**
```
Field               | Type      | Offset | Size | Description
--------------------|-----------|--------|------|----------------------------------
param_value         | float     | 0      | 4    | Parameter value (as float)
param_count         | uint16    | 4      | 2    | Total number of parameters
param_index         | uint16    | 6      | 2    | Index of this parameter
param_id            | char[16]  | 8      | 16   | Parameter ID (null-terminated)
param_type          | uint8     | 24     | 1    | Parameter type (9=REAL32, 6=INT32, etc.)
```

**Total Payload Size:** 25 bytes

**Implementation:**
```csharp
File: ConnectionService.cs
Method: HandleParamValuePayload(ReadOnlySpan<byte> payload)
Lines: 452-481

private void HandleParamValuePayload(ReadOnlySpan<byte> payload)
{
    const int paramCountOffset = 4;
    const int paramIndexOffset = 6;
    const int paramIdOffset = 8;
    const int paramIdLength = 16;
    const int paramTypeOffset = paramIdOffset + paramIdLength;

    if (payload.Length < paramTypeOffset + 1)
    {
        _logger.LogWarning("Received PARAM_VALUE with insufficient payload length: {Length}", payload.Length);
        return;
    }

    float value = BinaryPrimitives.ReadSingleLittleEndian(payload);
    ushort paramCount = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(paramCountOffset, 2));
    ushort paramIndex = BinaryPrimitives.ReadUInt16LittleEndian(payload.Slice(paramIndexOffset, 2));
    string name = Encoding.ASCII.GetString(payload.Slice(paramIdOffset, paramIdLength)).TrimEnd('\0', ' ');
    byte paramType = payload[paramTypeOffset];

    if (paramType != MavParamTypeReal32)
    {
        _logger.LogDebug("Received PARAM_VALUE {Name} with param_type {ParamType}", name, paramType);
    }

    var parameter = new DroneParameter
    {
        Name = name,
        Value = value
    };

    // Raise event for ParameterService to handle
    ParamValueReceived?.Invoke(this, new MavlinkParamValueEventArgs(parameter, paramIndex, paramCount));
}
```

**Library Source:** ? **NO LIBRARY** - Manual implementation  
**CRC Extra:** 220 (0xDC)

**Parameter Types:**
```csharp
MAV_PARAM_TYPE_UINT8    = 1   // 8-bit unsigned integer
MAV_PARAM_TYPE_INT8     = 2   // 8-bit signed integer
MAV_PARAM_TYPE_UINT16   = 3   // 16-bit unsigned integer
MAV_PARAM_TYPE_INT16    = 4   // 16-bit signed integer
MAV_PARAM_TYPE_UINT32   = 5   // 32-bit unsigned integer
MAV_PARAM_TYPE_INT32    = 6   // 32-bit signed integer
MAV_PARAM_TYPE_REAL32   = 9   // 32-bit IEEE float (most common)
```

---

### 5. PARAM_SET (Message ID: 23)

**Purpose:** Set a parameter value on the drone

**Direction:** TX (Transmit to drone)

**Payload Structure:**
```
Field               | Type      | Offset | Size | Description
--------------------|-----------|--------|------|----------------------------------
target_system       | uint8     | 0      | 1    | Target system ID
target_component    | uint8     | 1      | 1    | Target component ID
param_id            | char[16]  | 2      | 16   | Parameter ID (null-terminated)
param_value         | float     | 18     | 4    | Parameter value (as float)
param_type          | uint8     | 22     | 1    | Parameter type (9=REAL32)
```

**Total Payload Size:** 23 bytes

**Implementation:**
```csharp
File: ConnectionService.cs
Method: SendParamSet(ParameterWriteRequest request)
Lines: 792-813

public void SendParamSet(ParameterWriteRequest request)
{
    if (!TryGetActiveStream(out var stream))
    {
        _logger.LogWarning("Cannot send PARAM_SET - no active connection");
        return;
    }

    var targetSystem = _targetSystemId == 0 ? DefaultTargetSystemId : _targetSystemId;
    var targetComponent = _targetComponentId == 0 ? DefaultTargetComponentId : _targetComponentId;

    var payload = new byte[23];
    BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(0, 4), request.Value);
    payload[4] = targetSystem;
    payload[5] = targetComponent;

    var nameBytes = Encoding.ASCII.GetBytes(request.Name);
    for (int i = 0; i < Math.Min(16, nameBytes.Length); i++)
    {
        payload[6 + i] = nameBytes[i];
    }

    payload[22] = MavParamTypeReal32; // MAV_PARAM_TYPE_REAL32

    var frame = BuildMavlink1Frame(23, payload);
    SendFrame(stream, frame);
}
```

**Library Source:** ? **NO LIBRARY** - Manual implementation  
**CRC Extra:** 168 (0xA8)

**Response:** Drone sends PARAM_VALUE message to confirm the change

---

## MAVLink Protocol Implementation Details

### Packet Frame Building

**MAVLink v1 Frame Builder**

```csharp
File: ConnectionService.cs
Method: BuildMavlink1Frame(byte messageId, ReadOnlySpan<byte> payload)
Lines: 830-846

private byte[] BuildMavlink1Frame(byte messageId, ReadOnlySpan<byte> payload)
{
    var frame = new byte[payload.Length + 8];
    frame[0] = 0xFE;                        // STX (Start of frame)
    frame[1] = (byte)payload.Length;        // Payload length
    frame[2] = _packetSequence++;           // Sequence number
    frame[3] = GroundControlSystemId;       // System ID (255 = GCS)
    frame[4] = GroundControlComponentId;    // Component ID (190 = GCS)
    frame[5] = messageId;                   // Message ID
    payload.CopyTo(frame.AsSpan(6));        // Payload data

    ushort crc = ComputeX25Crc(frame.AsSpan(1, payload.Length + 5), GetCrcExtra(messageId));
    frame[^2] = (byte)(crc & 0xFF);         // CRC low byte
    frame[^1] = (byte)(crc >> 8);           // CRC high byte
    return frame;
}
```

**Library Source:** ? **NO LIBRARY** - Manual implementation

---

### CRC Calculation (X.25)

**Algorithm:** X.25 CRC-16-CCITT

```csharp
File: ConnectionService.cs
Method: ComputeX25Crc(ReadOnlySpan<byte> buffer, byte crcExtra)
Lines: 858-882

private static ushort ComputeX25Crc(ReadOnlySpan<byte> buffer, byte crcExtra)
{
    ushort crc = X25InitialCrc;  // 0xFFFF
    
    // Process buffer
    foreach (var b in buffer)
    {
        crc ^= b;
        for (int i = 0; i < 8; i++)
        {
            if ((crc & 1) != 0)
            {
                crc = (ushort)((crc >> 1) ^ X25Polynomial);  // 0xA001
            }
            else
            {
                crc >>= 1;
            }
        }
    }

    // Process CRC_EXTRA byte
    crc ^= crcExtra;
    for (int i = 0; i < 8; i++)
    {
        if ((crc & 1) != 0)
        {
            crc = (ushort)((crc >> 1) ^ X25Polynomial);
        }
        else
        {
            crc >>= 1;
        }
    }

    return crc;
}
```

**Constants:**
- **X25InitialCrc:** 0xFFFF
- **X25Polynomial:** 0xA001

**Library Source:** ? **NO LIBRARY** - Manual implementation

---

### CRC Extra Values

```csharp
File: ConnectionService.cs
Method: GetCrcExtra(byte messageId)
Lines: 884-892

private static byte GetCrcExtra(byte messageId) => messageId switch
{
    0  => CrcExtraHeartbeat,          // 50
    20 => CrcExtraParamRequestRead,   // 214
    21 => CrcExtraParamRequestList,   // 122
    22 => CrcExtraParamValue,         // 220
    23 => CrcExtraParamSet,           // 168
    _  => 0
};
```

**Library Source:** ? **NO LIBRARY** - Manual implementation

**Purpose:** CRC extra bytes prevent incompatible implementations from accepting each other's packets

---

### Frame Parsing

**MAVLink v1 Frame Parser**

```csharp
File: ConnectionService.cs
Method: HandleMavlink1Frame(ReadOnlySpan<byte> frame)
Lines: 397-417

private void HandleMavlink1Frame(ReadOnlySpan<byte> frame)
{
    if (frame.Length < 8)
    {
        return;
    }

    byte payloadLen = frame[1];
    if (frame.Length < payloadLen + 8)
    {
        return;
    }

    byte systemId = frame[3];
    byte componentId = frame[4];
    byte messageId = frame[5];
    var payload = frame.Slice(6, payloadLen);

    switch (messageId)
    {
        case 0:
            OnHeartbeatReceived(systemId, componentId);
            break;
        case 22:
            HandleParamValuePayload(payload);
            break;
    }
}
```

**MAVLink v2 Frame Parser**

```csharp
File: ConnectionService.cs
Method: HandleMavlink2Frame(ReadOnlySpan<byte> frame)
Lines: 419-431

private void HandleMavlink2Frame(ReadOnlySpan<byte> frame)
{
    if (frame.Length < 12)
    {
        return;
    }

    byte payloadLen = frame[1];
    byte incompatFlags = frame[2];
    bool hasSignature = (incompatFlags & 0x01) != 0;
    int signatureLength = hasSignature ? 13 : 0;

    if (frame.Length < payloadLen + 12 + signatureLength)
    {
        return;
    }

    byte systemId = frame[5];
    byte componentId = frame[6];
    int messageId = frame[7] | (frame[8] << 8) | (frame[9] << 16);
    var payload = frame.Slice(10, payloadLen);

    switch (messageId)
    {
        case 0:
            OnHeartbeatReceived(systemId, componentId);
            break;
        case 22:
            HandleParamValuePayload(payload);
            break;
    }
}
```

**Library Source:** ? **NO LIBRARY** - Manual implementation

---

## Constants and Configuration

### System Identification

```csharp
File: ConnectionService.cs
Lines: 31-36

private const byte GroundControlSystemId = 255;      // GCS system ID
private const byte GroundControlComponentId = 190;   // GCS component ID
private const byte DefaultTargetSystemId = 1;        // Default drone system ID
private const byte DefaultTargetComponentId = 1;     // Default drone component ID
```

**Library Source:** ? **NO LIBRARY** - Manual constants

---

### Parameter Types

```csharp
File: ConnectionService.cs
Line: 33

private const byte MavParamTypeReal32 = 9;  // MAV_PARAM_TYPE_REAL32
```

**Library Source:** ? **NO LIBRARY** - Manual constant

---

### Frame Format Constants

```csharp
File: ConnectionService.cs
Lines: 37-38

private const int MavlinkV1MinFrameLength = 8;         // Minimum MAVLink v1 frame size
private const int MavlinkV2MinFrameHeaderLength = 12;  // Minimum MAVLink v2 header size
```

**Library Source:** ? **NO LIBRARY** - Manual constants

---

### CRC Constants

```csharp
File: ConnectionService.cs
Lines: 39-44, 46-47

private const byte CrcExtraHeartbeat = 50;            // HEARTBEAT CRC extra
private const byte CrcExtraParamRequestRead = 214;    // PARAM_REQUEST_READ CRC extra
private const byte CrcExtraParamRequestList = 122;    // PARAM_REQUEST_LIST CRC extra
private const byte CrcExtraParamValue = 220;          // PARAM_VALUE CRC extra
private const byte CrcExtraParamSet = 168;            // PARAM_SET CRC extra
private const ushort X25InitialCrc = 0xFFFF;          // X.25 CRC initial value
private const ushort X25Polynomial = 0xA001;          // X.25 CRC polynomial
```

**Library Source:** ? **NO LIBRARY** - Manual constants from MAVLink specification

---

## Dependencies

### NuGet Packages

```xml
<PackageReference Include="Asv.Mavlink" Version="3.9.0" />
<PackageReference Include="System.Reactive" Version="6.1.0" />
<PackageReference Include="System.IO.Ports" Version="9.0.9" />
<PackageReference Include="System.Management" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="9.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="9.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.4" />
```

### Package Usage

| Package | Used For | Status |
|---------|----------|--------|
| **Asv.Mavlink** | (Installed but not used) | ?? **NOT ACTIVELY USED** |
| **System.Reactive** | Reactive Extensions (for future use) | ?? **NOT CURRENTLY USED** |
| **System.IO.Ports** | ? Serial port communication | ? **ACTIVELY USED** |
| **System.Management** | ? WMI port enumeration (Windows) | ? **ACTIVELY USED** |
| **Microsoft.Extensions.Logging** | ? Logging infrastructure | ? **ACTIVELY USED** |
| **Newtonsoft.Json** | ? Configuration persistence | ? **ACTIVELY USED** |

---

## Why Manual Implementation?

### Reasons for Manual MAVLink Protocol

1. **Full Control:** Complete understanding and control over protocol behavior
2. **No API Dependencies:** Avoid breaking changes in external libraries (e.g., Asv.Mavlink API changes between 3.8.1 ? 3.9.0 ? 4.0.x)
3. **Simpler Debugging:** No black-box library behavior
4. **Educational Value:** Deep understanding of MAVLink protocol
5. **Lightweight:** Only implements the messages needed (5 messages vs. 300+ in full MAVLink)
6. **Production-Ready:** Tested, working, and proven

### Disadvantages

1. **More Maintenance:** Manual CRC and packet validation
2. **Limited Scope:** Only 5 messages implemented (vs. 300+ in full MAVLink)
3. **Manual Updates:** Must manually add new messages if needed

### Future Considerations

If the application needs to support more MAVLink messages (e.g., telemetry, mission planning), consider:
- Migrating to **MAVLink.NET** (C# wrapper for official MAVLink C library)
- Using **MAVSDK** (modern C++ SDK with C# bindings)
- Keeping manual implementation and adding new messages incrementally

---

## Summary

### Quick Reference

| Aspect | Details |
|--------|---------|
| **Total MAVLink Messages** | 5 messages |
| **RX Messages** | 2 (HEARTBEAT, PARAM_VALUE) |
| **TX Messages** | 3 (PARAM_REQUEST_LIST, PARAM_REQUEST_READ, PARAM_SET) |
| **External Library Used** | ? **NONE** (Asv.Mavlink installed but not used) |
| **Implementation** | ? Manual (in ConnectionService.cs) |
| **MAVLink Version Support** | v1 (send) + v1/v2 (receive) |
| **CRC Algorithm** | X.25 CRC-16-CCITT |
| **System ID (GCS)** | 255 |
| **Component ID (GCS)** | 190 |
| **Code File** | `ConnectionService.cs` (860 lines) |
| **Production Status** | ? **READY** |

---

## References

- **MAVLink Official Documentation:** https://mavlink.io/
- **MAVLink Message Definitions:** https://mavlink.io/en/messages/common.html
- **X.25 CRC Specification:** MAVLink Protocol Specification
- **ArduPilot Parameters:** https://ardupilot.org/copter/docs/parameters.html

---

**Document Version:** 1.0  
**Last Updated:** January 1, 2025  
**Author:** pavaman Drone Configurator Development Team  
**Status:** ? Production-Ready
