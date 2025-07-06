namespace Game;

using Godot;
using MemoryPack;
using NathanHoad;

[MemoryPackable]
public partial class GameConfig
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
    public float MouseSensitivity { get; set; } = 0.005f;

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

    /// <summary>
    /// Applies all the configured settings
    /// </summary>
    public void ApplyConfig()
    {
        DisplayServer.WindowSetMode(WindowMode);
        DisplayServer.WindowSetVsyncMode(VSyncMode);

        // Set the resolution
        if (Resolution != Vector2I.Zero)
        {
            DisplayServer.WindowSetSize(Resolution);

            // Center the window in the screen after changing size
            DisplayServer.WindowSetPosition(
                (DisplayServer.ScreenGetSize() / 2) - (DisplayServer.WindowGetSize() / 2)
            );
        }

        // Set maximum FPS
        Engine.MaxFps = MaxFPS;

        // Set language
        TranslationServer.SetLocale(TranslationLocale);

        // Set Antialiasing setting
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

    /// <summary>
    /// Since changes to inputs are stored elsewhere in the input map,
    /// we need to grab those before saving our settings
    /// </summary>
    [MemoryPackOnSerializing]
    public void OnBeforeSerialize()
    {
        GameInputMap = InputHelper.SerializeInputsForActions();
    }

    [MemoryPackOnDeserialized]
    public void OnAfterDeserialize()
    {
        InputHelper.ResetAllActions();
        InputHelper.DeserializeInputsForActions(GameInputMap);
    }
}
