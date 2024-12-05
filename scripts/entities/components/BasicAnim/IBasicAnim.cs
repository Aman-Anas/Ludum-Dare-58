namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

public interface IBasicAnim : IEntityData
{
    public byte CurrentAnim { get; set; }
}

public static class BasicAnimExt
{
    public static void UpdateAnim(this IBasicAnim data)
    {
        data.SendMessage(new BasicAnimUpdate(data.EntityID, data.CurrentAnim));
    }
}
