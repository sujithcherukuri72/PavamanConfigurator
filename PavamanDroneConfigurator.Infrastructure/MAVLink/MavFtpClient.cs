using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Enums;
using PavamanDroneConfigurator.Core.Models;
using System.Text;

namespace PavamanDroneConfigurator.Infrastructure.MAVLink;

/// <summary>
/// MAVLink FTP client for file operations with the flight controller.
/// Implements the MAVLink FTP protocol for browsing and downloading files.
/// </summary>
public sealed class MavFtpClient : IDisposable
{
    private readonly ILogger _logger;
    private AsvMavlinkWrapper? _mavlink;
    private Stream? _inputStream;
    private Stream? _outputStream;
    
    private readonly object _lock = new();
    private readonly SemaphoreSlim _responseSemaphore = new(0, 1);
    
    private byte[]? _lastResponse;
    private MavFtpOpcode _lastOpcode;
    private MavFtpError _lastError;
    private ushort _sequenceNumber;
    private byte _sessionId;
    private bool _disposed;
    
    // MAVLink constants
    private const byte MAVLINK_STX_V2 = 0xFD;
    private const byte GCS_SYSTEM_ID = 255;
    private const byte GCS_COMPONENT_ID = 190;
    private const ushort MAVLINK_MSG_ID_FILE_TRANSFER_PROTOCOL = 110;
    private const byte CRC_EXTRA_FILE_TRANSFER_PROTOCOL = 84;
    
    // FTP packet structure
    private const int FTP_HEADER_SIZE = 12;
    private const int FTP_MAX_PAYLOAD_SIZE = 239; // 251 - 12 header bytes
    
    // Timeouts
    private const int DEFAULT_TIMEOUT_MS = 5000;
    private const int READ_TIMEOUT_MS = 2000;
    
    private byte _targetSystemId = 1;
    private byte _targetComponentId = 1;
    private byte _packetSequence;
    
    private readonly object _writeLock = new();
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public event EventHandler<(long BytesDownloaded, long TotalBytes)>? ProgressChanged;

    public MavFtpClient(ILogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Initializes the FTP client with stream connections.
    /// </summary>
    public void Initialize(Stream inputStream, Stream outputStream, byte targetSystemId = 1, byte targetComponentId = 1)
    {
        _inputStream = inputStream;
        _outputStream = outputStream;
        _targetSystemId = targetSystemId;
        _targetComponentId = targetComponentId;
        _cts = new CancellationTokenSource();
        _readTask = Task.Run(() => ReadLoopAsync(_cts.Token));
        
        _logger.LogInformation("MAVLink FTP client initialized");
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        var buffer = new byte[1024];
        var rxBuffer = new byte[4096];
        var rxBufferPos = 0;

        try
        {
            while (!token.IsCancellationRequested && _inputStream != null)
            {
                try
                {
                    int bytesRead = await _inputStream.ReadAsync(buffer, 0, buffer.Length, token);
                    if (bytesRead > 0)
                    {
                        // Add to buffer
                        for (int i = 0; i < bytesRead; i++)
                        {
                            if (rxBufferPos >= rxBuffer.Length)
                                rxBufferPos = 0;
                            rxBuffer[rxBufferPos++] = buffer[i];
                        }

                        // Process FTP responses
                        ProcessBuffer(rxBuffer, ref rxBufferPos);
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
            // Normal cancellation
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FTP read loop error");
        }
    }

    private void ProcessBuffer(byte[] rxBuffer, ref int rxBufferPos)
    {
        while (rxBufferPos > 0)
        {
            // Find MAVLink v2 start byte
            int startIdx = -1;
            for (int i = 0; i < rxBufferPos; i++)
            {
                if (rxBuffer[i] == MAVLINK_STX_V2)
                {
                    startIdx = i;
                    break;
                }
            }

            if (startIdx < 0)
            {
                rxBufferPos = 0;
                return;
            }

            if (startIdx > 0)
            {
                Array.Copy(rxBuffer, startIdx, rxBuffer, 0, rxBufferPos - startIdx);
                rxBufferPos -= startIdx;
            }

            if (rxBufferPos < 12)
                return;

            byte payloadLen = rxBuffer[1];
            int frameLen = 12 + payloadLen;

            if (rxBufferPos < frameLen)
                return;

            // Check if this is an FTP message
            int msgId = rxBuffer[7] | (rxBuffer[8] << 8) | (rxBuffer[9] << 16);
            if (msgId == MAVLINK_MSG_ID_FILE_TRANSFER_PROTOCOL)
            {
                var payload = new byte[payloadLen];
                Array.Copy(rxBuffer, 10, payload, 0, payloadLen);
                ProcessFtpResponse(payload);
            }

            // Remove processed frame
            Array.Copy(rxBuffer, frameLen, rxBuffer, 0, rxBufferPos - frameLen);
            rxBufferPos -= frameLen;
        }
    }

    private void ProcessFtpResponse(byte[] payload)
    {
        if (payload.Length < FTP_HEADER_SIZE + 3) // network bytes (3) + header
            return;

        // Skip network bytes (target_network, target_system, target_component)
        int offset = 3;
        
        // Parse FTP header
        ushort seqNumber = (ushort)(payload[offset] | (payload[offset + 1] << 8));
        byte session = payload[offset + 2];
        byte opcode = payload[offset + 3];
        byte size = payload[offset + 4];
        byte reqOpcode = payload[offset + 5];
        byte burstComplete = payload[offset + 6];
        // padding byte at offset + 7
        uint dataOffset = BitConverter.ToUInt32(payload, offset + 8);

        // Extract data
        var dataSize = Math.Min(size, payload.Length - offset - FTP_HEADER_SIZE);
        var data = new byte[dataSize];
        if (dataSize > 0)
        {
            Array.Copy(payload, offset + FTP_HEADER_SIZE, data, 0, dataSize);
        }

        lock (_lock)
        {
            _lastOpcode = (MavFtpOpcode)opcode;
            if (_lastOpcode == MavFtpOpcode.Nak && data.Length > 0)
            {
                _lastError = (MavFtpError)data[0];
            }
            else
            {
                _lastError = MavFtpError.None;
            }
            _lastResponse = data;
        }

        try { _responseSemaphore.Release(); } catch { }
    }

    /// <summary>
    /// Lists files and directories at the specified path.
    /// </summary>
    public async Task<List<FtpDirectoryEntry>> ListDirectoryAsync(string path, CancellationToken ct = default)
    {
        var entries = new List<FtpDirectoryEntry>();
        uint offset = 0;

        _logger.LogInformation("Listing directory: {Path}", path);

        while (!ct.IsCancellationRequested)
        {
            var response = await SendFtpCommandAsync(MavFtpOpcode.ListDirectory, path, offset, ct);
            
            if (response.Opcode == MavFtpOpcode.Nak)
            {
                if (response.Error == MavFtpError.EOF)
                    break; // End of listing
                if (response.Error == MavFtpError.FileNotFound)
                    break; // Directory not found
                    
                _logger.LogWarning("Directory listing failed: {Error}", response.Error);
                break;
            }

            if (response.Data == null || response.Data.Length == 0)
                break;

            // Parse directory entries (null-terminated strings)
            var entryStr = Encoding.ASCII.GetString(response.Data);
            var lines = entryStr.Split('\0', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var entry = ParseDirectoryEntry(line, path);
                if (entry != null)
                {
                    entries.Add(entry);
                    offset++;
                }
            }

            if (lines.Length == 0)
                break;
        }

        _logger.LogInformation("Found {Count} entries in {Path}", entries.Count, path);
        return entries;
    }

    private FtpDirectoryEntry? ParseDirectoryEntry(string line, string basePath)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        // Format: "D<name>" for directory, "F<name>\t<size>" for file, "S<name>" for skip
        var entry = new FtpDirectoryEntry();

        if (line.StartsWith("D"))
        {
            entry.Name = line.Substring(1);
            entry.IsDirectory = true;
            entry.Size = 0;
        }
        else if (line.StartsWith("F"))
        {
            var parts = line.Substring(1).Split('\t');
            entry.Name = parts[0];
            entry.IsDirectory = false;
            if (parts.Length > 1 && long.TryParse(parts[1], out var size))
                entry.Size = size;
        }
        else if (line.StartsWith("S"))
        {
            // Skip entry
            return null;
        }
        else
        {
            return null;
        }

        entry.FullPath = basePath.TrimEnd('/') + "/" + entry.Name;
        return entry;
    }

    /// <summary>
    /// Downloads a file from the flight controller.
    /// </summary>
    public async Task<bool> DownloadFileAsync(string remotePath, string localPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Downloading file: {Remote} -> {Local}", remotePath, localPath);

        // Open file
        var openResponse = await SendFtpCommandAsync(MavFtpOpcode.OpenFileRO, remotePath, 0, ct);
        if (openResponse.Opcode != MavFtpOpcode.Ack)
        {
            _logger.LogWarning("Failed to open file: {Error}", openResponse.Error);
            return false;
        }

        // Get file size from response
        long fileSize = 0;
        if (openResponse.Data != null && openResponse.Data.Length >= 4)
        {
            fileSize = BitConverter.ToUInt32(openResponse.Data, 0);
        }

        _sessionId = openResponse.Session;

        try
        {
            // Ensure directory exists
            var dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write);
            
            long bytesDownloaded = 0;
            uint offset = 0;

            while (!ct.IsCancellationRequested)
            {
                var readResponse = await SendFtpCommandAsync(MavFtpOpcode.ReadFile, null, offset, ct, _sessionId);
                
                if (readResponse.Opcode == MavFtpOpcode.Nak)
                {
                    if (readResponse.Error == MavFtpError.EOF)
                        break; // End of file
                        
                    _logger.LogWarning("Read failed at offset {Offset}: {Error}", offset, readResponse.Error);
                    return false;
                }

                if (readResponse.Data == null || readResponse.Data.Length == 0)
                    break;

                await fileStream.WriteAsync(readResponse.Data, 0, readResponse.Data.Length, ct);
                bytesDownloaded += readResponse.Data.Length;
                offset += (uint)readResponse.Data.Length;

                ProgressChanged?.Invoke(this, (bytesDownloaded, fileSize > 0 ? fileSize : bytesDownloaded));
            }

            _logger.LogInformation("Downloaded {Bytes} bytes", bytesDownloaded);
            return true;
        }
        finally
        {
            // Close session
            await SendFtpCommandAsync(MavFtpOpcode.TerminateSession, null, 0, ct, _sessionId);
        }
    }

    /// <summary>
    /// Deletes a file from the flight controller.
    /// </summary>
    public async Task<bool> DeleteFileAsync(string remotePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting file: {Path}", remotePath);

        var response = await SendFtpCommandAsync(MavFtpOpcode.RemoveFile, remotePath, 0, ct);
        
        if (response.Opcode == MavFtpOpcode.Ack)
        {
            _logger.LogInformation("File deleted: {Path}", remotePath);
            return true;
        }

        _logger.LogWarning("Failed to delete file: {Error}", response.Error);
        return false;
    }

    /// <summary>
    /// Resets all FTP sessions.
    /// </summary>
    public async Task ResetSessionsAsync(CancellationToken ct = default)
    {
        await SendFtpCommandAsync(MavFtpOpcode.ResetSessions, null, 0, ct);
    }

    private async Task<FtpResponse> SendFtpCommandAsync(MavFtpOpcode opcode, string? path, uint offset, CancellationToken ct, byte session = 0)
    {
        // Build FTP payload
        // Network bytes (3) + Header (12) + Data (up to 239)
        var payload = new byte[251];
        
        // Network bytes
        payload[0] = 0; // target_network
        payload[1] = _targetSystemId;
        payload[2] = _targetComponentId;
        
        // Header
        int headerOffset = 3;
        var seq = _sequenceNumber++;
        payload[headerOffset] = (byte)(seq & 0xFF);
        payload[headerOffset + 1] = (byte)(seq >> 8);
        payload[headerOffset + 2] = session;
        payload[headerOffset + 3] = (byte)opcode;
        
        // Data (path)
        byte dataSize = 0;
        if (!string.IsNullOrEmpty(path))
        {
            var pathBytes = Encoding.ASCII.GetBytes(path);
            dataSize = (byte)Math.Min(pathBytes.Length, FTP_MAX_PAYLOAD_SIZE);
            Array.Copy(pathBytes, 0, payload, headerOffset + FTP_HEADER_SIZE, dataSize);
        }
        
        payload[headerOffset + 4] = dataSize;
        payload[headerOffset + 5] = 0; // req_opcode
        payload[headerOffset + 6] = 0; // burst_complete
        payload[headerOffset + 7] = 0; // padding
        
        // Offset
        BitConverter.GetBytes(offset).CopyTo(payload, headerOffset + 8);

        // Clear previous response
        lock (_lock)
        {
            _lastResponse = null;
            _lastOpcode = MavFtpOpcode.None;
            _lastError = MavFtpError.None;
        }

        // Drain semaphore
        while (_responseSemaphore.CurrentCount > 0)
            await _responseSemaphore.WaitAsync(0);

        // Send message
        await SendFtpMessageAsync(payload, FTP_HEADER_SIZE + dataSize + 3, ct);

        // Wait for response
        var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(DEFAULT_TIMEOUT_MS);

        try
        {
            await _responseSemaphore.WaitAsync(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("FTP command timeout: {Opcode}", opcode);
            return new FtpResponse { Opcode = MavFtpOpcode.Nak, Error = MavFtpError.Fail };
        }

        lock (_lock)
        {
            return new FtpResponse
            {
                Opcode = _lastOpcode,
                Error = _lastError,
                Data = _lastResponse,
                Session = session
            };
        }
    }

    private async Task SendFtpMessageAsync(byte[] payload, int length, CancellationToken ct)
    {
        if (_outputStream == null)
        {
            _logger.LogWarning("Cannot send FTP message - no output stream");
            return;
        }

        // Build MAVLink v2 frame
        var frame = new byte[12 + length];
        frame[0] = MAVLINK_STX_V2;
        frame[1] = (byte)length;
        frame[2] = 0; // incompat_flags
        frame[3] = 0; // compat_flags
        frame[4] = _packetSequence++;
        frame[5] = GCS_SYSTEM_ID;
        frame[6] = GCS_COMPONENT_ID;
        frame[7] = (byte)(MAVLINK_MSG_ID_FILE_TRANSFER_PROTOCOL & 0xFF);
        frame[8] = (byte)((MAVLINK_MSG_ID_FILE_TRANSFER_PROTOCOL >> 8) & 0xFF);
        frame[9] = (byte)((MAVLINK_MSG_ID_FILE_TRANSFER_PROTOCOL >> 16) & 0xFF);
        
        Array.Copy(payload, 0, frame, 10, length);

        // Calculate CRC
        ushort crc = CalculateCrc(frame, 1, 9 + length, CRC_EXTRA_FILE_TRANSFER_PROTOCOL);
        frame[10 + length] = (byte)(crc & 0xFF);
        frame[11 + length] = (byte)(crc >> 8);

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
            _logger.LogError(ex, "Failed to send FTP message");
        }

        await Task.CompletedTask;
    }

    private static ushort CalculateCrc(byte[] buffer, int offset, int length, byte crcExtra)
    {
        ushort crc = 0xFFFF;

        for (int i = offset; i < offset + length; i++)
        {
            byte data = buffer[i];
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
        _responseSemaphore.Dispose();
        _inputStream = null;
        _outputStream = null;
    }

    private struct FtpResponse
    {
        public MavFtpOpcode Opcode;
        public MavFtpError Error;
        public byte[]? Data;
        public byte Session;
    }
}
