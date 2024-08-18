using System;
using Game;
using Game.Setup;
using Godot;

public partial class NetSetupScreen : Control
{
    // Back to title
    [Export]
    Button backToTitle;

    [Export(PropertyHint.File)]
    string titleScene;

    // Join/connect controls
    [Export]
    LineEdit joinIP;

    [Export]
    SpinBox joinPort;

    [Export]
    Button joinButton;

    // Host server controls
    [Export]
    Button makeNewWorld;

    [Export]
    SpinBox hostPort;

    [Export]
    Button hostPlayButton;

    [Export]
    Button hostOnlyButton;

    [Export]
    Button refreshList;

    // login boxes
    [Export]
    LineEdit usernameBox;

    [Export]
    LineEdit pwordBox;

    // Lists
    [Export]
    ItemList worldList;

    [Export]
    ItemList serverList;

    // Error modals
    [Export]
    AcceptDialog errorWindow;

    // Create world name
    [Export]
    ConfirmationDialog createWorldWindow;

    [Export]
    LineEdit enterWorldName;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        backToTitle.Pressed += () =>
            GetTree().ChangeSceneToPacked(GD.Load<PackedScene>(titleScene));

        joinButton.Pressed += JoinGame;
        hostPlayButton.Pressed += HostAndJoinGame;
        hostOnlyButton.Pressed += HostOnlyGame;

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
                errorWindow.DialogText = e.Message;
                errorWindow.Show();
            }
        };

        refreshList.Pressed += RefreshWorldList;
        RefreshWorldList();
    }

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
            errorWindow.DialogText = "Fill in username or password!";
            errorWindow.Show();
            return false;
        }
        return true;
    }

    bool CheckIfWorldSelected()
    {
        if (!worldList.IsAnythingSelected())
        {
            errorWindow.DialogText = "Select a world my dude";
            errorWindow.Show();
            return false;
        }
        return true;
    }

    void JoinGame()
    {
        if (!CheckIfUserPassword())
        {
            return;
        }
        var success = Manager.Instance.StartGame(
            GameNetState.ClientOnly,
            Mathf.RoundToInt(joinPort.Value),
            new(usernameBox.Text, pwordBox.Text),
            joinIP.Text
        );

        if (!success)
        {
            errorWindow.DialogText = "Failed to join game!";
            errorWindow.Show();
        }
    }

    void HostAndJoinGame()
    {
        if (!(CheckIfWorldSelected() && CheckIfUserPassword()))
        {
            return;
        }
        var success = Manager.Instance.StartGame(
            GameNetState.ClientAndServer,
            Mathf.RoundToInt(hostPort.Value),
            new(usernameBox.Text, pwordBox.Text)
        );

        if (!success)
        {
            errorWindow.DialogText = "Failed to start and/or join server!";
            errorWindow.Show();
        }
    }

    void HostOnlyGame()
    {
        if (!CheckIfWorldSelected())
        {
            return;
        }
        var success = Manager.Instance.StartGame(
            GameNetState.ServerOnly,
            Mathf.RoundToInt(hostPort.Value)
        );

        if (!success)
        {
            errorWindow.DialogText = "Failed to start server!";
            errorWindow.Show();
        }
    }
}
