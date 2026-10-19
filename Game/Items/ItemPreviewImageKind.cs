using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Component.Exd;
using Lumina.Excel.Sheets;
using OmenTools.Info.Game.ItemSource;
using OmenTools.Info.Game.ItemSource.Enums;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Collections;
using OmniToolbox.Config;
using Character = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace OmniToolbox.Items;

internal enum ItemPreviewImageKind
{
    Painting,
    Mount,
    Minion,
    Hairstyle,
    FashionAccessory
}
