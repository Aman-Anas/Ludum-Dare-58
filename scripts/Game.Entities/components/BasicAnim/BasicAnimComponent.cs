namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

// public interface IBasicAnim : IEntityData
// {
//     public byte CurrentAnim { get; set; }

//     public BasicAnimComponent BasicAnimHelper { get; }
// }

public partial class BasicAnimComponent : NetComponent<EntityData>
{
    public byte CurrentAnimation { get; set; }

    // public event Action? AnimationChanged;

    // public void UpdateAnim()
    // {
    //     this.NetUpdate();
    //     // data.SendMessage(new BasicAnimUpdate(data.EntityID, data.CurrentAnim));
    // }
}
