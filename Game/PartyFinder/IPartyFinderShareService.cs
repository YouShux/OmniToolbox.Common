using System.Threading;
using System.Threading.Tasks;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.UI.Info;

namespace OmniToolbox.PublicBridge;

public unsafe interface IPartyFinderShareService
{
    uint DataCenterID { get; }

    uint SnapshotDataCenterID { get; }

    long Revision { get; }

    long GeneratedAt { get; }

    int ListingCount { get; }

    bool IsConnected { get; }

    bool IsLoading { get; }

    bool IsConfigured { get; }

    IReadOnlyList<PartyFinderShareModels.Listing> GetSnapshot();

    Task RefreshSnapshotAsync
    (
        uint              requestedDataCenterID,
        CancellationToken cancellationToken
    );

    void CancelSnapshotRequest();

    void ReceiveSharedListings
    (
        InfoProxyCrossRealm*                              proxy,
        ServerIpcSegment<CrossRealmListingSegmentPacket>* packet
    );
}
