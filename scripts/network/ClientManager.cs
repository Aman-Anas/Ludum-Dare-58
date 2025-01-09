namespace Game.Networking;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Game.Entities;
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

    // Replace with a ClientData class for "world" info?

    public Dictionary<ulong, INetEntity> Entities { get; set; } = [];

    [Export(PropertyHint.File)]
    string controllablePlayerPath;

    public ClientManager()
    {
        NetClient = new(this);
    }

    public async Task<bool> StartClient(string address, int port, LoginPacket loginInfo)
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

        // TODO: in the future do this in an async way to properly display
        // a "connecting" message on the menu
        while (ServerLink.ConnectionState == ConnectionState.Outgoing)
        {
            NetClient.PollEvents();
            await Task.Delay(10);
        }

        return ServerLink.ConnectionState != ConnectionState.Disconnected;
    }

    public bool IsRunning() => NetClient.IsRunning;

    public override void _Process(double delta)
    {
        NetClient.PollEvents();
    }

    public void Stop()
    {
        NetClient.Stop();

        // Destroy all the currently loaded entities
        foreach (var entity in Entities.Values)
        {
            entity.Data.DestroyEntity();
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
                SwitchPacket(reader, peer, null, this);
                break;
        }
        reader.Recycle();
    }

    public void OnPeerConnected(NetPeer peer) { }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        if (ServerLink.ConnectionState != ConnectionState.Connected)
        {
            Manager.Instance.ExitToTitle(); // Calls Stop()
        }
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) { }

    public void OnNetworkReceiveUnconnected(
        IPEndPoint remoteEndPoint,
        NetPacketReader reader,
        UnconnectedMessageType messageType
    ) { }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) { }

    public void OnConnectionRequest(ConnectionRequest request)
    {
        request.RejectForce();
    }

    public void SpawnEntity(EntityData data)
    {
        data.Client = this;
        var newEntity = data.SpawnInstance(false);
        this.AddChild(newEntity.GetNode());

        Entities[data.EntityID] = newEntity;
    }

    public void RemoveEntity(ulong entityID)
    {
        if (Entities.Remove(entityID, out var entity))
        {
            var node = entity.GetNode();
            if (IsInstanceValid(node))
            {
                node.QueueFree();
            }
        }
    }

    public void InitializeScene(ClientInitializer initData)
    {
        // Clear out all old entities.
        // TODO: Maybe call a method to cleanup terrain stuff

        foreach (var entity in Entities.Values)
        {
            entity.Data.DestroyEntity();
        }

        // Spawn in all the entities from our new data set
        foreach (var entityData in initData.EntitiesData.Values)
        {
            if (entityData.EntityID == initData.PlayerEntityID)
            {
                // Spawn our player entity here instead
                // (for now we just overwrite the client scene with our controllable player)
                entityData.ClientScene = controllablePlayerPath;
            }

            SpawnEntity(entityData);
        }
    }
}
