using OmniToolbox.Config;

namespace OmniToolbox.UI.Theme;

public static class GlassMotion
{
    private static readonly Dictionary<ulong, State> States = [];
    private static readonly List<ulong> Expired = [];
    public static UIMotionMode Mode { get; internal set; }

    public static unsafe uint CurrentItemID => new ImGuiContextPtr(ImGui.GetCurrentContext()).LastItemData.ID;

    internal static unsafe uint ActiveItemID => new ImGuiContextPtr(ImGui.GetCurrentContext()).ActiveId;

    internal static unsafe bool IsDisabled =>
        (new ImGuiContextPtr(ImGui.GetCurrentContext()).CurrentItemFlags & ImGuiItemFlags.Disabled) != 0;

    public static float Value(uint id, uint channel, float target, float duration, bool spring = false)
    {
        if (!OmniTheme.UsesMaterial || Mode == UIMotionMode.Off)
        {
            return target;
        }

        var key = ((ulong)id << 32) | channel;
        var frame = ImGui.GetFrameCount();
        if (!States.TryGetValue(key, out var state) || frame - state.Frame > 1)
        {
            state = new State { Value = target, Frame = frame };
            States[key] = state;
            return target;
        }
        if (state.Frame == frame)
        {
            return state.Value;
        }
        if (MathF.Abs(state.Value - target) < 0.0001f && MathF.Abs(state.Velocity) < 0.0001f)
        {
            state.Value = target;
            state.Velocity = 0f;
        }
        else
        {
            var dt = Math.Clamp(ImGui.GetIO().DeltaTime, 0f, 0.25f);
            var speed = 4.6f / MathF.Max(0.01f, duration);
            if (spring && Mode == UIMotionMode.Full)
            {
                // 解析临界阻尼响应，低帧率下也不会数值发散。
                var displacement = state.Value - target;
                var impulse = state.Velocity + speed * displacement;
                var decay = MathF.Exp(-speed * dt);
                state.Value = target + (displacement + impulse * dt) * decay;
                state.Velocity = (state.Velocity - speed * impulse * dt) * decay;
            }
            else
            {
                state.Value += (target - state.Value) * (1f - MathF.Exp(-speed * dt));
                state.Velocity = 0f;
            }
        }

        state.Frame = frame;
        States[key] = state;
        return state.Value;
    }

    public static Vector4 ButtonFill(Vector4 normal)
        => ControlFill(CurrentItemID, normal,
            ImGui.IsItemHovered() || ImGui.IsItemFocused(), ImGui.IsItemActive());

    internal static Vector4 ControlFill(uint id, Vector4 normal, bool hovered, bool active)
    {
        hovered &= !IsDisabled;
        active &= !IsDisabled;
        var hover = Value(id, 0, hovered ? 1f : 0f, 0.14f);
        var pressed = Value(id, 1, active ? 1f : 0f, active ? 0.08f : 0.18f);
        var selected = Value(id, 3, normal == OmniTheme.Tokens.Accent ? 1f : 0f, 0.16f);
        var fill = Vector4.Lerp(normal == OmniTheme.Tokens.Accent ? OmniTheme.Tokens.Surface : normal,
            OmniTheme.Tokens.Accent, selected);
        return Vector4.Lerp(
            Vector4.Lerp(fill, OmniTheme.HoverBackground, hover * (1f - selected * 0.45f)),
            OmniTheme.ActiveBackground, pressed);
    }

    internal static float ButtonOffset() => Mode == UIMotionMode.Full
        ? OmniTheme.Scale(0.6f) * Value(CurrentItemID, 4, ImGui.IsItemActive() ? 1f : 0f,
            ImGui.IsItemActive() ? 0.08f : 0.18f, true)
        : 0f;

    internal static Vector2 Pointer(uint id, Vector2 target, bool tracking)
    {
        if (!tracking)
        {
            if (States.TryGetValue(((ulong)id << 32) | 12, out var x))
            {
                target.X = x.Value;
            }
            if (States.TryGetValue(((ulong)id << 32) | 13, out var y))
            {
                target.Y = y.Value;
            }
        }
        return new Vector2(Value(id, 12, target.X, 0.10f), Value(id, 13, target.Y, 0.10f));
    }

    internal static void Prune()
    {
        var frame = ImGui.GetFrameCount();
        if (!OmniTheme.UsesMaterial || Mode == UIMotionMode.Off)
        {
            Clear();
            return;
        }
        if (frame % 120 != 0)
        {
            return;
        }
        foreach (var (key, state) in States)
        {
            if (frame - state.Frame > 120)
            {
                Expired.Add(key);
            }
        }
        foreach (var key in Expired)
        {
            States.Remove(key);
        }
        Expired.Clear();
    }

    internal static void Clear()
    {
        States.Clear();
        Expired.Clear();
    }

    private struct State
    {
        public float Value;
        public float Velocity;
        public int Frame;
    }
}
