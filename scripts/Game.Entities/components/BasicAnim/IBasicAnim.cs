namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

public interface IBasicAnim : IEntityData
{
    public byte CurrentAnim { get; set; }

    public BasicAnimComponent BasicAnimHelper { get; }
}

public class BasicAnimComponent(IBasicAnim data)
{
    public void UpdateAnim()
    {
        data.SendMessage(new BasicAnimUpdate(data.EntityID, data.CurrentAnim));
    }
}
