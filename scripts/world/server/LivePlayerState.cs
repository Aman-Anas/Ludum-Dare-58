namespace Game.World.Data;

using LiteNetLib;

// Data to be stored about a player that is live
public record LivePlayerState(NetPeer Peer, string Username, Sector CurrentSector, PlayerData Data);

public static class PlayerStateExt
{
    public static LivePlayerState GetPlayerState(this NetPeer peer)
    {
        return (LivePlayerState)peer.Tag;
    }
}
