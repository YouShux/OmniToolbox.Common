using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Data;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static void AddFishingSources(IDictionary<uint, HashSet<CollectionSource>> target)
    {
        using var stream = EmbeddedData.Open("FishingSources.json");
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetInt32() != 1)
        {
            throw new InvalidDataException("Unsupported FishingSources schema version.");
        }

        var placeNameIds = root.GetProperty("spots")
            .EnumerateArray()
            .ToDictionary(
                spot => spot.GetProperty("i").GetUInt32(),
                spot => spot.GetProperty("p").GetUInt32());
        foreach (var item in root.GetProperty("items").EnumerateArray())
        {
            var itemID = item.GetProperty("i").GetUInt32();
            var detailSuffix = string.Format(
                CultureInfo.CurrentCulture,
                OmniLoc.Get("Collection.Source.FishingDetail"),
                item.TryGetProperty("f", out _) ? OmniLoc.Get("Common.Required") : OmniLoc.Get("Common.NotRequired"),
                GetFishingTime(item),
                GetFishingWeather(item));
            foreach (var spotIdElement in item.GetProperty("s").EnumerateArray())
            {
                var spotID = spotIdElement.GetUInt32();
                AddSource(target, itemID, new(
                    CollectionSourceCategory.Other,
                    spotID,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        OmniLoc.Get("Collection.Source.Fishing"),
                        LuminaWrapper.GetPlaceName(placeNameIds[spotID]),
                        detailSuffix),
                    string.Empty));
            }
        }
    }

    private static string GetFishingTime(JsonElement item)
    {
        var startHour = item.TryGetProperty("a", out var start) ? start.GetSingle() : 0f;
        var endHour = item.TryGetProperty("b", out var end) ? end.GetSingle() : 24f;
        return startHour == 0f && endHour == 24f
            ? OmniLoc.Get("Common.None")
            : string.Format(
                CultureInfo.CurrentCulture,
                OmniLoc.Get("Collection.Source.FishingTime"),
                FormatFishingHour(startHour),
                FormatFishingHour(endHour));
    }

    private static string GetFishingWeather(JsonElement item)
    {
        var weather = GetWeatherNames(item, "w");
        var previousWeather = GetWeatherNames(item, "v");
        if (previousWeather.Length != 0)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                OmniLoc.Get("Collection.Source.FishingWeatherTransition"),
                previousWeather,
                weather);
        }

        return weather.Length == 0 ? OmniLoc.Get("Common.None") : weather;
    }

    private static string GetWeatherNames(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var weatherIds)
            ? string.Join(
                OmniLoc.Get("Common.ListSeparator"),
                weatherIds.EnumerateArray().Select(weather => LuminaWrapper.GetWeatherName(weather.GetUInt32())))
            : string.Empty;

    private static string FormatFishingHour(float hour)
    {
        var minutes = (int)MathF.Round(hour * 60f);
        return $"{minutes / 60}:{minutes % 60:00}";
    }
}
