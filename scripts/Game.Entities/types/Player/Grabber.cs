namespace Game.Entities;

using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using Godot;

// public class Grabber(ControllablePlayer player, Node3D targetPos)
// {
//     readonly Dictionary<ulong, INetEntity> itemInstances = [];

//     public void UpdateInstances()
//     {
//         var currentInventory = player.Data.Inventory;
//         var idsToKeep = new HashSet<ulong>();
//         foreach (InventoryEntry item in currentInventory.Values)
//         {
//             var id = item.Storable.EntityID;
//             idsToKeep.Add(id);
//             // Check if this item was already cached,
//             // if not store an instance
//             if (!itemInstances.ContainsKey(id))
//             {
//                 var newInstance = item.Storable.SpawnInstance(false);
//                 itemInstances[id] = newInstance;
//                 var node = newInstance.GetNode();
//                 node.ProcessMode = Node.ProcessModeEnum.Disabled;
//                 node.Visible = false;
//                 targetPos.AddChild(node);
//             }
//         }

//         // Remove entities from the cache if they're not in the new inventory
//         foreach (INetEntity entity in itemInstances.Values)
//         {
//             if (!idsToKeep.Contains(entity.Data.EntityID))
//             {
//                 entity.GetNode().QueueFree();
//             }
//         }
//     }

//     public void Update(float delta)
//     {
//         var data = player.Data;
//     }
// }
