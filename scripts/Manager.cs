namespace Game;

using Godot;
using NathanHoad;
using Utilities.Collections;

public partial class Manager : Node
{
    public static Manager Instance { get; private set; }

    [Export]
    string configPath = "user://config_user.dat";

    string defaultConfigPath = "user://config_default.dat";

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
        GD.Print(InputHelper.GuessDeviceName());

        // Load config vars
        LoadConfig();

        // Load our game save
        LoadGame();
    }

    public void LoadConfig(bool defaultFile = false)
    {
        if (!defaultFile)
        {
            Config = DataUtils.LoadFromFileOrNull<GameConfig>(configPath);
        }

        // Fall back to default config
        if (Config == null || defaultFile)
        {
            Config = DataUtils.LoadFromFileOrNull<GameConfig>(defaultConfigPath);
        }

        // Use current settings if no default either
        Config ??= new();

        Config.UpdateConfig();

        // If there's no default config yet (e.g. first game start)
        if (!FileAccess.FileExists(defaultConfigPath))
        {
            DataUtils.SaveData(defaultConfigPath, Config);
        }
    }

    public void SaveConfig()
    {
        DataUtils.SaveData(configPath, Config);
    }

    public void LoadGame(string filename = null)
    {
        if (filename != null)
        {
            savePath = filename;
        }
        Data = DataUtils.LoadFromFileOrNull<GameData>(savePath);
        Data ??= new();
    }

    public void SaveGame(string filename = null)
    {
        if (filename != null)
        {
            savePath = filename;
        }
        DataUtils.SaveData(savePath, Data);
    }
}
