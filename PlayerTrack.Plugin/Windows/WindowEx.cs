using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using PlayerTrack.Models;

namespace PlayerTrack.Windows;

/// <inheritdoc />
public abstract class WindowEx : Window
{
    /// <summary>
    /// Plugin Configuration.
    /// </summary>
    protected readonly IPluginConfig Config;

    /// <summary>
    /// Default window flags.
    /// </summary>
    protected readonly ImGuiWindowFlags DefaultFlags;

    private const float IndentSpacing = 21f;
    private const float ChildBorderSize = 1;
    private readonly Vector2 CellPadding = new(4, 2);
    private readonly Vector2 FramePadding = new(4, 3);
    private readonly Vector2 ItemInnerSpacing = new(4, 4);
    private readonly Vector2 ItemSpacing = new(8, 4);
    private readonly Vector2 WindowPadding = new(8, 8);

    private ImRaii.StyleDisposable? Style = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowEx" /> class.
    /// </summary>
    /// <param name="name">window name.</param>
    /// <param name="config">plugin configuration.</param>
    /// <param name="flags">window flags.</param>
    // ReSharper disable once UnusedParameter.Local
    protected WindowEx(string name, IPluginConfig config, ImGuiWindowFlags flags = ImGuiWindowFlags.None) : base(name, flags)
    {
        Config = config;
        DefaultFlags = flags;
        RespectCloseHotkey = false;
    }

    /// <summary>
    /// Function to invoke on pre-enable.
    /// </summary>
    public abstract void Initialize();

    /// <summary>
    /// Additional conditions for the window to be drawn, regardless of its open-state.
    /// Checks if logged in and logged in state toggle.
    /// </summary>
    /// <returns>
    /// True if the window should be drawn, false otherwise.
    /// </returns>
    public override bool DrawConditions()
    {
        if (Plugin.ClientStateHandler.IsLoggedIn)
            return true;

        if (!Plugin.ClientStateHandler.IsLoggedIn && !Config.OnlyShowWindowWhenLoggedIn)
            return true;

        return false;
    }

    /// <summary>
    /// Code to be executed before conditionals are applied and the window is drawn.
    /// Enforces default padding and spacing styles.
    /// </summary>
    public override void PreDraw()
    {
        Style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, WindowPadding)
                      .Push(ImGuiStyleVar.FramePadding, FramePadding)
                      .Push(ImGuiStyleVar.CellPadding, CellPadding)
                      .Push(ImGuiStyleVar.ItemSpacing, ItemSpacing)
                      .Push(ImGuiStyleVar.ItemInnerSpacing, ItemInnerSpacing)
                      .Push(ImGuiStyleVar.IndentSpacing, IndentSpacing)
                      .Push(ImGuiStyleVar.ChildBorderSize, ChildBorderSize);
    }

    /// <summary>
    /// Code to be executed after the window is drawn.
    /// Enforces default padding and spacing styles.
    /// </summary>
    public override void PostDraw()
    {
        Style.Dispose();
    }

    /// <summary>
    /// Updates dynamic window flags.
    /// </summary>
    protected void SetWindowFlags()
    {
        var flags = DefaultFlags;
        if (Config.IsWindowSizeLocked)
            flags |= ImGuiWindowFlags.NoResize;

        if (Config.IsWindowPositionLocked)
            flags |= ImGuiWindowFlags.NoMove;

        Flags = flags;
    }
}
