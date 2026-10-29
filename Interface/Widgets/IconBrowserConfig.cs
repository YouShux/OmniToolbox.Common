using System.Diagnostics;
using System.Globalization;
using Dalamud.Game.Text;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using OmenTools.OmenService;
using OmniToolbox.Config;
using OmniToolbox.Host;
using OmniToolbox.Items;
using OmniToolbox.UI;
using OmniToolbox.UI.Controls;
using OmniToolbox.UI.Theme;
using CharaMakeCustomize = Lumina.Excel.Sheets.CharaMakeCustomize;
using MapSymbol = Lumina.Excel.Sheets.MapSymbol;

namespace OmniToolbox.UI;

public sealed class IconBrowserConfig
{
    public float IconSize { get; set; } = 50f;
}
