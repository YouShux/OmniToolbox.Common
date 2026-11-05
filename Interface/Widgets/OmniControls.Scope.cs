namespace OmniToolbox.UI.Controls;

public static partial class OmniControls
{
    internal struct ControlScope : IDisposable
    {
        private int colors;
        private int styles;
        private bool hasID;

        public static ControlScope PushColor(ImGuiCol target, Vector4 color) =>
            new ControlScope().Push(target, color);

        public static ControlScope PushStyle(ImGuiStyleVar target, float value) =>
            new ControlScope().Push(target, value);

        public static ControlScope PushStyle(ImGuiStyleVar target, Vector2 value) =>
            new ControlScope().Push(target, value);

        public static ControlScope PushID(string id)
        {
            ImGui.PushID(id);
            return new() { hasID = true };
        }

        public static ControlScope PushID(int id)
        {
            ImGui.PushID(id);
            return new() { hasID = true };
        }

        public ControlScope Push(ImGuiCol target, Vector4 color)
        {
            ImGui.PushStyleColor(target, color);
            colors++;
            return this;
        }

        public ControlScope Push(ImGuiStyleVar target, float value)
        {
            ImGui.PushStyleVar(target, value);
            styles++;
            return this;
        }

        public ControlScope Push(ImGuiStyleVar target, Vector2 value)
        {
            ImGui.PushStyleVar(target, value);
            styles++;
            return this;
        }

        public void Dispose()
        {
            if (styles > 0)
            {
                ImGui.PopStyleVar(styles);
                styles = 0;
            }
            if (colors > 0)
            {
                ImGui.PopStyleColor(colors);
                colors = 0;
            }
            if (hasID)
            {
                ImGui.PopID();
                hasID = false;
            }
        }
    }
}
