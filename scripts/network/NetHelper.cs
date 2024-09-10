namespace Game.Networking;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Game.World.Data;
using Godot;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;

// ;

public static class NetHelper
{
    // Basic utility Encode/Decode calls. For better performance you might need to use
    // more specific serialize/deserialize methods

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T DecodeData<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        NetDataReader reader
    )
    {
        return MemoryPackSerializer.Deserialize<T>(
            reader.RawData.AsSpan(reader.Position, reader.AvailableBytes)
        );
    }

    public static byte[] EncodeData<T>(T data)
    {
        return MemoryPackSerializer.Serialize<T>(data);
    }

    public static T InstanceFromScene<T>(string path)
        where T : Node3D
    {
        return GD.Load<PackedScene>(path).Instantiate<T>();
    }
}

public class GDNetLogger : INetLogger
{
    public void WriteNet(NetLogLevel level, string str, params object[] args)
    {
        switch (level)
        {
            case NetLogLevel.Info:
            case NetLogLevel.Trace:
                GD.Print(str);
                GD.Print(args);
                break;
            case NetLogLevel.Warning:
                GD.PushWarning(str);
                GD.PushWarning(args);
                break;
            case NetLogLevel.Error:
                GD.PushError(str);
                GD.PushError(args);
                break;
        }
    }
}
