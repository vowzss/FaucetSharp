﻿using System.Net;
using Serilog;
using FaucetSharp.Gameplay.channels;
using FaucetSharp.Core.Channels.Client;
using FaucetSharp.Core.Channels.Server;
using FaucetSharp.Gameplay.Configs;

namespace FaucetSharp.Tests.Tests;

public static class GameUpdateRates
{
    public const int Fps = 16; // ~60 Hz (16ms per update)
    public const int BattleRoyal = 33; // ~30 Hz (33ms per update)
    public const int Moba = 50; // ~20 Hz (50ms per update)
    public const int Mmorpg = 100; // ~10 Hz (100ms per update)
    public const int Racing = 20; // ~50 Hz (20ms per update)
}

public abstract class AbstractTest
{
    protected static readonly Random Random = new();
    protected readonly IServerChannel Server;
    protected readonly IClientChannel Client;

    protected readonly ILogger Logger;

    protected AbstractTest()
    {
        var endpoint = new IPEndPoint(IPAddress.Loopback, 52512);
        var serverConfig = new FaucetServerConfig();
        var clientConfig = new FaucetClientConfig(Guid.NewGuid().ToString());
        
        Logger = new LoggerConfiguration()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss.fffd} {Level:u3}] > {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Create a server
        Server = new FaucetServerChannel(endpoint, serverConfig);
        Server.Start().Wait();

        // Create a client
        Client = new FaucetClientChannel(endpoint, clientConfig);
        Client.Connect().Wait();
    }

    public abstract Task Execute();
    
    protected async Task Stop()
    {
        Thread.Sleep(100000);
        await Client.Disconnect();
        Thread.Sleep(20000);
        await Server.Stop();
    }
}