using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Interface.Textures;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using OmenTools.Interop.Game.Helpers;
using OmniToolbox.Config;
using OmniToolbox.Host;
using OmniToolbox.Lifecycle;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;

namespace OmniToolbox.Items;

internal sealed unsafe class ItemDetailImagePreview : IDisposable
{
    private const string AddonName = "ItemDetail";

    private readonly ItemImagePreviewConfig config;
    private readonly ItemPreviewService itemPreviewService;
    private readonly AddonEventRegistry addonEvents = new(DalamudServices.AddonLifecycle);
    private ISharedImmediateTexture? previewTexture;
    private ItemPreviewImage? preview;
    private uint currentItemID;
    private long nextRetryTick;
    private bool lookupPending;
    private bool drawRegistered;
    private bool disposed;

    public ItemDetailImagePreview(ItemImagePreviewConfig config, ItemPreviewService itemPreviewService)
    {
        this.config = config;
        this.itemPreviewService = itemPreviewService;

        try
        {
            addonEvents.Register(AddonEvent.PostUpdate, AddonName, OnItemDetailUpdate);
            addonEvents.Register(AddonEvent.PreFinalize, AddonName, OnItemDetailFinalize);
            DalamudServices.PluginInterface.UiBuilder.Draw += Draw;
            drawRegistered = true;
            Refresh();
        }
        catch
        {
            if (drawRegistered)
            {
                DalamudServices.PluginInterface.UiBuilder.Draw -= Draw;
                drawRegistered = false;
            }

            addonEvents.Dispose();
            throw;
        }
    }

    public void Refresh() => UpdateCurrentItem(true);

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        try
        {
            addonEvents.Dispose();
        }
        finally
        {
            if (drawRegistered)
            {
                DalamudServices.PluginInterface.UiBuilder.Draw -= Draw;
                drawRegistered = false;
            }

            ClearCurrentItem();
        }
    }

    private void OnItemDetailUpdate(AddonEvent _, AddonArgs args) => UpdateCurrentItem(false);

    private void OnItemDetailFinalize(AddonEvent _, AddonArgs args) => ClearCurrentItem();

    private void UpdateCurrentItem(bool force)
    {
        try
        {
            var agent = AgentItemDetail.Instance();
            if (agent == null || agent->ItemId == 0)
            {
                ClearCurrentItem();
                return;
            }

            var itemID = agent->ItemId;
            if (!force &&
                itemID == currentItemID &&
                (!lookupPending || Environment.TickCount64 < nextRetryTick))
            {
                return;
            }

            currentItemID = itemID;
            switch (itemPreviewService.ResolveImagePreview(itemID, config, out var image))
            {
                case ItemPreviewImageLookup.Found:
                    preview = image;
                    previewTexture = DalamudServices.TextureProvider.GetFromGame(image.TexturePath);
                    lookupPending = false;
                    break;
                case ItemPreviewImageLookup.Pending:
                    ClearImage();
                    lookupPending = true;
                    nextRetryTick = Environment.TickCount64 + 500;
                    break;
                default:
                    ClearImage();
                    lookupPending = false;
                    break;
            }
        }
        catch (Exception ex)
        {
            ClearImage();
            lookupPending = false;
            DalamudServices.PluginLog.Debug(ex, "Updating the ItemDetail image preview failed.");
        }
    }

    private void Draw()
    {
        if (lookupPending && Environment.TickCount64 >= nextRetryTick)
        {
            UpdateCurrentItem(true);
        }

        if (preview is not { } image ||
            previewTexture?.GetWrapOrDefault() is not { } texture)
        {
            return;
        }

        var addon = AddonHelper.GetByName(AddonName);
        if (addon == null ||
            !addon->IsVisible ||
            addon->RootNode == null)
        {
            return;
        }

        var scale = Math.Clamp(config.Scale, 0.1f, 3f);
        var imageSize = OmniTheme.FitImageSize(
            new Vector2(texture.Width, texture.Height),
            OmniTheme.Scale(image.Kind is ItemPreviewImageKind.Minion or
                ItemPreviewImageKind.Hairstyle or
                ItemPreviewImageKind.FashionAccessory
                    ? 240f
                    : 300f) * scale);
        var displaySize = ImGui.GetIO().DisplaySize;
        if (displaySize.X <= 0f || displaySize.Y <= 0f)
        {
            return;
        }

        var gap = OmniTheme.Scale(6f) * scale;
        var desired = new Vector2(
            config.Position == ItemImagePreviewPosition.Left
                ? addon->X - imageSize.X - gap
                : addon->X + addon->RootNode->Width * addon->Scale + gap,
            addon->Y - (image.Kind == ItemPreviewImageKind.Mount ? OmniTheme.Scale(22f) * scale : 0f));
        var position = Vector2.Clamp(
            desired,
            Vector2.Zero,
            Vector2.Max(Vector2.Zero, displaySize - imageSize));
        var drawList = ImGui.GetForegroundDrawList();
        drawList.AddImage(texture.Handle, position, position + imageSize);

        var statusSize = MathF.Min(imageSize.X, imageSize.Y) * 0.3f;
        var statusMin = position + imageSize - new Vector2(statusSize);
        FramedGameIcon.DrawCollectionStatus(
            drawList,
            statusMin,
            position + imageSize,
            image.IsUnlocked);
    }

    private void ClearCurrentItem()
    {
        currentItemID = 0;
        lookupPending = false;
        ClearImage();
    }

    private void ClearImage()
    {
        preview = null;
        previewTexture = null;
    }
}
