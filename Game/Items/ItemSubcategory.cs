using System.Collections.Frozen;
using System.Linq;
using Lumina.Excel.Sheets;
using OmniToolbox.Host;

namespace OmniToolbox.Items;

public readonly record struct ItemSubcategory(string Label, ItemCategory Category, uint ItemUICategoryID, uint JobID);
