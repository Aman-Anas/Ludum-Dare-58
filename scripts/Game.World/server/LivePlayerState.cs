namespace Game.World.Data;

using LiteNetLib;

// Data to be stored about a player that is live
public class LivePlayerState(NetPeer Peer, ulong PlayerID, Sector CurrentSector, PlayerData Data)
{
    public NetPeer Peer { get; init; } = Peer;
    public ulong PlayerID { get; init; } = PlayerID;
    public Sector CurrentSector { get; set; } = CurrentSector;
    public PlayerData Data { get; init; } = Data;
}

public static class PlayerStateExt
{
    public static LivePlayerState GetPlayerState(this NetPeer peer)
    {
        return (LivePlayerState)peer.Tag;
    }
}
