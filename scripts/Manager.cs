namespace Game;

using Godot;
using Utilities.Collections;

public partial class Manager : Node
{
    public static Manager Instance { get; private set; }

    [Export]
    string configPath = "user://config.dat";

    [Export]
    string savePath = "user://saves/default.dat";

    public GameConfig Config { get; set; }

    public GameData Data { get; set; }

    public Manager()
    {
        // Just so that other scripts can cache a reference.
        // Config and game data won't be loaded until _Ready() is called
        Instance ??= this;

        // Initialize MessagePack configuration
        DataUtils.InitMessagePack();
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Didn't really need to abstract this but whatever
        Config = DataUtils.LoadFromFileOrNull<GameConfig>(configPath);
        Config ??= new();
        Config.UpdateConfig();

        Data = DataUtils.LoadFromFileOrNull<GameData>(savePath);
        Data ??= new();
    }

    public void SaveConfig()
    {
        DataUtils.SaveData(configPath, Config);
    }
}
