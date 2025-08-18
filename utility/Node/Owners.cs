namespace Utilities;

using Godot;

public static class UpdateOwners
{
    public static void UpdateOwnerRecursive(Node current, Node newOwner)
    {
        if (current != newOwner)
        {
            current.Owner = newOwner;
        }
        foreach (var child in current.GetChildren())
        {
            UpdateOwnerRecursive(child, newOwner);
        }
    }
}
