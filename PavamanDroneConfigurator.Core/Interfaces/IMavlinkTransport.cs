using System;
using System.Threading;
using System.Threading.Tasks;

namespace PavamanDroneConfigurator.Core.Interfaces;

/// <summary>
/// MAVLink transport interface for sending and receiving messages
/// Supports: Serial, UDP, TCP
/// </summary>
public interface IMavlinkTransport : IDisposable
{
    /// <summary>
    /// Send a MAVLink message asynchronously
    /// </summary>
    /// <param name="message">The MAVLink message to send</param>
    /// <param name="ct">Cancellation token</param>
    Task SendMessageAsync(object message, CancellationToken ct);
    
    /// <summary>
    /// Observable stream of received MAVLink messages
    /// </summary>
    IObservable<object> OnMessageReceived { get; }
    
    /// <summary>
    /// Connect to the MAVLink endpoint
    /// </summary>
    Task ConnectAsync(CancellationToken ct);
    
    /// <summary>
    /// Disconnect from the MAVLink endpoint
    /// </summary>
    Task DisconnectAsync();
    
    /// <summary>
    /// Whether the transport is connected
    /// </summary>
    bool IsConnected { get; }
}
