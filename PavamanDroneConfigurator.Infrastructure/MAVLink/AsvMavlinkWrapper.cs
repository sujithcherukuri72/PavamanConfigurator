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

        private byte _packetSequence;

        private byte _targetSystemId = 1;
        private byte _targetComponentId = 1;

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
        private const byte MAVLINK_MSG_ID_COMMAND_ACK = 77;
        private const byte MAVLINK_MSG_ID_STATUSTEXT = 253;
        private const byte MAVLINK_MSG_ID_RC_CHANNELS = 65;

        // Commands
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

        // Events
        public event EventHandler<(byte SystemId, byte ComponentId)>? HeartbeatReceived;
        public event EventHandler<HeartbeatData>? HeartbeatDataReceived;
        public event EventHandler<(string Name, float Value, ushort Index, ushort Count)>? ParamValueReceived;
        public event EventHandler<(ushort Command, byte Result)>? CommandAckReceived;
        public event EventHandler<(byte Severity, string Text)>? StatusTextReceived;
        public event EventHandler<RcChannelsData>? RcChannelsReceived;

        public AsvMavlinkWrapper(ILogger logger)
        {
            _logger = logger;
        }

        #region Initialization

        public void Initialize(Stream inputStream, Stream outputStream)
        {
            _inputStream = inputStream;
            _outputStream = outputStream;

            _cts = new CancellationTokenSource();

            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
            _heartbeatTask = Task.Run(() => GcsHeartbeatLoopAsync(_cts.Token));

            _logger.LogInformation("MAVLink wrapper initialized (Mission Planner compatible)");
        }

        private async Task GcsHeartbeatLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await SendGcsHeartbeatAsync(token);
                await Task.Delay(1000, token);
            }
        }

        private async Task SendGcsHeartbeatAsync(CancellationToken ct)
        {
            var payload = new byte[9];
            payload[4] = 6; // MAV_TYPE_GCS
            payload[5] = 8; // MAV_AUTOPILOT_INVALID
            payload[8] = 3; // MAVLink version

            await SendMessageAsync(MAVLINK_MSG_ID_HEARTBEAT, payload, ct);
        }

        #endregion

        #region Read / Parse

        private async Task ReadLoopAsync(CancellationToken token)
        {
            var buffer = new byte[1024];

            while (!token.IsCancellationRequested && _inputStream != null)
            {
                int read = await _inputStream.ReadAsync(buffer, 0, buffer.Length, token);
                if (read > 0)
                    ProcessBytes(buffer, read);
            }
        }

        private void ProcessBytes(byte[] data, int length)
        {
            lock (_bufferLock)
            {
                for (int i = 0; i < length; i++)
                {
                    if (_rxBufferPos >= _rxBuffer.Length)
                        _rxBufferPos = 0;

                    _rxBuffer[_rxBufferPos++] = data[i];
                }

                ProcessBuffer();
            }
        }

        private void ProcessBuffer()
        {
            while (_rxBufferPos > 8)
            {
                if (_rxBuffer[0] != MAVLINK_STX_V1)
                {
                    Array.Copy(_rxBuffer, 1, _rxBuffer, 0, --_rxBufferPos);
                    continue;
                }

                int payloadLen = _rxBuffer[1];
                int frameLen = payloadLen + 8;

                if (_rxBufferPos < frameLen)
                    return;

                var frame = new byte[frameLen];
                Array.Copy(_rxBuffer, frame, frameLen);
                Array.Copy(_rxBuffer, frameLen, _rxBuffer, 0, _rxBufferPos - frameLen);
                _rxBufferPos -= frameLen;

                ProcessFrame(frame);
            }
        }

        private void ProcessFrame(byte[] frame)
        {
            byte sysId = frame[3];
            byte compId = frame[4];
            byte msgId = frame[5];
            byte payloadLen = frame[1];

            ushort crcCalc = CalculateCrc(frame, 1, payloadLen + 5, GetCrcExtra(msgId));
            ushort crcRecv = (ushort)(frame[6 + payloadLen] | (frame[7 + payloadLen] << 8));

            if (crcCalc != crcRecv)
                return;

            var payload = new byte[payloadLen];
            Array.Copy(frame, 6, payload, 0, payloadLen);

            HandleMessage(sysId, compId, msgId, payload);
        }

        #endregion

        #region Message Handling

        private void HandleMessage(byte sysId, byte compId, byte msgId, byte[] payload)
        {
            switch (msgId)
            {
                case MAVLINK_MSG_ID_HEARTBEAT: HandleHeartbeat(sysId, compId, payload); break;
                case MAVLINK_MSG_ID_PARAM_VALUE: HandleParamValue(payload); break;
                case MAVLINK_MSG_ID_COMMAND_ACK: HandleCommandAck(payload); break;
                case MAVLINK_MSG_ID_STATUSTEXT: HandleStatusText(payload); break;
                case MAVLINK_MSG_ID_RC_CHANNELS: HandleRcChannels(payload); break;
            }
        }

        private void HandleHeartbeat(byte sysId, byte compId, byte[] payload)
        {
            if (compId == GCS_COMPONENT_ID || sysId == 0) return;

            _targetSystemId = sysId;
            _targetComponentId = compId;

            HeartbeatReceived?.Invoke(this, (sysId, compId));

            if (payload.Length < 9) return;

            var hb = new HeartbeatData
            {
                SystemId = sysId,
                ComponentId = compId,
                CustomMode = BitConverter.ToUInt32(payload, 0),
                VehicleType = payload[4],
                Autopilot = payload[5],
                BaseMode = payload[6],
                SystemStatus = payload[7],
                MavlinkVersion = payload[8],
                IsArmed = (payload[6] & 0x80) != 0
            };

            HeartbeatDataReceived?.Invoke(this, hb);
        }

        private void HandleParamValue(byte[] payload)
        {
            float value = BitConverter.ToSingle(payload, 0);
            ushort count = BitConverter.ToUInt16(payload, 4);
            ushort index = BitConverter.ToUInt16(payload, 6);
            string name = Encoding.ASCII.GetString(payload, 8, 16).TrimEnd('\0');

            ParamValueReceived?.Invoke(this, (name, value, index, count));
        }

        private void HandleCommandAck(byte[] payload)
        {
            ushort cmd = BitConverter.ToUInt16(payload, 0);
            byte result = payload[2];
            CommandAckReceived?.Invoke(this, (cmd, result));
        }

        private void HandleStatusText(byte[] payload)
        {
            byte severity = payload[0];
            string text = Encoding.ASCII.GetString(payload, 1, payload.Length - 1).TrimEnd('\0');
            StatusTextReceived?.Invoke(this, (severity, text));
        }

        private void HandleRcChannels(byte[] payload)
        {
            var rc = new RcChannelsData
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

            RcChannelsReceived?.Invoke(this, rc);
        }

        #endregion

        #region Commands

        public Task SendParamRequestListAsync(CancellationToken ct = default)
            => SendMessageAsync(MAVLINK_MSG_ID_PARAM_REQUEST_LIST,
                new byte[] { _targetSystemId, _targetComponentId }, ct);

        public Task SendParamSetAsync(string name, float value, CancellationToken ct = default)
        {
            var payload = new byte[23];
            BitConverter.GetBytes(value).CopyTo(payload, 0);
            payload[4] = _targetSystemId;
            payload[5] = _targetComponentId;
            Encoding.ASCII.GetBytes(name).CopyTo(payload, 6);
            payload[22] = 9;
            return SendMessageAsync(MAVLINK_MSG_ID_PARAM_SET, payload, ct);
        }

        public Task SendPreflightCalibrationAsync(int gyro, int mag, int baro, int airspeed, int accel, CancellationToken ct = default)
            => SendCommandLongAsync(MAV_CMD_PREFLIGHT_CALIBRATION, gyro, mag, baro, airspeed, accel, 0, 0, ct);

        public Task SendAccelCalVehiclePosAsync(int pos, CancellationToken ct = default)
            => SendCommandLongAsync(MAV_CMD_ACCELCAL_VEHICLE_POS, pos, 0, 0, 0, 0, 0, 0, ct);

        public Task SendMotorTestAsync(int motor, int type, float throttle, float timeout, int count = 0, int order = 0, CancellationToken ct = default)
            => SendCommandLongAsync(MAV_CMD_DO_MOTOR_TEST, motor, type, throttle, timeout, count, order, 0, ct);

        public Task SendArmDisarmAsync(bool arm, bool force, CancellationToken ct = default)
            => SendCommandLongAsync(MAV_CMD_COMPONENT_ARM_DISARM, arm ? 1 : 0, force ? 21196 : 0, 0, 0, 0, 0, 0, ct);

        public Task SendResetParametersAsync(CancellationToken ct = default)
            => SendCommandLongAsync(MAV_CMD_PREFLIGHT_STORAGE, 2, 0, 0, 0, 0, 0, 0, ct);

        private async Task SendCommandLongAsync(ushort cmd, float p1, float p2, float p3, float p4, float p5, float p6, float p7, CancellationToken ct)
        {
            var payload = new byte[33];
            BitConverter.GetBytes(p1).CopyTo(payload, 0);
            BitConverter.GetBytes(p2).CopyTo(payload, 4);
            BitConverter.GetBytes(p3).CopyTo(payload, 8);
            BitConverter.GetBytes(p4).CopyTo(payload, 12);
            BitConverter.GetBytes(p5).CopyTo(payload, 16);
            BitConverter.GetBytes(p6).CopyTo(payload, 20);
            BitConverter.GetBytes(p7).CopyTo(payload, 24);
            BitConverter.GetBytes(cmd).CopyTo(payload, 28);
            payload[30] = _targetSystemId;
            payload[31] = _targetComponentId;

            await SendMessageAsync(MAVLINK_MSG_ID_COMMAND_LONG, payload, ct);
        }

        #endregion

        #region MAVLink Send + CRC

        private async Task SendMessageAsync(byte msgId, byte[] payload, CancellationToken ct)
        {
            if (_outputStream == null) return;

            var frame = new byte[8 + payload.Length];
            frame[0] = MAVLINK_STX_V1;
            frame[1] = (byte)payload.Length;
            frame[2] = _packetSequence++;
            frame[3] = GCS_SYSTEM_ID;
            frame[4] = GCS_COMPONENT_ID;
            frame[5] = msgId;
            Array.Copy(payload, 0, frame, 6, payload.Length);

            ushort crc = CalculateCrc(frame, 1, payload.Length + 5, GetCrcExtra(msgId));
            frame[^2] = (byte)(crc & 0xFF);
            frame[^1] = (byte)(crc >> 8);

            lock (_writeLock)
            {
                _outputStream.Write(frame);
                _outputStream.Flush();
            }

            await Task.CompletedTask;
        }

        private static ushort CalculateCrc(byte[] buffer, int offset, int length, byte extra)
        {
            ushort crc = 0xFFFF;

            for (int i = offset; i < offset + length; i++)
            {
                byte tmp = (byte)(buffer[i] ^ crc);
                tmp ^= (byte)(tmp << 4);
                crc = (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
            }

            byte t = (byte)(extra ^ crc);
            t ^= (byte)(t << 4);
            crc = (ushort)((crc >> 8) ^ (t << 8) ^ (t << 3) ^ (t >> 4));
            return crc;
        }

        private static byte GetCrcExtra(byte id) => id switch
        {
            MAVLINK_MSG_ID_HEARTBEAT => CRC_EXTRA_HEARTBEAT,
            MAVLINK_MSG_ID_PARAM_REQUEST_READ => CRC_EXTRA_PARAM_REQUEST_READ,
            MAVLINK_MSG_ID_PARAM_REQUEST_LIST => CRC_EXTRA_PARAM_REQUEST_LIST,
            MAVLINK_MSG_ID_PARAM_VALUE => CRC_EXTRA_PARAM_VALUE,
            MAVLINK_MSG_ID_PARAM_SET => CRC_EXTRA_PARAM_SET,
            MAVLINK_MSG_ID_COMMAND_LONG => CRC_EXTRA_COMMAND_LONG,
            MAVLINK_MSG_ID_COMMAND_ACK => CRC_EXTRA_COMMAND_ACK,
            MAVLINK_MSG_ID_STATUSTEXT => CRC_EXTRA_STATUSTEXT,
            MAVLINK_MSG_ID_RC_CHANNELS => CRC_EXTRA_RC_CHANNELS,
            _ => 0
        };

        #endregion

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _cts?.Cancel();
            _readTask?.Wait(500);
            _heartbeatTask?.Wait(500);
            _cts?.Dispose();
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
        public byte SystemStatus { get; set; }
        public byte MavlinkVersion { get; set; }
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
    }
}
