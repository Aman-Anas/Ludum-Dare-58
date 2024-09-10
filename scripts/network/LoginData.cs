namespace Game.Networking;

using MemoryPack;

[MemoryPackable]
public readonly partial record struct LoginPacket(string Username, string Password);
