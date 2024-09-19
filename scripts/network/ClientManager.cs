namespace Game.Networking;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Godot;
using LiteNetLib;
using LiteNetLib.Utils;
using static NetHelper;
using static NetMessageUtil;

public partial class ClientManager : Node, INetEventListener
{
    public string ConnectAddress { get; private set; }
    public int ConnectPort { get; private set; }
    public string Username { get; set; }

    public NetManager NetClient { get; private set; }
    public NetPeer ServerLink { get; private set; }

    public Queue<Action> EventQueue { get; set; } = new();

    // Replace with a ClientData class for "world" info?
    public Dictionary<uint, EntityData> EntitiesData { get; set; } = [];
    public Dictionary<uint, INetEntity> Entities { get; set; } = [];

    public ClientManager()
    {
        NetClient = new(this) { UnsyncedEvents = true };
    }

    public bool StartClient(string address, int port, LoginPacket loginInfo)
    {
        ConnectAddress = address;
        ConnectPort = port;
        Username = loginInfo.Username;

        NetClient.Start();
        ServerLink = NetClient.Connect(
            ConnectAddress,
            ConnectPort,
            NetDataWriter.FromBytes(EncodeData(loginInfo), false)
        );

        return ServerLink != null;
    }

    public bool IsRunning() => NetClient.IsRunning;

    public override void _Process(double delta)
    {
        while (EventQueue.TryDequeue(out Action currentEvent))
        {
            currentEvent();
        }
    }

    public void Stop()
    {
        NetClient.Stop();
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
                SwitchPacket(reader, peer, null, this);
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

    public void OnConnectionRequest(ConnectionRequest request) { }

    public void SpawnEntity(EntityData data)
    {
        var newEntity = data.SpawnInstance(false);
        this.AddChild((Node3D)newEntity);

        Entities[data.EntityID] = newEntity;
    }

    public void DestroyEntity(uint entityID)
    {
        EntitiesData.Remove(entityID);
        if (Entities.Remove(entityID, out var entity))
        {
            entity.GetNode().QueueFree();
        }
    }
}
