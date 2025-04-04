﻿using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using veloce.shared.enums;
using veloce.shared.events;
using veloce.shared.handlers;
using veloce.shared.interceptors.server;
using veloce.shared.models;
using veloce.shared.packets;
using veloce.shared.utils;

namespace veloce.shared.channels.server;

public abstract class AbstractServerChannel : AbstractChannel<IServerConfig, IServerPacketInterceptor>, IServerChannel
{
    public ServerState State { get; protected set; } = ServerState.Unknown;
    public ServerStatus Status { get; protected set; } = ServerStatus.Unknown;
    public IServerSessionHandler SessionHandler { get; protected init; }

    public ITickingClock Clock { get; }
    public event TickEvent OnTick;
    public event TickMissedEvent OnTickMissed;

    protected AbstractServerChannel(IPEndPoint endPoint, IServerConfig config) : base(endPoint, config,true)
    {
        // Setup ticking clock
        Clock = new VeloceTickingClock(config.TickRate, Token);
        Clock.OnTick += () => OnTick?.Invoke();
        Clock.OnTickMissed += time => OnTickMissed?.Invoke(time);
    }

    public virtual Task Start()
    {
        Logger.Information("Starting...");
        Status = ServerStatus.Starting;

        Task.Run(Listen, Token);
        Task.Run(Clock.Tick, Token);

        Status = ServerStatus.Online;
        Logger.Information("Online!");
        
        return Task.CompletedTask;
    }

    public virtual Task Stop()
    {
        Logger.Information("Stopping...");
        Status = ServerStatus.Stopping;

        Signal.Cancel();
        Transport.Close();
        Semaphore.Dispose();
        Queue.Clear();
        Workers.Clear();

        Status = ServerStatus.Offline;
        Logger.Information("Offline!");
        
        return Task.CompletedTask;
    }

    public override async Task Listen()
    {
        var lastLogTime = DateTime.UtcNow;

        while (!IsShuttingDown())
        {
            if (Queue.Count >= Config.MaxProcessThreshold)
            {
                var currentTime = DateTime.UtcNow;
                if (currentTime - lastLogTime >= TimeSpan.FromSeconds(30))
                {
                    Logger.Warning("Too many packets are waiting to be processed.");
                    lastLogTime = currentTime;
                }
            }

            try
            {
                Queue.Enqueue(await Transport.ReceiveAsync(Token));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "An error occured while listening from transport.");
            }

            await Task.Delay(1, Token);
        }
    }

    public override async Task Process()
    {
        while (!IsShuttingDown())
        {
            await Semaphore.WaitAsync(Token);

            try
            {
                while (Queue.TryDequeue(out var rs))
                {
                    var session = SessionHandler.FindByEndpoint(rs.RemoteEndPoint)
                                  ?? SessionHandler.Register(rs.RemoteEndPoint);

                    var args = new DataReceiveArgs(rs.RemoteEndPoint, rs.Buffer);
                    PacketInterceptor.Accept(args, session!.Encryption);
                }
            }
            finally
            {
                Semaphore.Release();
                await Task.Delay(1, Token);
            }
        }
    }

    public async Task Send(IServerSession session, IPacket packet)
    {
        Logger.Information($"Sending packet:[{packet}]. Trace > ID:{session.Id}.");

        // Check if session is unhealthy but skip if client is in handshake
        if (!session.IsHealthy() && packet is not IHandshakePacket)
        {
            Logger.Warning($"Cancelled packet:[{packet}]' due to unhealthy session! Trace > ID:[{session.Id}].");
            return;
        }

        try
        {
            var data = Serializer.Write(packet, session.Encryption);
            await Transport.SendAsync(data, data.Length, session.EndPoint);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, $"Failed to transport packet:[{packet}]!");
        }
    }

    public async Task Broadcast(IPacket packet)
    {
        Logger.Information($"Broadcasting packet:[{packet}] to connected clients.");

        foreach (var session in SessionHandler.GetAll())
            await Send(session, packet);
    }
    
    public override bool IsShuttingDown()
    {
        return Status == ServerStatus.Stopping || base.IsShuttingDown();
    }
}