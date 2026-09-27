using FFXIVClientStructs.FFXIV.Component.GUI;
using KamiToolKit;

namespace OmniToolbox.UI;

internal static unsafe class NativeNodeDetach
{
    // KamiToolKit 拆挂非首子节点时不会减少非组件父节点的 ChildCount，累积后游戏会按错误计数遍历节点链表。
    public static bool DetachAndDispose(NodeBase? node)
    {
        if (node is null)
        {
            return false;
        }

        AtkResNode* nativeNode = node;
        if (nativeNode != null)
        {
            // 碰撞管理器缓存的悬停节点不会在节点释放时自动清除，先清掉避免同帧读到已释放节点。
            var collisionManager = AtkStage.Instance()->AtkCollisionManager;
            if (collisionManager != null &&
                (AtkResNode*)collisionManager->IntersectingCollisionNode == nativeNode)
            {
                collisionManager->IntersectingCollisionNode = null;
                collisionManager->IntersectingAddon = null;
            }

            var parent = nativeNode->ParentNode;
            if (parent != null &&
                parent->GetNodeType() is not NodeType.Component &&
                parent->ChildNode != nativeNode &&
                parent->ChildCount > 0)
            {
                parent->ChildCount--;
            }
        }

        node.Dispose();
        return nativeNode != null;
    }

    // KamiToolKit 的列表更新排队到下一帧，拆卸后同一帧的 RequestedUpdate 仍会遍历到已释放的碰撞节点，这里同步重建。
    public static void UpdateNodeLists(AtkUnitBase* addon)
    {
        if (addon == null)
        {
            return;
        }

        addon->UldManager.UpdateDrawNodeList();
        addon->UpdateCollisionNodeList(false);
    }
}
