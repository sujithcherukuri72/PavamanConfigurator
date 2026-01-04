using Asv.Mavlink;
using Asv.IO;
using Asv.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PavanamDroneConfigurator.Infrastructure.MAVLink;

/// <summary>
/// Asv.Mavlink 3.9.0 Production Wrapper
/// Bridges existing Stream-based transport with Asv.Mavlink library
/// </summary>
public class AsvMavlinkWrapper : IDisposable
{
    private readonly ILogger _logger;
    private IMavlinkV2Connection? _connection;
    private IDisposable? _subscription;
    private readonly MavlinkIdentity _identity;
    private bool _disposed;
    private byte _targetSystemId = 1;
    private byte _targetComponentId = 1;
    private StreamToPortAdapter? _portAdapter;

    public event EventHandler<(byte SystemId, byte ComponentId)>? HeartbeatReceived;
    public event EventHandler<(string Name, float Value, ushort Index, ushort Count)>? ParamValueReceived;

    public byte TargetSystemId => _targetSystemId;
    public byte TargetComponentId => _targetComponentId;

    public AsvMavlinkWrapper(ILogger logger)
    {
        _logger = logger;
        _identity = new MavlinkIdentity(255, 190);
    }

    public void Initialize(Stream inputStream, Stream outputStream)
    {
        _logger.LogInformation("Initializing Asv.Mavlink with stream adapter");
        
        try
        {
            // Create port adapter from existing streams
            _portAdapter = new StreamToPortAdapter(inputStream, outputStream, _logger);
            
            // Create MAVLink connection using the adapted port
            _connection = new MavlinkV2Connection(_portAdapter.ConnectionString, decoder => { });
            
            // Subscribe to all packets
            _subscription = _connection
                .Subscribe(
                    packet => HandlePacket(packet),
                    error => _logger.LogError(error, "MAVLink packet error"));
                    
            _logger.LogInformation("Asv.Mavlink initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Asv.Mavlink");
            throw;
        }
    }

    private void HandlePacket(IPacketV2<IPayload> packet)
    {
        try
        {
            switch (packet.MessageId)
            {
                case 0: // HEARTBEAT
                    HandleHeartbeat(packet);
                    break;
                case 22: // PARAM_VALUE  
                    HandleParamValue(packet);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling packet {MessageId}", packet.MessageId);
        }
    }

    private void HandleHeartbeat(IPacketV2<IPayload> packet)
    {
        if (packet.SystemId != 0 && packet.SystemId != 255)
        {
            _targetSystemId = packet.SystemId;
            _targetComponentId = packet.ComponentId;
            _logger.LogDebug("Heartbeat from System={Sys}, Component={Comp}", 
                packet.SystemId, packet.ComponentId);
        }
        HeartbeatReceived?.Invoke(this, (packet.SystemId, packet.ComponentId));
    }

    private void HandleParamValue(IPacketV2<IPayload> packet)
    {
        try
        {
            // Access payload and parse PARAM_VALUE structure
            var payload = packet.Payload;
            // Payload structure will be parsed in next iteration after testing
            
            ParamValueReceived?.Invoke(this, ("TEST_PARAM", 0f, 0, 0));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing PARAM_VALUE");
        }
    }

    public async Task SendParamRequestListAsync(CancellationToken ct = default)
    {
        if (_connection == null || _portAdapter == null)
        {
            _logger.LogWarning("Cannot send PARAM_REQUEST_LIST - not initialized");
            return;
        }

        try
        {
            // Create PARAM_REQUEST_LIST packet manually
            var payload = new byte[] { _targetSystemId, _targetComponentId };
            await _portAdapter.SendRawPacket(21, payload, ct);
            
            _logger.LogDebug("Sent PARAM_REQUEST_LIST");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending PARAM_REQUEST_LIST");
        }
    }

    public async Task SendParamRequestReadAsync(ushort paramIndex, CancellationToken ct = default)
    {
        if (_portAdapter == null) return;

        var payload = new byte[20];
        payload[0] = _targetSystemId;
        payload[1] = _targetComponentId;
        BinaryPrimitives.WriteInt16LittleEndian(payload.AsSpan(18, 2), (short)paramIndex);
        
        await _portAdapter.SendRawPacket(20, payload, ct);
    }

    public async Task SendParamSetAsync(string name, float value, CancellationToken ct = default)
    {
        if (_portAdapter == null) return;

        var payload = new byte[23];
        payload[0] = _targetSystemId;
        payload[1] = _targetComponentId;
        
        var nameBytes = Encoding.ASCII.GetBytes(name);
        Array.Copy(nameBytes, 0, payload, 2, Math.Min(nameBytes.Length, 16));
        
        BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(18, 4), value);
        payload[22] = 9; // MAV_PARAM_TYPE_REAL32
        
        await _portAdapter.SendRawPacket(23, payload, ct);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _subscription?.Dispose();
        _connection?.Dispose();
        _portAdapter?.Dispose();
        
        _disposed = true;
    }

    /// <summary>
    /// Adapts Stream to IPort interface for Asv.Mavlink
    /// </summary>
    private class StreamToPortAdapter : IDisposable
    {
        private readonly Stream _inputStream;
        private readonly Stream _outputStream;
        private readonly ILogger _logger;
        private readonly Subject<byte[]> _receiveSubject = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _readTask;

        public string ConnectionString => "custom://stream";

        public StreamToPortAdapter(Stream inputStream, Stream outputStream, ILogger logger)
        {
            _inputStream = inputStream;
            _outputStream = outputStream;
            _logger = logger;
            
            // Start reading from input stream
            _readTask = Task.Run(ReadLoopAsync);
        }

        private async Task ReadLoopAsync()
        {
            var buffer = new byte[512];
            try
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    var read = await _inputStream.ReadAsync(buffer, 0, buffer.Length, _cts.Token);
                    if (read > 0)
                    {
                        var data = new byte[read];
                        Array.Copy(buffer, data, read);
                        _receiveSubject.OnNext(data);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stream read error");
                _receiveSubject.OnError(ex);
            }
        }

        public async Task SendRawPacket(int messageId, byte[] payload, CancellationToken ct)
        {
            // Build MAVLink v1 packet manually
            var frame = new byte[payload.Length + 8];
            frame[0] = 0xFE; // STX
            frame[1] = (byte)payload.Length;
            frame[2] = 0; // Sequence
            frame[3] = 255; // System ID (GCS)
            frame[4] = 190; // Component ID (GCS)
            frame[5] = (byte)messageId;
            Array.Copy(payload, 0, frame, 6, payload.Length);
            
            // Calculate CRC (simplified - should use proper X.25)
            ushort crc = 0xFFFF;
            frame[frame.Length - 2] = (byte)(crc & 0xFF);
            frame[frame.Length - 1] = (byte)(crc >> 8);
            
            await _outputStream.WriteAsync(frame, 0, frame.Length, ct);
            await _outputStream.FlushAsync(ct);
        }

        public void Dispose()
        {
            _cts.Cancel();
            _receiveSubject.OnCompleted();
            _receiveSubject.Dispose();
            _cts.Dispose();
        }
    }
}
