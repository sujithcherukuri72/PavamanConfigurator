using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PavamanDroneConfigurator.Infrastructure.MAVLink
{
    public sealed class AsvMavlinkWrapper : IDisposable
    {
        private readonly ILogger _logger;
        private Stream? _inputStream;
        private Stream? _outputStream;
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private Task? _heartbeatTask;
        private bool _disposed;

        private readonly byte[] _rxBuffer = new byte[4096];
        private int _rxBufferPos;
        private readonly object _bufferLock = new();
        private readonly object _writeLock = new();

        private byte _targetSystemId = 1;
        private byte _targetComponentId = 1;
        private byte _packetSequence;

        // MAVLink constants
        private const byte MAVLINK_STX_V1 = 0xFE;
        private const byte MAVLINK_STX_V2 = 0xFD;
        private const byte GCS_SYSTEM_ID = 255;
        private const byte GCS_COMPONENT_ID = 190;

        // Message IDs
        private const byte MAVLINK_MSG_ID_HEARTBEAT = 0;
        private const byte MAVLINK_MSG_ID_PARAM_REQUEST_READ = 20;
        private const byte MAVLINK_MSG_ID_PARAM_REQUEST_LIST = 21;
        private const byte MAVLINK_MSG_ID_PARAM_VALUE = 22;
        private const byte MAVLINK_MSG_ID_PARAM_SET = 23;
        private const byte MAVLINK_MSG_ID_COMMAND_LONG = 76;
        private const ushort MAVLINK_MSG_ID_COMMAND_ACK = 77;
        private const byte MAVLINK_MSG_ID_STATUSTEXT = 253;
        private const byte MAVLINK_MSG_ID_RC_CHANNELS = 65;

        // MAV_CMD IDs
        private const ushort MAV_CMD_DO_MOTOR_TEST = 209;
        private const ushort MAV_CMD_PREFLIGHT_CALIBRATION = 241;
        private const ushort MAV_CMD_PREFLIGHT_STORAGE = 245;
        private const ushort MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN = 246;
        private const ushort MAV_CMD_COMPONENT_ARM_DISARM = 400;
        private const ushort MAV_CMD_ACCELCAL_VEHICLE_POS = 42429;

        // CRC extras
        private const byte CRC_EXTRA_HEARTBEAT = 50;
        private const byte CRC_EXTRA_PARAM_REQUEST_READ = 214;
        private const byte CRC_EXTRA_PARAM_REQUEST_LIST = 159;
        private const byte CRC_EXTRA_PARAM_VALUE = 220;
        private const byte CRC_EXTRA_PARAM_SET = 168;
        private const byte CRC_EXTRA_COMMAND_LONG = 152;
        private const byte CRC_EXTRA_COMMAND_ACK = 143;
        private const byte CRC_EXTRA_STATUSTEXT = 83;
        private const byte CRC_EXTRA_RC_CHANNELS = 118;

        // MAV_TYPE and MAV_AUTOPILOT for GCS heartbeat
        private const byte MAV_TYPE_GCS = 6;
        private const byte MAV_AUTOPILOT_INVALID = 8;

        public event EventHandler<(byte SystemId, byte ComponentId)>? HeartbeatReceived;
        public event EventHandler<(string Name, float Value, ushort Index, ushort Count)>? ParamValueReceived;
        public event EventHandler<(ushort Command, byte Result)>? CommandAckReceived;
        public event EventHandler<(byte Severity, string Text)>? StatusTextReceived;
        public event EventHandler<HeartbeatData>? HeartbeatDataReceived;
        public event EventHandler<RcChannelsData>? RcChannelsReceived;

        public AsvMavlinkWrapper(ILogger logger)
        {
            _logger = logger;
        }

        public void Initialize(Stream inputStream, Stream outputStream)
        {
            _disposed = false;
            _inputStream = inputStream;
            _outputStream = outputStream;
            _rxBufferPos = 0;
            _cts = new CancellationTokenSource();

            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
            _heartbeatTask = Task.Run(() => GcsHeartbeatLoopAsync(_cts.Token));

            _logger.LogInformation("MAVLink wrapper initialized with GCS heartbeat");
        }

        private async Task GcsHeartbeatLoopAsync(CancellationToken token)
        {
            _logger.LogInformation("GCS heartbeat loop started");

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await SendGcsHeartbeatAsync(token);
                    await Task.Delay(1000, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error sending GCS heartbeat");
                }
            }

            _logger.LogInformation("GCS heartbeat loop ended");
        }

        private async Task SendGcsHeartbeatAsync(CancellationToken ct)
        {
            // HEARTBEAT payload (9 bytes):
            // [0-3] custom_mode (uint32)
            // [4]   type (uint8) - MAV_TYPE_GCS = 6
            // [5]   autopilot (uint8) - MAV_AUTOPILOT_INVALID = 8
            // [6]   base_mode (uint8)
            // [7]   system_status (uint8)
            // [8]   mavlink_version (uint8)

            var payload = new byte[9];
            payload[0] = 0; payload[1] = 0; payload[2] = 0; payload[3] = 0; // custom_mode = 0
            payload[4] = MAV_TYPE_GCS;
            payload[5] = MAV_AUTOPILOT_INVALID;
            payload[6] = 0; // base_mode
            payload[7] = 0; // system_status
            payload[8] = 3; // mavlink_version

            await SendMessageAsync(MAVLINK_MSG_ID_HEARTBEAT, payload, ct);
        }

        private async Task ReadLoopAsync(CancellationToken token)
        {
            var buffer = new byte[1024];
            try
            {
                _logger.LogInformation("MAVLink read loop started");
                while (!token.IsCancellationRequested && _inputStream != null)
                {
                    try
                    {
                        int bytesRead = await _inputStream.ReadAsync(buffer, 0, buffer.Length, token);
                        if (bytesRead > 0)
                        {
                            ProcessBytes(buffer, bytesRead);
                        }
                        else
                        {
                            await Task.Delay(10, token);
                        }
                    }
                    catch (IOException)
                    {
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MAVLink read loop error");
            }
            _logger.LogInformation("MAVLink read loop ended");
        }

        private void ProcessBytes(byte[] data, int length)
        {
            lock (_bufferLock)
            {
                for (int i = 0; i < length; i++)
                {
                    if (_rxBufferPos >= _rxBuffer.Length)
                    {
                        _rxBufferPos = 0;
                    }
                    _rxBuffer[_rxBufferPos++] = data[i];
                }
                ProcessBuffer();
            }
        }

        private void ProcessBuffer()
        {
            while (_rxBufferPos > 0)
            {
                int startIdx = -1;
                for (int i = 0; i < _rxBufferPos; i++)
                {
                    if (_rxBuffer[i] == MAVLINK_STX_V1 || _rxBuffer[i] == MAVLINK_STX_V2)
                    {
                        startIdx = i;
                        break;
                    }
                }

                if (startIdx < 0)
                {
                    _rxBufferPos = 0;
                    return;
                }

                if (startIdx > 0)
                {
                    Array.Copy(_rxBuffer, startIdx, _rxBuffer, 0, _rxBufferPos - startIdx);
                    _rxBufferPos -= startIdx;
                }

                if (_rxBufferPos < 8)
                    return;

                byte stx = _rxBuffer[0];
                int frameLen;

                if (stx == MAVLINK_STX_V1)
                {
                    byte payloadLen = _rxBuffer[1];
                    frameLen = 8 + payloadLen;
                }
                else
                {
                    if (_rxBufferPos < 12)
                        return;
                    byte payloadLen = _rxBuffer[1];
                    byte incompatFlags = _rxBuffer[2];
                    bool hasSignature = (incompatFlags & 0x01) != 0;
                    frameLen = 12 + payloadLen + (hasSignature ? 13 : 0);
                }

                if (_rxBufferPos < frameLen)
                    return;

                var frame = new byte[frameLen];
                Array.Copy(_rxBuffer, 0, frame, 0, frameLen);

                Array.Copy(_rxBuffer, frameLen, _rxBuffer, 0, _rxBufferPos - frameLen);
                _rxBufferPos -= frameLen;

                try
                {
                    if (stx == MAVLINK_STX_V1)
                        ProcessMavlink1Frame(frame);
                    else
                        ProcessMavlink2Frame(frame);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing MAVLink frame");
                }
            }
        }

        private void ProcessMavlink1Frame(byte[] frame)
        {
            byte payloadLen = frame[1];
            byte sysId = frame[3];
            byte compId = frame[4];
            byte msgId = frame[5];

            ushort crcCalc = CalculateCrc(frame, 1, payloadLen + 5, GetCrcExtra(msgId));
            ushort crcRecv = (ushort)(frame[6 + payloadLen] | (frame[7 + payloadLen] << 8));

            if (crcCalc != crcRecv)
            {
                return;
            }

            var payload = new byte[payloadLen];
            Array.Copy(frame, 6, payload, 0, payloadLen);

            HandleMessage(sysId, compId, msgId, payload);
        }

        private void ProcessMavlink2Frame(byte[] frame)
        {
            byte payloadLen = frame[1];
            byte sysId = frame[5];
            byte compId = frame[6];
            int msgId = frame[7] | (frame[8] << 8) | (frame[9] << 16);

            ushort crcCalc = CalculateCrc(frame, 1, 9 + payloadLen, GetCrcExtra((byte)(msgId & 0xFF)));
            int crcOffset = 10 + payloadLen;
            ushort crcRecv = (ushort)(frame[crcOffset] | (frame[crcOffset + 1] << 8));

            if (crcCalc != crcRecv)
            {
                return;
            }

            var payload = new byte[payloadLen];
            Array.Copy(frame, 10, payload, 0, payloadLen);

            HandleMessage(sysId, compId, (byte)msgId, payload);
        }

        private void HandleMessage(byte sysId, byte compId, byte msgId, byte[] payload)
        {
            switch (msgId)
            {
                case MAVLINK_MSG_ID_HEARTBEAT:
                    HandleHeartbeat(sysId, compId, payload);
                    break;
                case MAVLINK_MSG_ID_PARAM_VALUE:
                    HandleParamValue(payload);
                    break;
                case (byte)MAVLINK_MSG_ID_COMMAND_ACK:
                    HandleCommandAck(payload);
                    break;
                case MAVLINK_MSG_ID_STATUSTEXT:
                    HandleStatusText(payload);
                    break;
                case MAVLINK_MSG_ID_RC_CHANNELS:
                    HandleRcChannels(payload);
                    break;
            }
        }

        private void HandleHeartbeat(byte sysId, byte compId, byte[] payload)
        {
            // Skip GCS heartbeats
            if (compId == GCS_COMPONENT_ID || sysId == 0 || sysId == GCS_SYSTEM_ID)
                return;

            _targetSystemId = sysId;
            _targetComponentId = compId;

            _logger.LogDebug("Heartbeat from FC: sysid={SysId} compid={CompId}", sysId, compId);
            HeartbeatReceived?.Invoke(this, (sysId, compId));

            if (payload.Length >= 7)
            {
                uint customMode = BitConverter.ToUInt32(payload, 0);
                byte vehicleType = payload[4];
                byte autopilot = payload[5];
                byte baseMode = payload[6];

                var heartbeatData = new HeartbeatData
                {
                    SystemId = sysId,
                    ComponentId = compId,
                    CustomMode = customMode,
                    VehicleType = vehicleType,
                    Autopilot = autopilot,
                    BaseMode = baseMode,
                    IsArmed = (baseMode & 0x80) != 0
                };

                HeartbeatDataReceived?.Invoke(this, heartbeatData);
            }
        }

        private void HandleParamValue(byte[] payload)
        {
            if (payload.Length < 25)
            {
                return;
            }

            float value = BitConverter.ToSingle(payload, 0);
            ushort paramCount = BitConverter.ToUInt16(payload, 4);
            ushort paramIndex = BitConverter.ToUInt16(payload, 6);
            string name = Encoding.ASCII.GetString(payload, 8, 16).TrimEnd('\0');

            _logger.LogDebug("PARAM_VALUE: {Name}={Value} [{Index}/{Count}]", name, value, paramIndex + 1, paramCount);
            ParamValueReceived?.Invoke(this, (name, value, paramIndex, paramCount));
        }

        private void HandleCommandAck(byte[] payload)
        {
            if (payload.Length < 3)
            {
                return;
            }

            ushort command = BitConverter.ToUInt16(payload, 0);
            byte result = payload[2];

            _logger.LogDebug("COMMAND_ACK: cmd={Command} result={Result}", command, result);
            CommandAckReceived?.Invoke(this, (command, result));
        }

        private void HandleStatusText(byte[] payload)
        {
            if (payload.Length < 2)
                return;

            byte severity = payload[0];
            string text = Encoding.ASCII.GetString(payload, 1, Math.Min(50, payload.Length - 1)).TrimEnd('\0');

            _logger.LogDebug("STATUSTEXT: [{Severity}] {Text}", severity, text);
            StatusTextReceived?.Invoke(this, (severity, text));
        }

        private void HandleRcChannels(byte[] payload)
        {
            if (payload.Length < 40)
                return;

            var rcData = new RcChannelsData
            {
                TimeBootMs = BitConverter.ToUInt32(payload, 0),
                Channel1 = BitConverter.ToUInt16(payload, 4),
                Channel2 = BitConverter.ToUInt16(payload, 6),
                Channel3 = BitConverter.ToUInt16(payload, 8),
                Channel4 = BitConverter.ToUInt16(payload, 10),
                Channel5 = BitConverter.ToUInt16(payload, 12),
                Channel6 = BitConverter.ToUInt16(payload, 14),
                Channel7 = BitConverter.ToUInt16(payload, 16),
                Channel8 = BitConverter.ToUInt16(payload, 18),
                ChannelCount = payload[38],
                Rssi = payload[39]
            };

            RcChannelsReceived?.Invoke(this, rcData);
        }

        public async Task SendParamRequestListAsync(CancellationToken ct = default)
        {
            var payload = new byte[2];
            payload[0] = _targetSystemId != 0 ? _targetSystemId : (byte)1;
            payload[1] = _targetComponentId != 0 ? _targetComponentId : (byte)1;

            _logger.LogInformation("Sending PARAM_REQUEST_LIST to sysid={SysId} compid={CompId}", payload[0], payload[1]);
            await SendMessageAsync(MAVLINK_MSG_ID_PARAM_REQUEST_LIST, payload, ct);
        }

        public async Task SendParamRequestReadAsync(ushort paramIndex, CancellationToken ct = default)
        {
            var payload = new byte[20];
            payload[0] = (byte)(paramIndex & 0xFF);
            payload[1] = (byte)((paramIndex >> 8) & 0xFF);
            payload[2] = _targetSystemId != 0 ? _targetSystemId : (byte)1;
            payload[3] = _targetComponentId != 0 ? _targetComponentId : (byte)1;

            await SendMessageAsync(MAVLINK_MSG_ID_PARAM_REQUEST_READ, payload, ct);
        }

        public async Task SendParamSetAsync(string name, float value, CancellationToken ct = default)
        {
            var payload = new byte[23];
            BitConverter.GetBytes(value).CopyTo(payload, 0);
            payload[4] = _targetSystemId != 0 ? _targetSystemId : (byte)1;
            payload[5] = _targetComponentId != 0 ? _targetComponentId : (byte)1;

            var nameBytes = Encoding.ASCII.GetBytes(name);
            Array.Copy(nameBytes, 0, payload, 6, Math.Min(16, nameBytes.Length));
            payload[22] = 9;

            _logger.LogInformation("Sending PARAM_SET: {Name}={Value}", name, value);
            await SendMessageAsync(MAVLINK_MSG_ID_PARAM_SET, payload, ct);
        }

        public async Task SendMotorTestAsync(int motorInstance, int throttleType, float throttleValue,
            float timeout, int motorCount = 0, int testOrder = 0, CancellationToken ct = default)
        {
            _logger.LogInformation("Sending DO_MOTOR_TEST: motor={Motor} throttle={Throttle}", motorInstance, throttleValue);
            await SendCommandLongAsync(MAV_CMD_DO_MOTOR_TEST, motorInstance, throttleType, throttleValue,
                timeout, motorCount, testOrder, 0, ct);
        }

        /// <summary>
        /// Send MAV_CMD_PREFLIGHT_CALIBRATION command
        /// </summary>
        /// <param name="gyro">Gyroscope calibration (0=skip, 1=calibrate)</param>
        /// <param name="mag">Magnetometer calibration (0=skip, 1=calibrate)</param>
        /// <param name="groundPressure">Ground pressure/barometer calibration (0=skip, 1=calibrate)</param>
        /// <param name="airspeed">Radio/airspeed calibration (0=skip, 1=calibrate)</param>
        /// <param name="accel">Accelerometer calibration (0=skip, 1=calibrate, 2=board level, 3=simple)</param>
        public async Task SendPreflightCalibrationAsync(int gyro = 0, int mag = 0, int groundPressure = 0,
            int airspeed = 0, int accel = 0, CancellationToken ct = default)
        {
            _logger.LogInformation("Sending MAV_CMD_PREFLIGHT_CALIBRATION: gyro={Gyro} mag={Mag} baro={Baro} airspeed={Airspeed} accel={Accel}",
                gyro, mag, groundPressure, airspeed, accel);

            await SendCommandLongAsync(MAV_CMD_PREFLIGHT_CALIBRATION, gyro, mag, groundPressure, airspeed, accel, 0, 0, ct);
        }

        /// <summary>
        /// Send MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN command
        /// </summary>
        /// <param name="autopilot">0=do nothing, 1=reboot autopilot, 2=shutdown autopilot, 3=reboot to bootloader</param>
        /// <param name="companion">0=do nothing, 1=reboot companion, 2=shutdown companion</param>
        public async Task SendPreflightRebootAsync(int autopilot = 1, int companion = 0, CancellationToken ct = default)
        {
            _logger.LogInformation("Sending MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN: autopilot={Autopilot} companion={Companion}",
                autopilot, companion);

            await SendCommandLongAsync(MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN, autopilot, companion, 0, 0, 0, 0, 0, ct);
        }

        /// <summary>
        /// Send arm/disarm command
        /// </summary>
        /// <param name="arm">True to arm, false to disarm</param>
        /// <param name="force">True to force arm/disarm</param>
        public async Task SendArmDisarmAsync(bool arm, bool force = false, CancellationToken ct = default)
        {
            _logger.LogInformation("Sending MAV_CMD_COMPONENT_ARM_DISARM: arm={Arm} force={Force}", arm, force);

            await SendCommandLongAsync(MAV_CMD_COMPONENT_ARM_DISARM, arm ? 1 : 0, force ? 21196 : 0, 0, 0, 0, 0, 0, ct);
        }

        /// <summary>
        /// Send MAV_CMD_ACCELCAL_VEHICLE_POS command for accelerometer calibration
        /// </summary>
        /// <param name="position">Vehicle position (1-6 for different orientations)</param>
        public async Task SendAccelCalVehiclePosAsync(int position, CancellationToken ct = default)
        {
            _logger.LogInformation("Sending MAV_CMD_ACCELCAL_VEHICLE_POS: position={Position}", position);
            await SendCommandLongAsync(MAV_CMD_ACCELCAL_VEHICLE_POS, position, 0, 0, 0, 0, 0, 0, ct);
        }

        /// <summary>
        /// Send MAV_CMD_PREFLIGHT_STORAGE command to reset all parameters to default
        /// param1 = 0: Read params from storage
        /// param1 = 1: Write params to storage
        /// param1 = 2: Reset params to default
        /// </summary>
        public async Task SendResetParametersAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("Sending MAV_CMD_PREFLIGHT_STORAGE: param1=2 (reset to defaults)");

            await SendCommandLongAsync(MAV_CMD_PREFLIGHT_STORAGE, 2, 0, 0, 0, 0, 0, 0, ct);
        }

        private async Task SendCommandLongAsync(ushort command, float param1, float param2, float param3,
            float param4, float param5, float param6, float param7, CancellationToken ct = default)
        {
            var payload = new byte[33];
            BitConverter.GetBytes(param1).CopyTo(payload, 0);
            BitConverter.GetBytes(param2).CopyTo(payload, 4);
            BitConverter.GetBytes(param3).CopyTo(payload, 8);
            BitConverter.GetBytes(param4).CopyTo(payload, 12);
            BitConverter.GetBytes(param5).CopyTo(payload, 16);
            BitConverter.GetBytes(param6).CopyTo(payload, 20);
            BitConverter.GetBytes(param7).CopyTo(payload, 24);
            BitConverter.GetBytes(command).CopyTo(payload, 28);
            payload[30] = _targetSystemId != 0 ? _targetSystemId : (byte)1;
            payload[31] = _targetComponentId != 0 ? _targetComponentId : (byte)1;
            payload[32] = 0;

            await SendMessageAsync(MAVLINK_MSG_ID_COMMAND_LONG, payload, ct);
        }

        private async Task SendMessageAsync(byte msgId, byte[] payload, CancellationToken ct)
        {
            if (_outputStream == null)
            {
                _logger.LogWarning("Cannot send - no output stream");
                return;
            }

            var frame = new byte[8 + payload.Length];
            frame[0] = MAVLINK_STX_V1;
            frame[1] = (byte)payload.Length;
            frame[2] = _packetSequence++;
            frame[3] = GCS_SYSTEM_ID;
            frame[4] = GCS_COMPONENT_ID;
            frame[5] = msgId;
            Array.Copy(payload, 0, frame, 6, payload.Length);

            ushort crc = CalculateCrc(frame, 1, payload.Length + 5, GetCrcExtra(msgId));
            frame[6 + payload.Length] = (byte)(crc & 0xFF);
            frame[7 + payload.Length] = (byte)(crc >> 8);

            try
            {
                lock (_writeLock)
                {
                    _outputStream.Write(frame, 0, frame.Length);
                    _outputStream.Flush();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send MAVLink message {MsgId}", msgId);
            }

            await Task.CompletedTask;
        }

        private static ushort CalculateCrc(byte[] buffer, int offset, int length, byte crcExtra)
        {
            ushort crc = 0xFFFF;

            for (int i = offset; i < offset + length; i++)
            {
                byte tmp = (byte)(buffer[i] ^ (byte)(crc & 0xFF));
                tmp ^= (byte)(tmp << 4);
                crc = (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
            }

            byte tmpExtra = (byte)(crcExtra ^ (byte)(crc & 0xFF));
            tmpExtra ^= (byte)(tmpExtra << 4);
            crc = (ushort)((crc >> 8) ^ (tmpExtra << 8) ^ (tmpExtra << 3) ^ (tmpExtra >> 4));

            return crc;
        }

        private static byte GetCrcExtra(byte msgId) => msgId switch
        {
            MAVLINK_MSG_ID_HEARTBEAT => CRC_EXTRA_HEARTBEAT,
            MAVLINK_MSG_ID_PARAM_REQUEST_READ => CRC_EXTRA_PARAM_REQUEST_READ,
            MAVLINK_MSG_ID_PARAM_REQUEST_LIST => CRC_EXTRA_PARAM_REQUEST_LIST,
            MAVLINK_MSG_ID_PARAM_VALUE => CRC_EXTRA_PARAM_VALUE,
            MAVLINK_MSG_ID_PARAM_SET => CRC_EXTRA_PARAM_SET,
            MAVLINK_MSG_ID_COMMAND_LONG => CRC_EXTRA_COMMAND_LONG,
            (byte)MAVLINK_MSG_ID_COMMAND_ACK => CRC_EXTRA_COMMAND_ACK,
            MAVLINK_MSG_ID_STATUSTEXT => CRC_EXTRA_STATUSTEXT,
            MAVLINK_MSG_ID_RC_CHANNELS => CRC_EXTRA_RC_CHANNELS,
            _ => 0
        };

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                _cts?.Cancel();
                _readTask?.Wait(1000);
                _heartbeatTask?.Wait(1000);
            }
            catch { }

            _cts?.Dispose();
            _cts = null;
            _readTask = null;
            _heartbeatTask = null;
            _inputStream = null;
            _outputStream = null;
        }
    }

    public class HeartbeatData
    {
        public byte SystemId { get; set; }
        public byte ComponentId { get; set; }
        public uint CustomMode { get; set; }
        public byte VehicleType { get; set; }
        public byte Autopilot { get; set; }
        public byte BaseMode { get; set; }
        public bool IsArmed { get; set; }
    }

    public class RcChannelsData
    {
        public uint TimeBootMs { get; set; }
        public ushort Channel1 { get; set; }
        public ushort Channel2 { get; set; }
        public ushort Channel3 { get; set; }
        public ushort Channel4 { get; set; }
        public ushort Channel5 { get; set; }
        public ushort Channel6 { get; set; }
        public ushort Channel7 { get; set; }
        public ushort Channel8 { get; set; }
        public byte ChannelCount { get; set; }
        public byte Rssi { get; set; }

        public ushort GetChannel(int channelNumber) => channelNumber switch
        {
            1 => Channel1,
            2 => Channel2,
            3 => Channel3,
            4 => Channel4,
            5 => Channel5,
            6 => Channel6,
            7 => Channel7,
            8 => Channel8,
            _ => 0
        };
    }
}