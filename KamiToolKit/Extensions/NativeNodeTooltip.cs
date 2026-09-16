using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;
using KamiToolKit.Nodes;
using Lumina.Text.ReadOnly;

namespace OmniToolbox.UI;

internal static class NativeNodeTooltip
{
    private static readonly ConditionalWeakTable<NodeBase, TextTooltipState> TextTooltips = new();

    public static void SetTextTooltip(this NodeBase node, ReadOnlySeString tooltip) =>
        TextTooltips.GetValue(node, static value => new(value)).Text = tooltip;

    private sealed class TextTooltipState
    {
        private readonly NodeBase node;

        public TextTooltipState(NodeBase node)
        {
            this.node = node;
            node.AddEvent(AtkEventType.MouseOver, () => node.ShowTextTooltip(Text));
            node.AddEvent(AtkEventType.MouseOut, node.HideTooltip);
            if (node is not ComponentNode)
            {
                node.AddNodeFlags(NodeFlags.HasCollision);
            }
        }

        public ReadOnlySeString Text { get; set; }
    }
}
