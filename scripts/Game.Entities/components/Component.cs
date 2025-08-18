using System;
using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

namespace Game.Entities;

public interface IComponent<in T>
    where T : IEntityData
{
    public void Initialize(T data, int index);
    public void Initialize(T data);
}

public abstract class Component<T> : IComponent<T>
    where T : IEntityData
{
    // Keep a reference to the data this component is contained in.
    // Why? So we can use its server/client link, and in case we need
    // to interact with other components
    [MemoryPackIgnore]
    protected T data = default!;

    // If this is contained in an array of components, hang on to the index
    // Can pass it to network messages so they know which index we are targeting
    protected int index;

    public void Initialize(T data, int index)
    {
        this.data = data;
        this.index = index;
    }

    /// <summary>
    /// Some components are singletons on their data structure
    /// </summary>
    public void Initialize(T data) => Initialize(data, 0);
}

public static class ComponentExtensions
{
    public static void InitializeAll<TComponent, TData>(this TComponent[] componentList, TData data)
        where TComponent : IComponent<TData>
        where TData : IEntityData
    {
        for (int x = 0; x < componentList.Length; x++)
        {
            componentList[x].Initialize(data, x);
        }
    }
}
