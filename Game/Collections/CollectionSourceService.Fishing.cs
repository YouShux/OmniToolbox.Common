using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.UI;

namespace OmniToolbox.Collections;

public sealed partial class CollectionSourceService
{
    private static void AddFishingSources
    (
        IDictionary<uint, HashSet<CollectionSource>> target,
        JsonDocument?                                preparedDocument = null,
        PreparedSourceText?                          preparedText = null,
        CancellationToken                            cancellationToken = default
    )
    {
        using var document = preparedDocument is null ? ReadDocument("FishingSources.json") : null;
        var root = (preparedDocument ?? document ?? throw new InvalidOperationException()).RootElement;
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
            cancellationToken.ThrowIfCancellationRequested();
            var itemID = item.GetProperty("i").GetUInt32();
            var detailSuffix = string.Format(
                CultureInfo.CurrentCulture,
                OmniLoc.Get("Collection.Source.FishingDetail"),
                item.TryGetProperty("f", out _) ? OmniLoc.Get("Common.Required") : OmniLoc.Get("Common.NotRequired"),
                GetFishingTime(item),
                GetFishingWeather(item, preparedText?.WeatherNames));
            foreach (var spotIdElement in item.GetProperty("s").EnumerateArray())
            {
                var spotID = spotIdElement.GetUInt32();
                AddSource(target, itemID, new(
                    CollectionSourceCategory.Other,
                    spotID,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        OmniLoc.Get("Collection.Source.Fishing"),
                        preparedText is null
                            ? LuminaWrapper.GetPlaceName(placeNameIds[spotID])
                            : preparedText.PlaceNames.GetValueOrDefault(placeNameIds[spotID], string.Empty),
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

    private static string GetFishingWeather
    (
        JsonElement                        item,
        IReadOnlyDictionary<uint, string>? preparedNames
    )
    {
        var weather = GetWeatherNames(item, "w", preparedNames);
        var previousWeather = GetWeatherNames(item, "v", preparedNames);
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

    private static string GetWeatherNames
    (
        JsonElement                        item,
        string                             propertyName,
        IReadOnlyDictionary<uint, string>? preparedNames
    ) =>
        item.TryGetProperty(propertyName, out var weatherIds)
            ? string.Join(
                OmniLoc.Get("Common.ListSeparator"),
                weatherIds.EnumerateArray().Select(weather => preparedNames is null
                    ? LuminaWrapper.GetWeatherName(weather.GetUInt32())
                    : preparedNames.GetValueOrDefault(weather.GetUInt32(), string.Empty)))
            : string.Empty;

    private static string FormatFishingHour(float hour)
    {
        var minutes = (int)MathF.Round(hour * 60f);
        return $"{minutes / 60}:{minutes % 60:00}";
    }
}
