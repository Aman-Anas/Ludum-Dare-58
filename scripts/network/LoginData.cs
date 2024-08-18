namespace Game.Networking;

using MessagePack;

[MessagePackObject]
public readonly record struct LoginPacket(
    [property: Key(0)] string Username,
    [property: Key(1)] string Password
);
