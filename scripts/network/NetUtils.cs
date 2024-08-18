namespace Game.Networking;

using System;
using Game.World.Data;
using Godot;
using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;

public static class NetUtils
{
    // Basic utility Encode/Decode calls. For better performance use
    // a more specific serialize method.

    public static T DecodeData<T>(NetDataReader reader)
    {
        return MessagePackSerializer.Deserialize<T>(
            reader.RawData.AsMemory(reader.Position, reader.AvailableBytes)
        );
    }

    public static byte[] EncodeData<T>(T data)
    {
        return MessagePackSerializer.Serialize<T>(data);
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
