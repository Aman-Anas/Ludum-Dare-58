namespace Game;

using System;
using System.Threading.Tasks;
using Game.Networking;
using Game.Terrain;
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

    public ServerManager GameServer { get; private set; }
    public ClientManager GameClient { get; private set; }
    public ChunkManager ChunkManager { get; set; }

    [Export]
    PackedScene titleScene;

    [Export]
    PackedScene mainClientScene;

    [Export]
    PackedScene serverOnlyScene;

    [Export]
    PackedScene serverScene;

    [Export]
    PackedScene clientScene;

    public Manager()
    {
        // Just so that other scripts can cache a reference.
        // Config and game data won't be loaded until _Ready() is called
        if (Instance == null)
        {
            Instance = this;

            // Initialize MessagePack configuration
            // DataUtils.InitMessagePack();

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
        GameServer = serverScene.Instantiate<ServerManager>();
        GameClient = clientScene.Instantiate<ClientManager>();
        AddChild(GameServer);
        AddChild(GameClient);
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            GD.Print("oof");
            QuitGame();
        }
    }

    public async Task<bool> StartGame(
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

                // TODO: Make StartClient (and StartGame I guess) return a Task so we can
                // wait for connection asynchronously (and probably put up a connection loading
                // screen)
                var connect = GameClient.StartClient("localhost", port, loginInfo);
                await connect;
                clientSuccess = connect.Result;

                GD.Print("server success", serverSuccess);
                GD.Print("client success", clientSuccess);

                if (!(serverSuccess && clientSuccess))
                {
                    GameServer.Stop();
                    GameClient.Stop();
                    return false;
                }

                GetTree().ChangeSceneToPacked(mainClientScene);
                break;
            case GameNetState.ClientOnly:
                var clientOnly = GameClient.StartClient(address, port, loginInfo);
                await clientOnly;
                if (!clientOnly.Result)
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
        GetTree().Paused = false;
        GetTree().ChangeSceneToPacked(titleScene);
    }

    public void QuitGame()
    {
        if (GameClient.IsRunning())
        {
            GameClient.Stop();
        }

        if (GameServer.IsRunning())
        {
            GameServer.Stop();
        }
        GetTree().Quit();
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
