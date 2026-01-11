using System;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Asv.IO;
using Asv.Mavlink;
using Asv.Mavlink.V2.Common;
using Microsoft.Extensions.Logging;
using PavamanDroneConfigurator.Core.Interfaces;

namespace PavamanDroneConfigurator.Infrastructure.MAVLink;

/// <summary>
/// UDP implementation of MAVLink transport using asv-mavlink v3.9
/// </summary>
public class UdpMavlinkTransport : IMavlinkTransport
{
    private readonly ILogger<UdpMavlinkTransport> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly Subject<object> _messageSubject;
    private IMavlinkV2Connection? _connection;
    private IDisposable? _subscription;
    private bool _isConnected;
    private bool _disposed;

    public UdpMavlinkTransport(ILogger<UdpMavlinkTransport> logger, string host = "127.0.0.1", int port = 14550)
    {
        _logger = logger;
        _host = host;
        _port = port;
        _messageSubject = new Subject<object>();
    }

    public IObservable<object> OnMessageReceived => _messageSubject;

    public bool IsConnected => _isConnected;

    public async Task ConnectAsync(CancellationToken ct)
    {
        if (_isConnected)
        {
            _logger.LogWarning("Already connected");
            return;
        }

        try
        {
            _logger.LogInformation("Connecting to UDP {Host}:{Port}", _host, _port);

            // Create UDP client
            var udpClient = new UdpClient();
            await udpClient.ConnectAsync(_host, _port);

            // Create port with DataStream
            var port = PortFactory.Create($"udp://{_host}:{_port}");
            
            // Create MAVLink connection
            var config = new MavlinkV2ConnectionConfig
            {
                SystemId = 255, // GCS system ID
                ComponentId = 190, // GCS component ID
                HeartbeatTimeoutMs = 5000
            };

            _connection = new MavlinkV2Connection(port, new PacketV2Decoder(), config);

            // Subscribe to all messages
            _subscription = _connection.Where(_ => true).Subscribe(
                packet =>
                {
                    try
                    {
                        // Deserialize packet to message
                        var message = packet.Payload;
                        _messageSubject.OnNext(message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing received message");
                    }
                },
                error =>
                {
                    _logger.LogError(error, "Error in message stream");
                    _messageSubject.OnError(error);
                },
                () =>
                {
                    _logger.LogInformation("Message stream completed");
                    _messageSubject.OnCompleted();
                }
            );

            _isConnected = true;
            _logger.LogInformation("Connected to UDP {Host}:{Port}", _host, _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to UDP {Host}:{Port}", _host, _port);
            throw;
        }
    }

    public Task DisconnectAsync()
    {
        if (!_isConnected)
        {
            return Task.CompletedTask;
        }

        try
        {
            _logger.LogInformation("Disconnecting from UDP {Host}:{Port}", _host, _port);
            
            _subscription?.Dispose();
            _connection?.Dispose();
            
            _isConnected = false;
            _logger.LogInformation("Disconnected from UDP {Host}:{Port}", _host, _port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }

        return Task.CompletedTask;
    }

    public async Task SendMessageAsync(object message, CancellationToken ct)
    {
        if (!_isConnected || _connection == null)
        {
            throw new InvalidOperationException("Not connected");
        }

        try
        {
            // Send the message using asv-mavlink
            if (message is IPayload payload)
            {
                await _connection.Send(payload, ct);
                _logger.LogDebug("Sent message: {MessageType}", message.GetType().Name);
            }
            else
            {
                _logger.LogWarning("Message is not IPayload: {MessageType}", message.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message: {MessageType}", message.GetType().Name);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisconnectAsync().Wait();
        _messageSubject.Dispose();
        _disposed = true;
    }
}
