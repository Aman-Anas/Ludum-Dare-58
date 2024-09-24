namespace Game.Networking;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Game.Setup;
using Game.World.Data;
using Godot;
using LiteNetLib;
using LiteNetLib.Utils;
using static NetHelper;
using static NetMessageUtil;

public partial class ServerManager : Node, INetEventListener
{
    public string CurrentSaveName { get; set; }

    public NetManager NetServer { get; set; }
    readonly NetDataWriter rejectWriter = new();

    public ServerData WorldData { get; set; }

    public ServerManager()
    {
        NetServer = new(this);
    }

    public bool StartServer(int port)
    {
        WorldData = WorldSaves.LoadWorld(CurrentSaveName);
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
        WorldData.SaveServerData();

        NetServer.Stop();
    }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        var loginData = DecodeData<LoginPacket>(request.Data);

        bool valid;

        if (WorldData.LoginData.TryGetValue(loginData.Username, out string actualPword))
        {
            valid = loginData.Password == actualPword;
        }
        else
        {
            // If the username is not in the registry, then let's add it
            WorldData.LoginData[loginData.Username] = loginData.Password;
            WorldData.PlayerData[loginData.Username] = new()
            {
                CurrentSectorID = 0,
                CurrentEntityID = 15 // TODO: Make a new player entity for each lad
            };
            valid = true;
        }

        if (valid)
        {
            var newPeer = request.Accept();
            var playerData = WorldData.PlayerData[loginData.Username];

            if (!WorldData.LoadedSectors.ContainsKey(playerData.CurrentSectorID))
            {
                // If the sector doesn't exist, load it up
                WorldData.LoadSector(playerData.CurrentSectorID);
            }

            var currentSector = WorldData.LoadedSectors[playerData.CurrentSectorID];

            // make a LivePlayerState to provide easy access to the player's current sector etc
            LivePlayerState state = new(newPeer, loginData.Username, currentSector, playerData);

            WorldData.ActivePlayers[loginData.Username] = newPeer;

            newPeer.Tag = state;

            GD.Print($"Accepted login from user {loginData.Username}");
            // newPeer.Send(
            //     // EncodeNetMessage(new ClientInitializer(Vector3.One, Vector3.Forward, false, [])),
            //     DeliveryMethod.ReliableUnordered
            // );
        }
        else
        {
            rejectWriter.Reset();
            rejectWriter.Put("stinky");
            request.Reject(rejectWriter);
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

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) { }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

    public void OnNetworkReceiveUnconnected(
        IPEndPoint remoteEndPoint,
        NetPacketReader reader,
        UnconnectedMessageType messageType
    ) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }
}
