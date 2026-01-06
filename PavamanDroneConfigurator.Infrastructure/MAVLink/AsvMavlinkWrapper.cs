using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PavamanDroneConfigurator.Infrastructure.MAVLink
{
    /// <summary>
    /// MAVLink protocol implementation matching Mission Planner's approach.
    /// Handles both MAVLink v1 and v2 frames with proper CRC validation.
    /// </summary>
    public sealed class AsvMavlinkWrapper : IDisposable
    {
        private readonly ILogger _logger;
        private Stream? _inputStream;
        private Stream? _outputStream;
        private CancellationTokenSource? _cts;
        private Task? _readTask;
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
        private const byte GCS_COMPONENT_ID = 190; // MAV_COMP_ID_MISSIONPLANNER
        
        // Message IDs
        private const byte MAVLINK_MSG_ID_HEARTBEAT = 0;
        private const byte MAVLINK_MSG_ID_PARAM_REQUEST_READ = 20;
        private const byte MAVLINK_MSG_ID_PARAM_REQUEST_LIST = 21;
        private const byte MAVLINK_MSG_ID_PARAM_VALUE = 22;
        private const byte MAVLINK_MSG_ID_PARAM_SET = 23;

        // CRC extras from MAVLink message definitions
        private const byte CRC_EXTRA_HEARTBEAT = 50;
        private const byte CRC_EXTRA_PARAM_REQUEST_READ = 214;
        private const byte CRC_EXTRA_PARAM_REQUEST_LIST = 159;
        private const byte CRC_EXTRA_PARAM_VALUE = 220;
        private const byte CRC_EXTRA_PARAM_SET = 168;

        public event EventHandler<(byte SystemId, byte ComponentId)>? HeartbeatReceived;
        public event EventHandler<(string Name, float Value, ushort Index, ushort Count)>? ParamValueReceived;

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
            _logger.LogInformation("MAVLink wrapper initialized");
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
                        // Connection closed
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Normal cancellation
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
                // Add new data to buffer
                for (int i = 0; i < length; i++)
                {
                    if (_rxBufferPos >= _rxBuffer.Length)
                    {
                        // Buffer overflow - reset
                        _rxBufferPos = 0;
                    }
                    _rxBuffer[_rxBufferPos++] = data[i];
                }

                // Process complete packets
                ProcessBuffer();
            }
        }

        private void ProcessBuffer()
        {
            while (_rxBufferPos > 0)
            {
                // Find start byte
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
                    // No start byte found - clear buffer
                    _rxBufferPos = 0;
                    return;
                }

                // Remove garbage before start byte
                if (startIdx > 0)
                {
                    Array.Copy(_rxBuffer, startIdx, _rxBuffer, 0, _rxBufferPos - startIdx);
                    _rxBufferPos -= startIdx;
                }

                // Check if we have enough data for header
                if (_rxBufferPos < 8)
                    return;

                byte stx = _rxBuffer[0];
                int frameLen;
                
                if (stx == MAVLINK_STX_V1)
                {
                    // MAVLink v1: STX + LEN + SEQ + SYSID + COMPID + MSGID + PAYLOAD + CRC(2)
                    byte payloadLen = _rxBuffer[1];
                    frameLen = 8 + payloadLen;
                }
                else // MAVLINK_STX_V2
                {
                    // MAVLink v2: STX + LEN + INCOMPAT + COMPAT + SEQ + SYSID + COMPID + MSGID(3) + PAYLOAD + CRC(2) + [SIG(13)]
                    if (_rxBufferPos < 12)
                        return;
                    byte payloadLen = _rxBuffer[1];
                    byte incompatFlags = _rxBuffer[2];
                    bool hasSignature = (incompatFlags & 0x01) != 0;
                    frameLen = 12 + payloadLen + (hasSignature ? 13 : 0);
                }

                // Check if we have complete frame
                if (_rxBufferPos < frameLen)
                    return;

                // Extract and process frame
                var frame = new byte[frameLen];
                Array.Copy(_rxBuffer, 0, frame, 0, frameLen);

                // Remove processed frame from buffer
                Array.Copy(_rxBuffer, frameLen, _rxBuffer, 0, _rxBufferPos - frameLen);
                _rxBufferPos -= frameLen;

                // Process the frame
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
            byte seq = frame[2];
            byte sysId = frame[3];
            byte compId = frame[4];
            byte msgId = frame[5];

            // Verify CRC
            ushort crcCalc = CalculateCrc(frame, 1, payloadLen + 5, GetCrcExtra(msgId));
            ushort crcRecv = (ushort)(frame[6 + payloadLen] | (frame[7 + payloadLen] << 8));

            if (crcCalc != crcRecv)
            {
                _logger.LogTrace("V1 CRC mismatch: msg={Msg} calc=0x{Calc:X4} recv=0x{Recv:X4}", msgId, crcCalc, crcRecv);
                return;
            }

            // Extract payload
            var payload = new byte[payloadLen];
            Array.Copy(frame, 6, payload, 0, payloadLen);

            HandleMessage(sysId, compId, msgId, payload);
        }

        private void ProcessMavlink2Frame(byte[] frame)
        {
            byte payloadLen = frame[1];
            byte incompatFlags = frame[2];
            byte compatFlags = frame[3];
            byte seq = frame[4];
            byte sysId = frame[5];
            byte compId = frame[6];
            int msgId = frame[7] | (frame[8] << 8) | (frame[9] << 16);

            // Verify CRC (for v2, CRC is calculated over bytes 1 to 9+payloadLen)
            ushort crcCalc = CalculateCrc(frame, 1, 9 + payloadLen, GetCrcExtra((byte)(msgId & 0xFF)));
            int crcOffset = 10 + payloadLen;
            ushort crcRecv = (ushort)(frame[crcOffset] | (frame[crcOffset + 1] << 8));

            if (crcCalc != crcRecv)
            {
                _logger.LogTrace("V2 CRC mismatch: msg={Msg} calc=0x{Calc:X4} recv=0x{Recv:X4}", msgId, crcCalc, crcRecv);
                return;
            }

            // Extract payload
            var payload = new byte[payloadLen];
            Array.Copy(frame, 10, payload, 0, payloadLen);

            HandleMessage(sysId, compId, (byte)msgId, payload);
        }

        private void HandleMessage(byte sysId, byte compId, byte msgId, byte[] payload)
        {
            switch (msgId)
            {
                case MAVLINK_MSG_ID_HEARTBEAT:
                    HandleHeartbeat(sysId, compId);
                    break;

                case MAVLINK_MSG_ID_PARAM_VALUE:
                    HandleParamValue(payload);
                    break;
            }
        }

        private void HandleHeartbeat(byte sysId, byte compId)
        {
            // Skip GCS heartbeats
            if (compId == GCS_COMPONENT_ID || sysId == 0)
                return;

            _targetSystemId = sysId;
            _targetComponentId = compId;
            
            _logger.LogDebug("Heartbeat: sysid={SysId} compid={CompId}", sysId, compId);
            HeartbeatReceived?.Invoke(this, (sysId, compId));
        }

        private void HandleParamValue(byte[] payload)
        {
            // PARAM_VALUE payload (25 bytes):
            // [0-3]   param_value (float)
            // [4-5]   param_count (uint16)
            // [6-7]   param_index (uint16)
            // [8-23]  param_id (char[16])
            // [24]    param_type (uint8)

            if (payload.Length < 25)
            {
                _logger.LogWarning("PARAM_VALUE payload too short: {Len}", payload.Length);
                return;
            }

            float value = BitConverter.ToSingle(payload, 0);
            ushort paramCount = BitConverter.ToUInt16(payload, 4);
            ushort paramIndex = BitConverter.ToUInt16(payload, 6);
            
            // Extract param name (null-terminated string)
            string name = Encoding.ASCII.GetString(payload, 8, 16).TrimEnd('\0');
            byte paramType = payload[24];

            _logger.LogDebug("PARAM_VALUE: {Name}={Value} [{Index}/{Count}] type={Type}", 
                name, value, paramIndex + 1, paramCount, paramType);

            ParamValueReceived?.Invoke(this, (name, value, paramIndex, paramCount));
        }

        public async Task SendParamRequestListAsync(CancellationToken ct = default)
        {
            // PARAM_REQUEST_LIST payload (2 bytes):
            // [0] target_system
            // [1] target_component
            var payload = new byte[2];
            payload[0] = _targetSystemId != 0 ? _targetSystemId : (byte)1;
            payload[1] = _targetComponentId != 0 ? _targetComponentId : (byte)1;

            _logger.LogInformation("Sending PARAM_REQUEST_LIST to sysid={SysId} compid={CompId}", payload[0], payload[1]);
            await SendMessageAsync(MAVLINK_MSG_ID_PARAM_REQUEST_LIST, payload, ct);
        }

        public async Task SendParamRequestReadAsync(ushort paramIndex, CancellationToken ct = default)
        {
            // PARAM_REQUEST_READ payload (20 bytes):
            // [0-1]   param_index (int16)
            // [2]     target_system
            // [3]     target_component
            // [4-19]  param_id (char[16]) - empty when using index
            var payload = new byte[20];
            
            // Write param_index as int16
            payload[0] = (byte)(paramIndex & 0xFF);
            payload[1] = (byte)((paramIndex >> 8) & 0xFF);
            payload[2] = _targetSystemId != 0 ? _targetSystemId : (byte)1;
            payload[3] = _targetComponentId != 0 ? _targetComponentId : (byte)1;
            // param_id bytes 4-19 stay zero when requesting by index

            await SendMessageAsync(MAVLINK_MSG_ID_PARAM_REQUEST_READ, payload, ct);
        }

        public async Task SendParamSetAsync(string name, float value, CancellationToken ct = default)
        {
            // PARAM_SET payload (23 bytes):
            // [0-3]   param_value (float)
            // [4]     target_system
            // [5]     target_component
            // [6-21]  param_id (char[16])
            // [22]    param_type (uint8) - 9 = MAV_PARAM_TYPE_REAL32
            var payload = new byte[23];
            
            BitConverter.GetBytes(value).CopyTo(payload, 0);
            payload[4] = _targetSystemId != 0 ? _targetSystemId : (byte)1;
            payload[5] = _targetComponentId != 0 ? _targetComponentId : (byte)1;
            
            var nameBytes = Encoding.ASCII.GetBytes(name);
            Array.Copy(nameBytes, 0, payload, 6, Math.Min(16, nameBytes.Length));
            
            payload[22] = 9; // MAV_PARAM_TYPE_REAL32

            _logger.LogInformation("Sending PARAM_SET: {Name}={Value}", name, value);
            await SendMessageAsync(MAVLINK_MSG_ID_PARAM_SET, payload, ct);
        }

        private async Task SendMessageAsync(byte msgId, byte[] payload, CancellationToken ct)
        {
            if (_outputStream == null)
            {
                _logger.LogWarning("Cannot send - no output stream");
                return;
            }

            // Build MAVLink v1 frame
            var frame = new byte[8 + payload.Length];
            frame[0] = MAVLINK_STX_V1;
            frame[1] = (byte)payload.Length;
            frame[2] = _packetSequence++;
            frame[3] = GCS_SYSTEM_ID;
            frame[4] = GCS_COMPONENT_ID;
            frame[5] = msgId;
            Array.Copy(payload, 0, frame, 6, payload.Length);

            // Calculate CRC
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

        /// <summary>
        /// Calculate MAVLink CRC (X.25 checksum)
        /// </summary>
        private static ushort CalculateCrc(byte[] buffer, int offset, int length, byte crcExtra)
        {
            ushort crc = 0xFFFF;

            for (int i = offset; i < offset + length; i++)
            {
                ref byte data = ref buffer[i];
                byte tmp = (byte)(data ^ (byte)(crc & 0xFF));
                tmp ^= (byte)(tmp << 4);
                crc = (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
            }

            // Include CRC extra
            {
                byte tmp = (byte)(crcExtra ^ (byte)(crc & 0xFF));
                tmp ^= (byte)(tmp << 4);
                crc = (ushort)((crc >> 8) ^ (tmp << 8) ^ (tmp << 3) ^ (tmp >> 4));
            }

            return crc;
        }

        private static byte GetCrcExtra(byte msgId) => msgId switch
        {
            MAVLINK_MSG_ID_HEARTBEAT => CRC_EXTRA_HEARTBEAT,
            MAVLINK_MSG_ID_PARAM_REQUEST_READ => CRC_EXTRA_PARAM_REQUEST_READ,
            MAVLINK_MSG_ID_PARAM_REQUEST_LIST => CRC_EXTRA_PARAM_REQUEST_LIST,
            MAVLINK_MSG_ID_PARAM_VALUE => CRC_EXTRA_PARAM_VALUE,
            MAVLINK_MSG_ID_PARAM_SET => CRC_EXTRA_PARAM_SET,
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
            }
            catch { }

            _cts?.Dispose();
            _cts = null;
            _readTask = null;
            _inputStream = null;
            _outputStream = null;
        }
   }
}
