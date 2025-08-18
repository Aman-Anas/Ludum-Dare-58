namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;

public interface IPortalHolder : IEntityData
{
    /// <summary>
    /// This should be stored in secret data
    /// </summary>
    public IPortalInfo PortalInfo { get; }
}

public interface IPortalInfo
{
    /// <summary>
    /// Portals associated with this entity
    /// </summary>
    public PortalNode[] PortalNodes { get; }
}

public record PortalNode
{
    /// <summary>
    /// The target entity should be an IPortalHolder
    /// </summary>
    public ulong TargetEntity { get; set; }

    /// <summary>
    /// The id of the target node in the IPortalHolder's array
    /// </summary>
    public byte TargetNodeID { get; set; }

    /// <summary>
    /// This node's "exit" position and rotation offset from the IPortalHolder entity.
    /// This should be set by the portal holder at initialization
    /// </summary>
    public Vector3 ExitPosition { get; set; }
    public Vector3 ExitRotation { get; set; }
}

public static class PortalHolderExt
{
    public static void UsePortal(this IPortalHolder data, byte portalID, ulong userEntityID)
    {
        // Call function to move the entity with ID "userEntityID" to another sector, and copy
        // position + rotation using the PortalNode on the other side
    }
}
