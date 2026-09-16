using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Collections;
using OmniToolbox.Host;
using OmniToolbox.Notifications;
using OmniToolbox.UI;

namespace OmniToolbox.Items;

public sealed unsafe partial class ItemPreviewService : IDisposable
{
    private readonly Dictionary<uint, PreviewTarget> itemPreviewTargets;
    private readonly Dictionary<DrawDataContainer.EquipmentSlot, EquipmentModelId> originalEquipment = [];
    private readonly Dictionary<DrawDataContainer.WeaponSlot, WeaponModelId> originalWeapons = [];
    private ushort? originalGlassesID;
    private ushort? originalMountID;
    private CharacterModelState? originalMountCharacterState;
    private ushort? originalCompanionID;
    private ushort? originalOrnamentID;
    private byte? originalHairstyleID;
    private uint? previewHairstyleID;
    private byte? previewHairstyleFeatureID;
    private CharacterActionState? originalEmoteState;
    private BardingPreviewState? originalBardingState;
    private uint lastTerritoryID;
    private nint lastPlayerAddress;

    public ItemPreviewService()
    {
        itemPreviewTargets = BuildPreviewIndex();
        lastTerritoryID = DService.Instance().ClientState.TerritoryType;
        lastPlayerAddress = GetLocalPlayerAddress();
    }

    public bool CanTryOn(uint? itemID) =>
        itemID is { } id &&
        itemPreviewTargets.TryGetValue(id, out var target) &&
        target.Type == CollectionType.Equipment;

    public bool CanPreview(uint? itemID) =>
        itemID is { } id && itemPreviewTargets.ContainsKey(id);

    public bool CanPreview(CollectionEntry item) =>
        item.Type == CollectionType.Equipment
            ? CanTryOn(item.ItemID)
            : IsTargetAvailable(new(item.Type, item.ID));

    public bool TryOn(uint itemID)
    {
        if (!CanTryOn(itemID) || !DalamudServices.PlayerState.IsLoaded || AgentTryon.Instance() == null)
        {
            return false;
        }

        try
        {
            AgentTryon.TryOn(0, itemID, 0, 0);
            return true;
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Warning(ex, $"Item preview failed for {itemID}.");
            return false;
        }
    }

    public bool ExecuteEmote(uint emoteID)
    {
        var emoteManager = EmoteManager.Instance();
        if (!CanUseDirectPreview() ||
            !LuminaGetter.TryGetRow<Emote>(emoteID, out _) ||
            emoteManager == null)
        {
            return false;
        }

        return emoteManager->ExecuteEmote((ushort)emoteID);
    }

    public bool Preview(uint itemID) =>
        itemPreviewTargets.TryGetValue(itemID, out var target) && Preview(target, itemID);

    public bool Preview(CollectionEntry item) =>
        CanPreview(item) && Preview(
            item.Type == CollectionType.Equipment
                ? new(CollectionType.Equipment, item.ItemID!.Value)
                : new(item.Type, item.ID),
            item.ItemID);

    public void Tick(IFramework _)
    {
        if (!HasDirectPreview())
        {
            return;
        }

        var territoryID = DService.Instance().ClientState.TerritoryType;
        var playerAddress = GetLocalPlayerAddress();
        if (territoryID == lastTerritoryID && playerAddress == lastPlayerAddress)
        {
            return;
        }

        ClearPreview();
        lastTerritoryID = territoryID;
        lastPlayerAddress = playerAddress;
    }

    public void ClearPreviewResidue()
    {
        var shouldBeUnmounted = originalMountID.HasValue
            ? originalMountID.Value == 0
            : !DService.Instance().Condition[ConditionFlag.Mounted];
        ClearPreview();

        var character = CanUseDirectPreview() ? GetLocalCharacter() : null;
        if (character == null || !shouldBeUnmounted || !HasLocalMountResidue(character))
        {
            return;
        }

        OmniNotifier.Popup(
            OmniLoc.Get("Collection.Relic.ClearPreview"),
            OmniLoc.Get("Collection.Relic.ClearPreview.MountFailed"),
            NotificationType.Warning);
    }

    public void ClearPreview()
    {
        if (!HasDirectPreview())
        {
            return;
        }

        var canRestore = lastTerritoryID == DService.Instance().ClientState.TerritoryType &&
                         lastPlayerAddress == GetLocalPlayerAddress();
        var character = canRestore ? GetLocalCharacter() : null;
        if (character != null)
        {
            try
            {
                foreach (var (slot, model) in originalWeapons)
                {
                    character->DrawData.LoadWeapon(slot, model, 0, 0, 0, 0, false);
                }

                foreach (var (slot, model) in originalEquipment)
                {
                    var restoreModel = model;
                    character->DrawData.LoadEquipment(slot, &restoreModel, true);
                }

                if (originalGlassesID.HasValue)
                {
                    character->DrawData.SetGlasses(0, originalGlassesID.Value);
                }

                if (originalMountID.HasValue)
                {
                    if (originalMountID.Value == 0)
                    {
                        RestoreCharacterDrawState(character, true);
                    }
                    else
                    {
                        character->Mount.SetupMount((short)originalMountID.Value, 0, 0, 0, 0);
                    }
                }

                if (originalMountCharacterState.HasValue)
                {
                    RestoreCharacterModelState(character, originalMountCharacterState.Value);
                }

                RestoreCharacterDrawState(character, false);

                if (originalCompanionID.HasValue)
                {
                    character->CompanionData.SetupCompanion((short)originalCompanionID.Value, 0);
                }

                if (originalOrnamentID.HasValue)
                {
                    character->OrnamentData.SetupOrnament((short)originalOrnamentID.Value, 0);
                }

                if (originalHairstyleID.HasValue)
                {
                    ApplyHairstyle(character, originalHairstyleID.Value);
                }

                if (originalEmoteState.HasValue)
                {
                    RestoreCharacterActionState(character, originalEmoteState.Value);
                }
            }
            catch (Exception ex)
            {
                DalamudServices.PluginLog.Warning(ex, "Restoring direct item preview failed.");
            }
        }

        if (canRestore && originalBardingState.HasValue)
        {
            try
            {
                RestoreBarding(originalBardingState.Value);
            }
            catch (Exception ex)
            {
                DalamudServices.PluginLog.Warning(ex, "Restoring chocobo barding preview failed.");
            }
        }

        originalWeapons.Clear();
        originalEquipment.Clear();
        originalGlassesID = null;
        originalMountID = null;
        originalMountCharacterState = null;
        originalCompanionID = null;
        originalOrnamentID = null;
        originalHairstyleID = null;
        previewHairstyleID = null;
        previewHairstyleFeatureID = null;
        originalEmoteState = null;
        originalBardingState = null;
    }

    public void Dispose() => ClearPreview();

    private static Dictionary<uint, PreviewTarget> BuildPreviewIndex()
    {
        var targets = new Dictionary<uint, PreviewTarget>();
        var emotesByUnlockLink = new Dictionary<uint, uint>();
        var hairstylesByUnlockLink = new Dictionary<uint, uint>();
        foreach (var emote in LuminaGetter.Get<Emote>())
        {
            if (emote.RowId != 0 && emote.UnlockLink != 0)
            {
                emotesByUnlockLink.TryAdd(emote.UnlockLink, emote.RowId);
            }
        }

        foreach (var hairstyle in HairstyleData.GetPurchasableRows())
        {
            if (hairstyle.Icon == 131_094)
            {
                continue;
            }

            hairstylesByUnlockLink.TryAdd(hairstyle.UnlockLink, hairstyle.RowId);
            if (hairstyle.HintItem.RowId != 0)
            {
                targets.TryAdd(
                    hairstyle.HintItem.RowId,
                    new(CollectionType.Hairstyle, hairstyle.RowId));
            }
        }

        foreach (var item in LuminaGetter.Get<Item>())
        {
            if (item.RowId == 0)
            {
                continue;
            }

            if (item.EquipSlotCategory.RowId != 0 && item.ModelMain != 0)
            {
                targets[item.RowId] = new(CollectionType.Equipment, item.RowId);
                continue;
            }

            if (!item.ItemAction.IsValid)
            {
                continue;
            }

            var action = item.ItemAction.Value;
            var targetID = (uint)action.Data[0];
            PreviewTarget target;
            switch ((ItemActionKind)action.Action.RowId)
            {
                case ItemActionKind.Companion:
                    target = new(CollectionType.Minion, targetID);
                    break;
                case ItemActionKind.BuddyEquip:
                    target = new(CollectionType.Barding, targetID);
                    break;
                case ItemActionKind.Mount:
                    target = new(CollectionType.Mount, targetID);
                    break;
                case ItemActionKind.UnlockLink:
                    if (targets.ContainsKey(item.RowId))
                    {
                        continue;
                    }

                    if (hairstylesByUnlockLink.TryGetValue(targetID, out var hairstyleId))
                    {
                        target = new(CollectionType.Hairstyle, hairstyleId);
                    }
                    else if (emotesByUnlockLink.TryGetValue(targetID, out var emoteId))
                    {
                        target = new(CollectionType.Emote, emoteId);
                    }
                    else
                    {
                        continue;
                    }

                    break;
                case ItemActionKind.Ornament:
                    target = new(CollectionType.FashionAccessory, targetID);
                    break;
                case ItemActionKind.Glasses:
                    target = new(CollectionType.Glasses, item.AdditionalData.RowId);
                    break;
                default:
                    continue;
            }

            if (IsTargetAvailable(target))
            {
                targets.TryAdd(item.RowId, target);
            }
        }

        return targets;
    }

    private static bool IsTargetAvailable(PreviewTarget target) => target.Type switch
    {
        CollectionType.Glasses =>
            target.ID <= ushort.MaxValue && LuminaGetter.TryGetRow<Glasses>(target.ID, out _),
        CollectionType.Mount =>
            target.ID <= short.MaxValue && LuminaGetter.TryGetRow<Mount>(target.ID, out _),
        CollectionType.Minion =>
            target.ID <= short.MaxValue &&
            LuminaGetter.TryGetRow<Lumina.Excel.Sheets.Companion>(target.ID, out _),
        CollectionType.Emote =>
            target.ID <= ushort.MaxValue && LuminaGetter.TryGetRow<Emote>(target.ID, out _),
        CollectionType.FashionAccessory =>
            target.ID <= short.MaxValue &&
            LuminaGetter.TryGetRow<Lumina.Excel.Sheets.Ornament>(target.ID, out _),
        CollectionType.Hairstyle => LuminaGetter.TryGetRow<CharaMakeCustomize>(target.ID, out _),
        CollectionType.Barding => LuminaGetter.TryGetRow<BuddyEquip>(target.ID, out _),
        _ => false
    };

    private bool Preview(PreviewTarget target, uint? itemID)
    {
        try
        {
            return target.Type switch
            {
                CollectionType.Equipment => PreviewEquipment(target.ID),
                CollectionType.Glasses => PreviewGlasses(target.ID),
                CollectionType.Mount => PreviewMount(target.ID),
                CollectionType.Minion => PreviewCompanion(target.ID),
                CollectionType.Emote => PreviewEmote(target.ID),
                CollectionType.FashionAccessory => PreviewOrnament(target.ID),
                CollectionType.Hairstyle => PreviewHairstyle(target.ID, itemID),
                CollectionType.Barding => PreviewBarding(target.ID),
                _ => false
            };
        }
        catch (Exception ex)
        {
            DalamudServices.PluginLog.Warning(
                ex,
                $"Item {target.Type} preview failed for {target.ID}.");
            return false;
        }
    }

    public bool PreviewEquipment(uint itemID, int slot = -1, byte stain0 = 0, byte stain1 = 0)
    {
        if (!TryGetLocalCharacterForPreview(out var character) ||
            !LuminaGetter.TryGetRow<Item>(itemID, out var item) ||
            !item.EquipSlotCategory.IsValid ||
            item.EquipSlotCategory.RowId == 0 ||
            item.ModelMain == 0)
        {
            return false;
        }

        var slotCategory = item.EquipSlotCategory.Value;
        if (TryGetWeaponSlot(slotCategory, out var weaponSlot))
        {
            originalWeapons.TryAdd(weaponSlot, character->DrawData.Weapon(weaponSlot).ModelId);
            character->DrawData.LoadWeapon(
                weaponSlot,
                BuildWeaponModel(item, stain0, stain1),
                0,
                0,
                0,
                0,
                false);
            return true;
        }

        if (!TryGetEquipmentSlot(slotCategory, slot, out var equipmentSlot))
        {
            return false;
        }

        originalEquipment.TryAdd(equipmentSlot, character->DrawData.Equipment(equipmentSlot));
        var model = BuildEquipmentModel(item, stain0, stain1);
        character->DrawData.LoadEquipment(equipmentSlot, &model, true);
        return true;
    }

    public bool IsEquipmentPreviewApplied(uint itemID, int slot = -1, byte stain0 = 0, byte stain1 = 0)
    {
        var character = CanUseDirectPreview() ? GetLocalCharacter() : null;
        if (character == null ||
            !LuminaGetter.TryGetRow<Item>(itemID, out var item) ||
            !item.EquipSlotCategory.IsValid ||
            item.EquipSlotCategory.RowId == 0 ||
            item.ModelMain == 0)
        {
            return false;
        }

        var slotCategory = item.EquipSlotCategory.Value;
        if (TryGetWeaponSlot(slotCategory, out var weaponSlot))
        {
            return character->DrawData.Weapon(weaponSlot).ModelId.Value ==
                   BuildWeaponModel(item, stain0, stain1).Value;
        }

        if (!TryGetEquipmentSlot(slotCategory, slot, out var equipmentSlot))
        {
            return false;
        }

        var current = character->DrawData.Equipment(equipmentSlot);
        var expected = BuildEquipmentModel(item, stain0, stain1);
        return current.Id == expected.Id &&
               current.Variant == expected.Variant &&
               current.Stain0 == expected.Stain0 &&
               current.Stain1 == expected.Stain1;
    }

    public bool PreviewGlasses(uint glassesID)
    {
        if (!TryGetLocalCharacterForPreview(out var character))
        {
            return false;
        }

        originalGlassesID ??= character->DrawData.GlassesIds[0];
        character->DrawData.SetGlasses(0, (ushort)glassesID);
        return true;
    }

    public bool IsGlassesPreviewApplied(uint glassesID)
    {
        var character = CanUseDirectPreview() ? GetLocalCharacter() : null;
        return character != null && character->DrawData.GlassesIds[0] == (ushort)glassesID;
    }

    private bool PreviewMount(uint mountID)
    {
        if (!TryGetLocalCharacterForPreview(out var character))
        {
            return false;
        }

        originalMountID ??= character->Mount.MountId;
        originalMountCharacterState ??= CaptureCharacterModelState(character);
        character->Mount.CreateAndSetupMount((short)mountID, 0, 0, 0, 0, 0, 0);
        return true;
    }

    private bool PreviewCompanion(uint companionID)
    {
        if (!TryGetLocalCharacterForPreview(out var character))
        {
            return false;
        }

        originalCompanionID ??= character->CompanionData.CompanionId;
        character->CompanionData.SetupCompanion((short)companionID, 0);
        return true;
    }

    private bool PreviewOrnament(uint ornamentID)
    {
        if (!TryGetLocalCharacterForPreview(out var character))
        {
            return false;
        }

        originalOrnamentID ??= character->OrnamentData.OrnamentId;
        character->OrnamentData.SetupOrnament((short)ornamentID, 0);
        return true;
    }

    private bool PreviewEmote(uint emoteID)
    {
        if (!TryGetLocalCharacterForPreview(out var character) ||
            !LuminaGetter.TryGetRow<Emote>(emoteID, out var emote))
        {
            return false;
        }

        var originalState = originalEmoteState ?? CaptureCharacterActionState(character);
        var emoteManager = EmoteManager.Instance();
        if (emoteManager != null && emoteManager->ExecuteEmote((ushort)emoteID))
        {
            originalEmoteState ??= originalState;
            return true;
        }

        if (!PlayLocalEmote(character, emote))
        {
            return false;
        }

        originalEmoteState ??= originalState;
        return true;
    }

    private bool PreviewHairstyle(uint hairstyleID, uint? itemID)
    {
        if (!TryGetLocalCharacterForPreview(out var character) ||
            ResolveHairstyle(hairstyleID, itemID, character) is not { } hairstyle)
        {
            return false;
        }

        originalHairstyleID ??= character->DrawData.CustomizeData.Hairstyle;
        previewHairstyleID = hairstyleID;
        previewHairstyleFeatureID = (byte)hairstyle.FeatureID;
        ApplyHairstyle(character, previewHairstyleFeatureID.Value);
        return true;
    }

    public bool PreviewHairstyle(uint hairstyleID) => PreviewHairstyle(hairstyleID, null);

    public bool IsHairstylePreviewApplied(uint hairstyleID)
    {
        var character = CanUseDirectPreview() ? GetLocalCharacter() : null;
        return character != null &&
               previewHairstyleID == hairstyleID &&
               previewHairstyleFeatureID == character->DrawData.CustomizeData.Hairstyle;
    }

    public bool RestoreHairstylePreview()
    {
        if (!originalHairstyleID.HasValue)
        {
            return true;
        }

        if (!TryGetLocalCharacterForPreview(out var character))
        {
            return false;
        }

        ApplyHairstyle(character, originalHairstyleID.Value);
        previewHairstyleID = null;
        previewHairstyleFeatureID = null;
        return true;
    }

    private bool PreviewBarding(uint bardingID)
    {
        if (!CanUseDirectPreview() ||
            !LuminaGetter.TryGetRow<BuddyEquip>(bardingID, out var barding))
        {
            return false;
        }

        var chocobo = GetLocalChocobo();
        if (chocobo == null)
        {
            return false;
        }

        TrackPreviewOwner();
        var entityID = ((GameObject*)chocobo)->EntityId;
        if (!originalBardingState.HasValue || originalBardingState.Value.EntityID != entityID)
        {
            originalBardingState = CaptureBardingState(chocobo);
        }

        ApplyBarding(chocobo, barding);
        return true;
    }

    private static bool PlayLocalEmote(Character* character, Emote emote)
    {
        var intro = (ushort)emote.ActionTimeline[1].RowId;
        var loop = (ushort)emote.ActionTimeline[0].RowId;

        character->SetMode(
            emote.EmoteMode.RowId != 0
                ? (CharacterModes)emote.EmoteMode.Value.ConditionMode
                : CharacterModes.Normal,
            emote.EmoteMode.RowId != 0 ? (byte)emote.EmoteMode.RowId : (byte)0);

        if (intro != 0 && loop != 0)
        {
            character->Timeline.PlayActionTimeline(intro, loop);
        }
        else if (loop != 0)
        {
            character->Timeline.TimelineSequencer.PlayTimeline(loop);
        }
        else
        {
            character->Timeline.TimelineSequencer.PlayTimeline(intro != 0 ? intro : (ushort)3);
        }

        return true;
    }

    private static CharaMakeCustomize? ResolveHairstyle(
        uint hairstyleID,
        uint? itemID,
        Character* character)
    {
        if (!LuminaGetter.TryGetRow<CharaMakeCustomize>(hairstyleID, out var selected))
        {
            return null;
        }

        var customize = character->DrawData.CustomizeData;
        foreach (var row in LuminaGetter.Get<HairMakeType>())
        {
            if (row.Tribe.RowId != customize.Tribe || row.Gender != customize.Sex)
            {
                continue;
            }

            foreach (var rowID in row.CharaMakeStruct[0].SubMenuParam)
            {
                if (rowID == 0 ||
                    !LuminaGetter.TryGetRow<CharaMakeCustomize>(rowID, out var hairstyle) ||
                    hairstyle.FeatureID != selected.FeatureID)
                {
                    continue;
                }

                return hairstyle;
            }

            return GetHairstyleVariant(selected.UnlockLink, customize.Tribe, customize.Sex) ?? selected;
        }

        return null;
    }

    private static IEnumerable<uint> GetHairstyleRows(HairMakeType hairMakeType)
    {
        for (var menuIndex = 0; menuIndex < hairMakeType.CharaMakeStruct.Count; menuIndex++)
        {
            foreach (var rowID in hairMakeType.CharaMakeStruct[menuIndex].SubMenuParam)
            {
                if (rowID != 0)
                {
                    yield return rowID;
                }
            }
        }
    }

    private static CharaMakeCustomize? GetHairstyleVariant(uint unlockLink, byte tribe, byte sex)
    {
        if (unlockLink == 0)
        {
            return null;
        }

        var variantIndex = (tribe, sex) switch
        {
            (8, 1) => 9,
            _ => -1
        };
        if (variantIndex < 0)
        {
            return null;
        }

        var currentIndex = 0;
        foreach (var row in LuminaGetter.Get<CharaMakeCustomize>())
        {
            if (row.UnlockLink != unlockLink)
            {
                continue;
            }

            if (currentIndex++ == variantIndex)
            {
                return row;
            }
        }

        return null;
    }

    private static void ApplyHairstyle(Character* character, byte hairstyleID)
    {
        character->DrawData.CustomizeData.Hairstyle = hairstyleID;
        var characterBase = ((GameObject*)character)->GetCharacterBase();
        if (characterBase == null || characterBase->GetModelType() != CharacterBase.ModelType.Human)
        {
            return;
        }

        var drawData = new Human.DrawData
        {
            CustomizeData = character->DrawData.CustomizeData
        };
        ((Human*)characterBase)->UpdateDrawData(&drawData, true);
    }

    private bool TryGetLocalCharacterForPreview(out Character* character)
    {
        character = null;
        if (!CanUseDirectPreview())
        {
            return false;
        }

        character = GetLocalCharacter();
        if (character == null)
        {
            return false;
        }

        TrackPreviewOwner();
        return true;
    }

    private static bool CanUseDirectPreview() =>
        DService.Instance().ClientState.IsLoggedIn &&
        !DService.Instance().Condition.IsBetweenAreas;

    private void TrackPreviewOwner()
    {
        if (HasDirectPreview())
        {
            return;
        }

        lastTerritoryID = DService.Instance().ClientState.TerritoryType;
        lastPlayerAddress = GetLocalPlayerAddress();
    }

    private bool HasDirectPreview() =>
        originalEquipment.Count != 0 ||
        originalWeapons.Count != 0 ||
        originalGlassesID.HasValue ||
        originalMountID.HasValue ||
        originalMountCharacterState.HasValue ||
        originalCompanionID.HasValue ||
        originalOrnamentID.HasValue ||
        originalHairstyleID.HasValue ||
        originalEmoteState.HasValue ||
        originalBardingState.HasValue;

    private static Character* GetLocalCharacter()
    {
        var localPlayer = DService.Instance().ObjectTable.LocalPlayer;
        return localPlayer is null || localPlayer.Address == nint.Zero
            ? null
            : (Character*)localPlayer.Address;
    }

    private static nint GetLocalPlayerAddress() =>
        DService.Instance().ObjectTable.LocalPlayer?.Address ?? nint.Zero;

    private static Character* GetLocalChocobo(uint expectedEntityID = 0)
    {
        var objectTable = DService.Instance().ObjectTable;
        var ownerID = objectTable.LocalPlayer?.EntityID ?? 0;
        if (expectedEntityID != 0)
        {
            return GetLocalChocobo(expectedEntityID, ownerID);
        }

        var uiState = UIState.Instance();
        var companion = uiState != null ? uiState->Buddy.CompanionInfo.Companion : null;
        if (companion != null)
        {
            var chocobo = GetLocalChocobo(companion->EntityId, ownerID);
            if (chocobo != null)
            {
                return chocobo;
            }
        }

        foreach (var gameObject in objectTable)
        {
            if (IsLocalChocobo(gameObject.ToStruct(), ownerID))
            {
                return (Character*)gameObject.Address;
            }
        }

        return null;
    }

    private static Character* GetLocalChocobo(uint entityID, uint ownerID)
    {
        var gameObject = DService.Instance().ObjectTable.SearchByEntityID(entityID);
        return gameObject is not null && IsLocalChocobo(gameObject.ToStruct(), ownerID)
            ? (Character*)gameObject.Address
            : null;
    }

    private static bool IsLocalChocobo(GameObject* gameObject, uint ownerID) =>
        gameObject != null &&
        gameObject->ObjectKind == ObjectKind.BattleNpc &&
        gameObject->BattleNpcSubKind == BattleNpcSubKind.Buddy &&
        (ownerID == 0 || gameObject->OwnerId == ownerID);

    private static CharacterModelState CaptureCharacterModelState(Character* character) => new()
    {
        Mode = character->Mode,
        ModeParam = character->ModeParam,
        ModelScale = character->ModelScale,
        ModelScaleID = character->ModelContainer.ModelScaleId,
        UnscaledRadius = character->ModelContainer.UnscaledRadius
    };

    private static void RestoreCharacterModelState(Character* character, CharacterModelState state)
    {
        character->ModelScale = state.ModelScale;
        character->ModelContainer.ModelScaleId = state.ModelScaleID;
        character->ModelContainer.UnscaledRadius = state.UnscaledRadius;
        character->SetMode(state.Mode, state.ModeParam);
    }

    private static CharacterActionState CaptureCharacterActionState(Character* character) => new()
    {
        Mode = character->Mode,
        ModeParam = character->ModeParam
    };

    private static void RestoreCharacterActionState(Character* character, CharacterActionState state)
    {
        character->Timeline.TimelineSequencer.PlayTimeline(3);
        character->SetMode(state.Mode, state.ModeParam);
    }

    private static void RestoreCharacterDrawState(Character* character, bool resetMode)
    {
        var gameObject = (GameObject*)character;
        gameObject->EnableDraw();
        gameObject->SetReadyToDraw();
        if (resetMode)
        {
            character->SetMode(CharacterModes.Normal, 0);
        }
    }

    private static bool HasLocalMountResidue(Character* character) =>
        character->Mount.MountId != 0 ||
        character->Mount.MountObject != null ||
        character->Mode == CharacterModes.Mounted;

    private static BardingPreviewState CaptureBardingState(Character* chocobo) => new()
    {
        EntityID = ((GameObject*)chocobo)->EntityId,
        Head = chocobo->DrawData.Equipment(DrawDataContainer.EquipmentSlot.Head),
        Body = chocobo->DrawData.Equipment(DrawDataContainer.EquipmentSlot.Body),
        Feet = chocobo->DrawData.Equipment(DrawDataContainer.EquipmentSlot.Feet)
    };

    private static void RestoreBarding(BardingPreviewState state)
    {
        var chocobo = GetLocalChocobo(state.EntityID);
        if (chocobo == null)
        {
            return;
        }

        var head = state.Head;
        var body = state.Body;
        var feet = state.Feet;
        chocobo->DrawData.LoadEquipment(DrawDataContainer.EquipmentSlot.Head, &head, true);
        chocobo->DrawData.LoadEquipment(DrawDataContainer.EquipmentSlot.Body, &body, true);
        chocobo->DrawData.LoadEquipment(DrawDataContainer.EquipmentSlot.Feet, &feet, true);
    }

    private static void ApplyBarding(Character* chocobo, BuddyEquip barding)
    {
        if (barding.ModelTop != 0)
        {
            ApplyBardingSlot(chocobo, DrawDataContainer.EquipmentSlot.Head, (uint)barding.ModelTop);
        }

        if (barding.ModelBody != 0)
        {
            ApplyBardingSlot(chocobo, DrawDataContainer.EquipmentSlot.Body, (uint)barding.ModelBody);
        }

        if (barding.ModelLegs != 0)
        {
            ApplyBardingSlot(chocobo, DrawDataContainer.EquipmentSlot.Feet, (uint)barding.ModelLegs);
        }
    }

    private static void ApplyBardingSlot(
        Character* chocobo,
        DrawDataContainer.EquipmentSlot slot,
        uint modelValue)
    {
        var current = chocobo->DrawData.Equipment(slot);
        var model = new EquipmentModelId
        {
            Id = (ushort)modelValue,
            Variant = (byte)(modelValue >> 16),
            Stain0 = current.Stain0,
            Stain1 = current.Stain1
        };
        chocobo->DrawData.LoadEquipment(slot, &model, true);
    }

    private static bool TryGetWeaponSlot(
        EquipSlotCategory equipSlotCategory,
        out DrawDataContainer.WeaponSlot slot)
    {
        if (equipSlotCategory.MainHand != 0)
        {
            slot = DrawDataContainer.WeaponSlot.MainHand;
            return true;
        }

        if (equipSlotCategory.OffHand != 0)
        {
            slot = DrawDataContainer.WeaponSlot.OffHand;
            return true;
        }

        slot = DrawDataContainer.WeaponSlot.MainHand;
        return false;
    }

    private static bool TryGetEquipmentSlot(
        EquipSlotCategory equipSlotCategory,
        int inventorySlot,
        out DrawDataContainer.EquipmentSlot slot)
    {
        if (equipSlotCategory.Head != 0)
        {
            slot = DrawDataContainer.EquipmentSlot.Head;
            return true;
        }

        if (equipSlotCategory.Body != 0)
        {
            slot = DrawDataContainer.EquipmentSlot.Body;
            return true;
        }

        if (equipSlotCategory.Gloves != 0)
        {
            slot = DrawDataContainer.EquipmentSlot.Hands;
            return true;
        }

        if (equipSlotCategory.Legs != 0)
        {
            slot = DrawDataContainer.EquipmentSlot.Legs;
            return true;
        }

        if (equipSlotCategory.Feet != 0)
        {
            slot = DrawDataContainer.EquipmentSlot.Feet;
            return true;
        }

        if (equipSlotCategory.Ears != 0)
        {
            slot = DrawDataContainer.EquipmentSlot.Ears;
            return true;
        }

        if (equipSlotCategory.Neck != 0)
        {
            slot = DrawDataContainer.EquipmentSlot.Neck;
            return true;
        }

        if (equipSlotCategory.Wrists != 0)
        {
            slot = DrawDataContainer.EquipmentSlot.Wrists;
            return true;
        }

        if (equipSlotCategory.FingerL != 0 || equipSlotCategory.FingerR != 0)
        {
            slot = inventorySlot == 12
                ? DrawDataContainer.EquipmentSlot.LFinger
                : DrawDataContainer.EquipmentSlot.RFinger;
            return true;
        }

        slot = DrawDataContainer.EquipmentSlot.Head;
        return false;
    }

    private static EquipmentModelId BuildEquipmentModel(Item item, byte stain0, byte stain1) => new()
    {
        Id = (ushort)item.ModelMain,
        Variant = (byte)(item.ModelMain >> 16),
        Stain0 = stain0,
        Stain1 = stain1
    };

    private static WeaponModelId BuildWeaponModel(Item item, byte stain0, byte stain1) => new()
    {
        Id = (ushort)item.ModelMain,
        Type = (ushort)(item.ModelMain >> 16),
        Variant = (ushort)(item.ModelMain >> 32),
        Stain0 = stain0,
        Stain1 = stain1
    };

    private enum ItemActionKind : uint
    {
        Companion = 853,
        BuddyEquip = 1013,
        Mount = 1322,
        UnlockLink = 2633,
        Ornament = 20086,
        Glasses = 37312
    }

    private readonly record struct PreviewTarget(CollectionType Type, uint ID);

    private readonly record struct CharacterModelState(
        CharacterModes Mode,
        byte ModeParam,
        float ModelScale,
        byte ModelScaleID,
        float UnscaledRadius);

    private readonly record struct CharacterActionState(CharacterModes Mode, byte ModeParam);

    private readonly record struct BardingPreviewState(
        uint EntityID,
        EquipmentModelId Head,
        EquipmentModelId Body,
        EquipmentModelId Feet);
}
