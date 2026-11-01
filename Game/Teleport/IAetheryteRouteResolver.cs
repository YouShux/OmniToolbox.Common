using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;

namespace OmniToolbox.Teleport;

public interface IAetheryteRouteResolver
{
    IReadOnlyList<AetheryteDestination> Destinations { get; }

    bool TryResolve
    (
        MapLinkPayload              mapLink,
        IReadOnlySet<uint>          ignoredAetheryteIds,
        out AetheryteRouteSegment[] route
    );

    void AppendRoutePayloads
    (
        List<Payload>                       payloads,
        IReadOnlyList<AetheryteRouteSegment> route,
        DalamudLinkPayload                  aetheryteLinkPayload
    );
}
