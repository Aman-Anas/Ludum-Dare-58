using System;
using Game;
using Game.Setup;
using Godot;

public partial class NetSetupScreen : Control
{
    // Back to title
    [Export]
    Button backToTitle = null!;

    [Export(PropertyHint.File)]
    string titleScene = null!;

    // Join/connect controls
    [Export]
    LineEdit joinIP = null!;

    [Export]
    SpinBox joinPort = null!;

    [Export]
    Button joinButton = null!;

    // Host server controls
    [Export]
    Button makeNewWorld = null!;

    [Export]
    SpinBox hostPort = null!;

    [Export]
    Button hostPlayButton = null!;

    [Export]
    Button hostOnlyButton = null!;

    [Export]
    Button refreshList = null!;

    // login boxes
    [Export]
    LineEdit usernameBox = null!;

    [Export]
    LineEdit pwordBox = null!;

    // Lists
    [Export]
    ItemList worldList = null!;

    [Export]
    ItemList serverList = null!;

    // Error modals
    [Export]
    AcceptDialog errorWindow = null!;

    // Create world name
    [Export]
    ConfirmationDialog createWorldWindow = null!;

    [Export]
    LineEdit enterWorldName = null!;

    [Export]
    Button debugPlay = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        backToTitle.Pressed += () =>
            GetTree().ChangeSceneToPacked(GD.Load<PackedScene>(titleScene));

        joinButton.Pressed += JoinGame;
        hostPlayButton.Pressed += HostAndJoinGame;
        hostOnlyButton.Pressed += HostOnlyGame;

#if DEBUG
        debugPlay.Pressed += QuickStart;
#endif

        worldList.ItemSelected += (long index) =>
            Manager.Instance.GameServer.CurrentSaveName = worldList.GetItemText((int)index);

        makeNewWorld.Pressed += createWorldWindow.Show;
        createWorldWindow.Confirmed += () =>
        {
            try
            {
                WorldSaves.MakeNewWorld(enterWorldName.Text);
                RefreshWorldList();
            }
            catch (ArgumentException e)
            {
                ShowErrorBox(e.Message);
            }
        };

        refreshList.Pressed += RefreshWorldList;
        RefreshWorldList();
    }

    void ShowErrorBox(string msg)
    {
        errorWindow.DialogText = msg;
        errorWindow.Show();
    }

#if DEBUG
    // Debug method to quickly host and play a test world
    async void QuickStart()
    {
        const string debugWorldName = "Debug_world_potato";
        WorldSaves.DeleteWorld(debugWorldName);
        WorldSaves.MakeNewWorld(debugWorldName);
        Manager.Instance.GameServer.CurrentSaveName = debugWorldName;

        var success = await Manager.Instance.StartGame(
            GameNetState.ClientAndServer,
            8303,
            new("igalactic", "potato123456")
        );

        if (!success)
        {
            ShowErrorBox("Failed to join game!");
        }
    }
#endif

    void RefreshWorldList()
    {
        var worlds = WorldSaves.GetWorldList();
        worldList.Clear();
        foreach (var world in worlds)
        {
            worldList.AddItem(world.Item2.Nickname, null, true);
        }
    }

    bool CheckIfUserPassword()
    {
        if (usernameBox.Text.Length < 3 || pwordBox.Text.Length < 8)
        {
            ShowErrorBox("Fill in username or password!");
            return false;
        }
        return true;
    }

    bool CheckIfWorldSelected()
    {
        if (!worldList.IsAnythingSelected())
        {
            ShowErrorBox("Select a world my dude");
            return false;
        }
        return true;
    }

    async void JoinGame()
    {
        if (!CheckIfUserPassword())
        {
            return;
        }
        var success = await Manager.Instance.StartGame(
            GameNetState.ClientOnly,
            Mathf.RoundToInt(joinPort.Value),
            new(usernameBox.Text, pwordBox.Text),
            joinIP.Text
        );

        if (!success)
        {
            ShowErrorBox("Failed to join game!");
        }
    }

    async void HostAndJoinGame()
    {
        if (!(CheckIfWorldSelected() && CheckIfUserPassword()))
        {
            return;
        }
        var success = await Manager.Instance.StartGame(
            GameNetState.ClientAndServer,
            Mathf.RoundToInt(hostPort.Value),
            new(usernameBox.Text, pwordBox.Text)
        );

        if (!success)
        {
            ShowErrorBox("Failed to start and/or join server!");
        }
    }

    async void HostOnlyGame()
    {
        if (!CheckIfWorldSelected())
        {
            return;
        }
        var success = await Manager.Instance.StartGame(
            GameNetState.ServerOnly,
            Mathf.RoundToInt(hostPort.Value)
        );

        if (!success)
        {
            ShowErrorBox("Failed to start server!");
        }
    }
}
