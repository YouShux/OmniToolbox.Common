using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit.BaseTypes;
using OmenTools.Extensions;

namespace OmniToolbox.UI;

public static unsafe class OmniNativeWindowAttachment
{
    public static void SyncBeside
    (
        NativeAddon  window,
        AtkUnitBase* addon,
        AtkUnitBase* sourceAddon,
        float        gap,
        float        verticalOffset,
        bool         preferRight = false
    )
    {
        var stage = AtkStage.Instance();
        if (stage == null || sourceAddon->WindowNode == null || addon->WindowNode == null)
            return;

        addon->EnableTitleBarContextMenu = false;
        addon->DisableUserScaling        = false;
        var sourceNode = (AtkResNode*)sourceAddon->WindowNode;
        var targetNode = (AtkResNode*)addon->WindowNode;
        var scale = sourceNode->GetScale();
        var currentScale = targetNode->GetScale();
        if (Vector2.DistanceSquared(currentScale, scale) > 0.000001f)
        {
            addon->SetScale(scale.X / AtkUnitBase.GetGlobalUIScale(), true);
            currentScale = targetNode->GetScale();
            if (currentScale.X > 0f && currentScale.Y > 0f)
            {
                window.RootNode.ScaleX *= scale.X / currentScale.X;
                window.RootNode.ScaleY *= scale.Y / currentScale.Y;
            }
        }

        var source = sourceNode->GetNodeState();
        var target = targetNode->GetNodeState();
        var desired = OmniWindowGeometry.GetBesidePosition(
            source.TopLeft, new(source.Width, source.Height), new(target.Width, target.Height),
            Vector2.Zero, new(stage->ScreenSize.Width, stage->ScreenSize.Height),
            new(gap * scale.X, verticalOffset * scale.Y), preferRight);
        var position = new Vector2(addon->X, addon->Y) + desired - target.TopLeft;
        if (MathF.Abs(addon->X - position.X) > 0.5f || MathF.Abs(addon->Y - position.Y) > 0.5f)
            window.SetWindowPosition(position);
    }
}
