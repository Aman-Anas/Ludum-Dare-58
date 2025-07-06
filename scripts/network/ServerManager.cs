namespace Game.Networking;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Game.Entities;
using Game.Setup;
using Game.World.Data;
using Godot;
using LiteNetLib;
using LiteNetLib.Utils;
using static NetHelper;
using static NetMessageUtil;

public partial class ServerManager : Node, INetEventListener
{
    // Set externally
    public string? CurrentSaveName { get; set; }

    public NetManager NetServer { get; set; }
    readonly NetDataWriter rejectWriter = new();

    // Set on loading up a server
    public ServerData WorldData { get; set; } = null!;

    [Export]
    PlayerEntityData playerTemplate = null!;

    [Export(PropertyHint.File)]
    string testSceneTemplate = null!;

    public ServerManager()
    {
        NetServer = new(this);
    }

    public bool StartServer(int port)
    {
        WorldData = WorldSaves.LoadWorld(CurrentSaveName!);

        Sector newSector;
        if (!WorldData.SectorMetadata.ContainsKey(0))
        {
            newSector = WorldData.AddNewSector("Home", new(false, 0, Vector3.Zero));
            newSector.LoadFromTemplate(testSceneTemplate);
        }
        else
        {
            newSector = WorldData.LoadSector(0);
        }
        GD.Print("home sector ID: ", newSector.SectorID);

        return NetServer.Start(port);
    }

    public bool IsRunning() => NetServer.IsRunning;

    public override void _Process(double delta)
    {
        NetServer.PollEvents();
    }

    public SubViewport GetNewSectorViewport()
    {
        var newViewport = new SubViewport { OwnWorld3D = true };
        AddChild(newViewport);
        return newViewport;
    }

    public void Stop()
    {
        WorldData.UnloadAllSectors();
        WorldData.SaveServerData();
        NetServer.Stop();
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        var loginData = DecodeData<LoginPacket>(request.Data);
        GD.Print("Received request from user ", loginData.Username);

        if (WorldData.ValidatePlayer(loginData, playerTemplate, out var playerID))
        {
            var newPeer = request.Accept();
            var playerData = WorldData.PlayerData[playerID];

            if (!WorldData.LoadedSectors.ContainsKey(playerData.CurrentSectorID))
            {
                // If the sector doesn't exist, load it up
                WorldData.LoadSector(playerData.CurrentSectorID);
            }

            // Now the sector has been loaded
            var currentSector = WorldData.LoadedSectors[playerData.CurrentSectorID];

            // make a LivePlayerState to provide easy access to the player's current sector etc
            LivePlayerState state = new(newPeer, playerData.PlayerID, currentSector, playerData);
            newPeer.Tag = state;

            WorldData.ActivePlayers[loginData.Username] = newPeer;
            currentSector.Players.Add(playerID, newPeer);

            GD.Print($"Accepted login from user {loginData.Username}");

            currentSector.PlayerConnect(newPeer);
        }
        else
        {
            rejectWriter.Reset();
            rejectWriter.Put("stinky");
            request.RejectForce(rejectWriter);
            GD.Print($"Rejected login for user {loginData.Username}");
        }
    }

    public void OnNetworkReceive(
        NetPeer peer,
        NetPacketReader reader,
        byte channelNumber,
        DeliveryMethod deliveryMethod
    )
    {
        // Using a switch, in case we want to process other channels differently
        switch (channelNumber)
        {
            case 0:
                SwitchPacket(reader, peer, this, null);
                break;
        }
        reader.Recycle();
    }

    public void OnPeerConnected(NetPeer peer) { }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        WorldData.PlayerDisconnect(peer);
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

    public void OnNetworkReceiveUnconnected(
        IPEndPoint remoteEndPoint,
        NetPacketReader reader,
        UnconnectedMessageType messageType
    ) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
}
