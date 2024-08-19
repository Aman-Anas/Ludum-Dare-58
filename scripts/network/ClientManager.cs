namespace Game.Networking;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Godot;
using LiteNetLib;
using LiteNetLib.Utils;
using static NetMessageProcessor;
using static NetUtils;

public partial class ClientManager : Node, INetEventListener
{
    public string ConnectAddress { get; private set; }
    public int ConnectPort { get; private set; }

    public NetManager NetClient { get; private set; }

    public NetPeer Server { get; private set; }

    public Queue<Action> EventQueue { get; set; } = new();

    public ClientManager()
    {
        NetClient = new(this) { UnsyncedEvents = true };
    }

    public bool StartClient(string address, int port, LoginPacket loginInfo)
    {
        ConnectAddress = address;
        ConnectPort = port;

        NetClient.Start();
        Server = NetClient.Connect(
            ConnectAddress,
            ConnectPort,
            NetDataWriter.FromBytes(EncodeData(loginInfo), false)
        );

        return Server != null;
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
}
