namespace OmniToolbox.UI.Widgets;

internal readonly unsafe struct OmniListInteraction
{
    private static readonly Dictionary<uint, ListState> States = [];
    private static readonly List<uint> Expired = [];
    private static int PrunedFrame = -1;

    private readonly ListState state;

    internal bool RetainAll { get; }

    internal bool HasRetainedRows => state.ActiveID != 0 || state.NavID != 0 ||
        state.PopupID != 0 || state.Released;

    internal OmniListInteraction(string id)
    {
        Prune();
        var listID = ImGui.GetID(id);
        if (!States.TryGetValue(listID, out var cached))
        {
            cached = new();
            if (States.Count < 512)
                States.Add(listID, cached);
        }
        state = cached;
        var frame = ImGui.GetFrameCount();
        if (state.Frame < frame - 1)
            state.Reset();
        var context = new ImGuiContextPtr(ImGui.GetCurrentContext());
        var window = ImGuiP.GetCurrentWindowRead();
        state.Released = state.ActiveID != 0 && state.ActiveID != context.ActiveId &&
            state.ActiveID == context.ActiveIdPreviousFrame;
        if (state.Released)
            state.ReleasedRow = state.ActiveRow;
        if (state.ActiveID != context.ActiveId)
            state.ActiveID = 0;
        if (state.NavID != context.NavId)
            state.NavID = 0;
        var popupOwned = false;
        var popupKnown = false;
        for (var index = context.BeginPopupStack.Size; index < context.OpenPopupStack.Size; index++)
        {
            var popup = context.OpenPopupStack.Data[index];
            if (!BelongsTo(popup.SourceWindow, window.ID))
                continue;
            popupOwned = true;
            popupKnown |= popup.PopupId == state.PopupID;
        }
        if (!popupKnown)
            state.PopupID = 0;
        var moving = context.ActiveId != 0 && context.ActiveIdWindow.Handle != null &&
            context.ActiveId == context.ActiveIdWindow.MoveId;
        var activeOwned = !moving && context.ActiveId != 0 && BelongsTo(context.ActiveIdWindow, window.ID);
        if (activeOwned && state.ActiveID == 0 && popupKnown)
        {
            state.ActiveID = context.ActiveId;
            state.ActiveRow = state.PopupRow;
        }
        var navOwned = BelongsTo(context.NavWindow, window.ID);
        var navigating = navOwned && (context.NavAnyRequest || context.NavInitRequest || context.NavMoveScoringItems ||
            context.NavId != 0 && context.NavId != state.ObservedNavID && state.NavID == 0);
        state.ObservedNavID = context.NavId;
        RetainAll = activeOwned && state.ActiveID == 0 || popupOwned && !popupKnown || navigating;
        state.Frame = frame;
    }

    internal bool ShouldRetain(uint rowID) => RetainAll ||
        state.ActiveID != 0 && state.ActiveRow == rowID ||
        state.NavID != 0 && state.NavRow == rowID ||
        state.PopupID != 0 && state.PopupRow == rowID ||
        state.Released && state.ReleasedRow == rowID;

    internal bool ShouldRetain(int rowID) => ShouldRetain(unchecked((uint)rowID));

    internal RowScope BeginRow(uint rowID) => new(state, rowID);

    internal RowScope BeginRow(int rowID) => BeginRow(unchecked((uint)rowID));

    internal void IncludeRetainedRow(ImGuiListClipperPtr clipper, int index, uint rowID)
    {
        if (ShouldRetain(rowID))
            clipper.ForceDisplayRangeByIndices(index, index + 1);
    }

    internal static void Clear() => States.Clear();

    internal static void Prune()
    {
        var frame = ImGui.GetFrameCount();
        if (PrunedFrame == frame)
            return;
        PrunedFrame = frame;
        foreach (var (id, value) in States)
            if (value.Frame < frame - 1)
                Expired.Add(id);
        foreach (var id in Expired)
            States.Remove(id);
        Expired.Clear();
    }

    private static bool BelongsTo(ImGuiWindowPtr window, uint owner)
    {
        var context = new ImGuiContextPtr(ImGui.GetCurrentContext());
        for (var depth = context.OpenPopupStack.Size; depth >= 0 && window.Handle != null; depth--)
        {
            var current = window;
            while (current.Handle != null)
            {
                if (current.ID == owner)
                    return true;
                if ((current.Flags & ImGuiWindowFlags.ChildWindow) == 0)
                    break;
                current = current.ParentWindow;
            }
            var source = default(ImGuiWindowPtr);
            for (var index = context.OpenPopupStack.Size - 1; index >= 0; index--)
            {
                var popup = context.OpenPopupStack.Data[index];
                if (popup.Window != null && current.Handle == popup.Window)
                {
                    source = popup.SourceWindow;
                    break;
                }
            }
            window = source;
        }
        return false;
    }

    internal readonly struct RowScope : IDisposable
    {
        private readonly ListState state;
        private readonly uint rowID;
        private readonly uint activeAlive;
        private readonly uint navID;
        private readonly bool navAlive;
        private readonly int popupCount;
        private readonly uint popupID;

        internal RowScope(ListState state, uint rowID)
        {
            this.state = state;
            this.rowID = rowID;
            var context = new ImGuiContextPtr(ImGui.GetCurrentContext());
            activeAlive = context.ActiveIdIsAlive;
            navID = context.NavId;
            navAlive = context.NavIdIsAlive;
            popupCount = context.OpenPopupStack.Size;
            popupID = popupCount > 0 ? context.OpenPopupStack.Data[popupCount - 1].PopupId : 0;
        }

        public void Dispose()
        {
            var context = new ImGuiContextPtr(ImGui.GetCurrentContext());
            if (context.ActiveId != 0 && activeAlive != context.ActiveId &&
                context.ActiveIdIsAlive == context.ActiveId &&
                context.ActiveIdWindow.Handle != null && context.ActiveId != context.ActiveIdWindow.MoveId)
            {
                state.ActiveID = context.ActiveId;
                state.ActiveRow = rowID;
            }
            if (navID != 0 && navID == context.NavId && !navAlive && context.NavIdIsAlive)
            {
                state.NavID = navID;
                state.NavRow = rowID;
            }
            if (context.OpenPopupStack.Size > popupCount || context.OpenPopupStack.Size > 0 &&
                context.OpenPopupStack.Size == popupCount &&
                context.OpenPopupStack.Data[popupCount - 1].PopupId != popupID)
            {
                state.PopupID = context.OpenPopupStack.Data[context.OpenPopupStack.Size - 1].PopupId;
                state.PopupRow = rowID;
            }
        }
    }

    internal sealed class ListState
    {
        internal int Frame = -1;
        internal uint ActiveID;
        internal uint ActiveRow;
        internal uint NavID;
        internal uint ObservedNavID;
        internal uint NavRow;
        internal uint PopupID;
        internal uint PopupRow;
        internal bool Released;
        internal uint ReleasedRow;

        internal void Reset()
        {
            ActiveID = NavID = ObservedNavID = PopupID = 0;
            Released = false;
        }
    }
}
