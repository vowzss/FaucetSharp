﻿using veloce.shared.interceptors.client;
using veloce.shared.models;
using veloce.shared.packets;

namespace veloce.shared.channels.client;

/// <summary>
///     Represents a channel object designed for client communication.
/// </summary>
public interface IClientChannel : IChannel<IClientConfig, IClientPacketInterceptor>
{
    /// <summary>
    ///     Represents the object for encryption values.
    /// </summary>
    protected IClientEncryption Encryption { get; }

    /// <summary>
    ///     Non-blocking method to connect to the server.
    ///     <remarks>
    ///         This method will automatically initiate the handshake process with the server.
    ///     </remarks>
    /// </summary>
    public Task Connect();
    
    /// <summary>
    ///     Non-blocking method to disconnect from the server.
    ///     <remarks>
    ///         This method should be called to ensure proper termination.
    ///     </remarks>
    /// </summary>
    public Task Disconnect();

    /// <summary>
    ///     Non-blocking method to send a packet to a server.
    /// </summary>
    public Task Send(IPacket packet);
}