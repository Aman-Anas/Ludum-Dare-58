namespace Game;

using Godot;
using MessagePack;
using NathanHoad;

[MessagePackObject(keyAsPropertyName: true)]
public class GameConfig : IMessagePackSerializationCallbackReceiver
{
    public string GameInputMap { get; set; }
    public Vector2I Resolution { get; set; }
    public DisplayServer.WindowMode WindowMode { get; set; }
    public DisplayServer.VSyncMode VSyncMode { get; set; }
    public RenderingServer.ViewportMsaa AntiAliasing { get; set; }
    public int MaxFPS { get; set; }

    // Idk if I'll ever use translations lol
    public string TranslationLocale { get; set; }

    // Game-specific config
    public float MOUSE_SENSITIVITY { get; set; } = 0.01f;

    public GameConfig()
    {
        WindowMode = DisplayServer.WindowGetMode();
        VSyncMode = DisplayServer.WindowGetVsyncMode();
        Resolution = DisplayServer.WindowGetSize();
        MaxFPS = Engine.MaxFps;
        TranslationLocale = TranslationServer.GetLocale();
        AntiAliasing = 0;
        GameInputMap = InputHelper.SerializeInputsForActions();
    }

    public void UpdateConfig()
    {
        DisplayServer.WindowSetMode(WindowMode);
        DisplayServer.WindowSetVsyncMode(VSyncMode);

        // Set resolution
        if (Resolution != Vector2I.Zero)
        {
            DisplayServer.WindowSetSize(Resolution);
            // Center the window after changing size
            DisplayServer.WindowSetPosition(
                (DisplayServer.ScreenGetSize() / 2) - (DisplayServer.WindowGetSize() / 2)
            );
        }

        // Set maximum FPS
        Engine.MaxFps = MaxFPS;
        TranslationServer.SetLocale(TranslationLocale);

        // Set Antialiasing
        // Set both 2D and 3D settings to the same value
        RenderingServer.ViewportSetMsaa2D(
            Manager.Instance.GetTree().Root.GetViewportRid(),
            AntiAliasing
        );
        RenderingServer.ViewportSetMsaa3D(
            Manager.Instance.GetTree().Root.GetViewportRid(),
            AntiAliasing
        );
    }

    public void OnBeforeSerialize()
    {
        GameInputMap = InputHelper.SerializeInputsForActions();
    }

    public void OnAfterDeserialize()
    {
        if (GameInputMap != null)
        {
            InputHelper.ResetAllActions();
            InputHelper.DeserializeInputsForActions(GameInputMap);
        }
    }
}
