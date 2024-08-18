namespace Game;

using System;
using Game.Networking;
using Game.World;
using Godot;
using LiteNetLib;
using Utilities.Collections;

public enum GameNetState
{
    ClientAndServer,
    ServerOnly,
    ClientOnly
}

public partial class Manager : Node
{
    public static Manager Instance { get; private set; }

    [Export]
    string configPath = "user://config_user.dat";

    const string defaultConfigPath = "user://config_default.dat";

    public GameConfig Config { get; set; }

    public GameNetState NetState { get; private set; }

    public ServerManager GameServer { get; } = new();
    public ClientManager GameClient { get; } = new();

    [Export]
    PackedScene titleScene;

    [Export]
    PackedScene mainClientScene;

    [Export]
    PackedScene serverOnlyScene;

    // // Add a second one for background world maybe
    // public WorldComponents MainWorld { get; set; } = new();

    public Manager()
    {
        // Just so that other scripts can cache a reference.
        // Config and game data won't be loaded until _Ready() is called
        if (Instance == null)
        {
            Instance = this;

            // Initialize MessagePack configuration
            DataUtils.InitMessagePack();

            NetDebug.Logger = new GDNetLogger();
        }
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // At this point all other autoloads are also ready
        // Now we should do actual game stuff (e.g. loading config)

        // Load config vars
        LoadConfig();

        AddChild(GameServer);
        AddChild(GameClient);
    }

    public bool StartGame(
        GameNetState state,
        int port,
        LoginPacket loginInfo = default,
        string address = "localhost"
    )
    {
        switch (state)
        {
            case GameNetState.ClientAndServer:
                bool serverSuccess;
                bool clientSuccess;

                serverSuccess = GameServer.StartServer(port);

                clientSuccess = GameClient.StartClient("localhost", port, loginInfo);

                if (!(serverSuccess && clientSuccess))
                {
                    GameServer.Stop();
                    GameClient.Stop();
                    return false;
                }

                GetTree().ChangeSceneToPacked(mainClientScene);
                break;
            case GameNetState.ClientOnly:
                if (!GameClient.StartClient(address, port, loginInfo))
                {
                    GameClient.Stop();
                    return false;
                }
                GetTree().ChangeSceneToPacked(mainClientScene);
                break;
            case GameNetState.ServerOnly:
                if (!GameServer.StartServer(port))
                {
                    GameServer.Stop();
                    return false;
                }
                GetTree().ChangeSceneToPacked(serverOnlyScene);
                break;
        }
        return true;
    }

    public void ExitToTitle()
    {
        if (GameClient.IsRunning())
        {
            GameClient.Stop();
        }

        if (GameServer.IsRunning())
        {
            GameServer.Stop();
        }

        GetTree().ChangeSceneToPacked(titleScene);
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
}
