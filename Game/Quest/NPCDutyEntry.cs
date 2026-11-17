using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using OmenTools;
using OmenTools.Extensions;
using OmenTools.Interop.Game.AddonEvent;

namespace OmniToolbox.Quests;

public static unsafe class NPCDutyEntry
{
    /// <summary>仅推进指定 NPC 入区事件；确认提交后仍需调用方核实目标区域。</summary>
    public static NPCDutyEntryProgress Advance(uint eventID, ulong actorID, string dutyName)
    {
        var services = DService.Instance();
        var framework = EventFramework.Instance();
        if (actorID == 0 || string.IsNullOrWhiteSpace(dutyName) || framework == null ||
            services.Condition.IsBetweenAreas || !UIModule.IsScreenReady() ||
            !services.Condition.IsOccupiedInEvent ||
            !(framework->EventState1.EventId.Id == eventID && framework->EventState1.ObjectId == actorID ||
              framework->EventState2.EventId.Id == eventID && framework->EventState2.ObjectId == actorID))
            return NPCDutyEntryProgress.Waiting;

        var confirm = services.GameGUI.GetAddonByName<AddonSelectYesno>("SelectYesno");
        if (confirm != null && ((AtkUnitBase*)confirm)->IsAddonAndNodesReady())
        {
            if (confirm->PromptText == null ||
                !confirm->PromptText->NodeText.ToString().Contains(dutyName, StringComparison.OrdinalIgnoreCase))
                return NPCDutyEntryProgress.MenuMismatch;
            if (confirm->YesButton == null || !confirm->YesButton->IsEnabled)
                return NPCDutyEntryProgress.Waiting;
            return AddonSelectYesnoEvent.ClickYes(dutyName)
                ? NPCDutyEntryProgress.Confirmed : NPCDutyEntryProgress.Waiting;
        }

        var menu = services.GameGUI.GetAddonByName<AddonSelectString>("SelectString");
        if (menu != null && ((AtkUnitBase*)menu)->IsAddonAndNodesReady())
            return AddonSelectStringEvent.Select(dutyName)
                ? NPCDutyEntryProgress.Advanced : NPCDutyEntryProgress.MenuMismatch;
        var iconMenu = services.GameGUI.GetAddonByName<AddonSelectIconString>("SelectIconString");
        if (iconMenu != null && ((AtkUnitBase*)iconMenu)->IsAddonAndNodesReady())
            return AddonSelectIconStringEvent.Select(dutyName)
                ? NPCDutyEntryProgress.Advanced : NPCDutyEntryProgress.MenuMismatch;
        var talk = services.GameGUI.GetAddonByName<AddonTalk>("Talk");
        return talk != null && ((AtkUnitBase*)talk)->IsAddonAndNodesReady() && AddonTalkEvent.ClickNext()
            ? NPCDutyEntryProgress.Advanced : NPCDutyEntryProgress.Waiting;
    }
}

public enum NPCDutyEntryProgress { Waiting, Advanced, Confirmed, MenuMismatch }
