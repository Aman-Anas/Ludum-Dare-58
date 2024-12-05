namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

public interface IBasicAnim : IEntityData
{
    public byte CurrentAnim { get; set; }

    public virtual void UpdateAnim()
    {
        this.SendMessage(new BasicAnimUpdate(EntityID, (byte)CurrentAnim));
    }
}
