namespace Game;

using Godot;
using MessagePack;
using NathanHoad;

[MessagePackObject(keyAsPropertyName: true)]
public class GameConfig : IMessagePackSerializationCallbackReceiver
{
    public string GameInputMap { get; private set; }
    public Vector2I Resolution { get; set; }
    public DisplayServer.WindowMode WindowMode { get; set; }
    public DisplayServer.VSyncMode VSyncMode { get; set; }
    public RenderingServer.ViewportMsaa AntiAliasing { get; set; }
    public int MaxFPS { get; set; }

    // Idk if I'll ever use translations lol
    public string TranslationLocale { get; set; }

    public GameConfig()
    {
        WindowMode = DisplayServer.WindowGetMode();
        VSyncMode = DisplayServer.WindowGetVsyncMode();
        Resolution = DisplayServer.WindowGetSize();
        MaxFPS = Engine.MaxFps;
        TranslationLocale = TranslationServer.GetLocale();
        AntiAliasing = 0;
        GameInputMap = (string)InputHelper.Instance.Call("serialize_inputs_for_actions");
    }

    public void UpdateConfig()
    {
        // Set resolution
        if (Resolution != Vector2I.Zero)
        {
            DisplayServer.WindowSetSize(Resolution);
            // Center the window after changing size
            DisplayServer.WindowSetPosition(
                (DisplayServer.ScreenGetSize() / 2) - (DisplayServer.WindowGetSize() / 2)
            );
        }

        DisplayServer.WindowSetMode(WindowMode);
        DisplayServer.WindowSetVsyncMode(VSyncMode);
        // Set maximum FPS
        Engine.MaxFps = MaxFPS;
        TranslationServer.SetLocale(TranslationLocale);

        // Set Antialiasing
        // Set both 2D and 3D settings to the same value
        ProjectSettings.SetSetting(
            name: "rendering/anti_aliasing/quality/msaa_2d",
            value: (int)AntiAliasing
        );

        ProjectSettings.SetSetting(
            name: "rendering/anti_aliasing/quality/msaa_3d",
            value: (int)AntiAliasing
        );
    }

    public void OnBeforeSerialize()
    {
        GameInputMap = (string)InputHelper.Instance.Call("serialize_inputs_for_actions");
    }

    public void OnAfterDeserialize()
    {
        if (GameInputMap != null)
        {
            InputHelper.DeserializeInputsForActions(GameInputMap);
        }
    }
}
