namespace Utilities;

using Godot;

public static class DirAccessExtensions
{
    /// <summary>
    /// Thanks to https://github.com/Elip100/godot_remove_directory/blob/main/addons/remove_directory/rmdir.gd
    /// </summary>
    public static void RemoveDir(string directory)
    {
        foreach (string file in DirAccess.GetFilesAt(directory))
        {
            DirAccess.RemoveAbsolute(directory.PathJoin(file));
        }
        foreach (string dir in DirAccess.GetDirectoriesAt(directory))
        {
            RemoveDir(directory.PathJoin(dir));
        }
        DirAccess.RemoveAbsolute(directory);
    }
}
