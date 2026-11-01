using System.Text.Json.Serialization;

namespace OmniToolbox.PublicBridge;

public static class PartyFinderShareModels
{
    public sealed record StateRequest(
        [property: JsonPropertyName("schema_version")]
        int SchemaVersion,
        [property: JsonPropertyName("data_center_id")]
        uint DataCenterID,
        [property: JsonPropertyName("outside_duty")]
        bool OutsideDuty,
        [property: JsonPropertyName("needs_updates")]
        bool NeedsUpdates,
        [property: JsonPropertyName("collector_available")]
        bool CollectorAvailable);

    public sealed record StateResult(
        [property: JsonPropertyName("schema_version")]
        int SchemaVersion,
        [property: JsonPropertyName("is_lease_holder")]
        bool IsLeaseHolder,
        [property: JsonPropertyName("refresh_after_seconds")]
        int RefreshAfterSeconds,
        [property: JsonPropertyName("lease_expires_at")]
        long LeaseExpiresAt,
        [property: JsonPropertyName("revision")]
        long Revision);

    public sealed record ObservationBatch(
        [property: JsonPropertyName("schema_version")]
        int SchemaVersion,
        [property: JsonPropertyName("data_center_id")]
        uint DataCenterID,
        [property: JsonPropertyName("scan_id")]
        string ScanID,
        [property: JsonPropertyName("observed_at")]
        long ObservedAt,
        [property: JsonPropertyName("snapshot_eligible")]
        bool SnapshotEligible,
        [property: JsonPropertyName("listings")]
        Listing[] Listings);

    public sealed record Listing(
        [property: JsonPropertyName("id")]
        string ID,
        [property: JsonPropertyName("last_server_restart")]
        int LastServerRestart,
        [property: JsonPropertyName("created_world_id")]
        uint CreatedWorldID,
        [property: JsonPropertyName("current_world_id")]
        uint CurrentWorldID,
        [property: JsonPropertyName("home_world_id")]
        uint HomeWorldID,
        [property: JsonPropertyName("category")]
        uint Category,
        [property: JsonPropertyName("duty_type")]
        uint DutyType,
        [property: JsonPropertyName("duty_id")]
        ushort DutyID,
        [property: JsonPropertyName("recruiter_name")]
        string RecruiterName,
        [property: JsonPropertyName("description")]
        string Description,
        [property: JsonPropertyName("observed_at")]
        long ObservedAt,
        [property: JsonPropertyName("seconds_remaining")]
        uint SecondsRemaining,
        [property: JsonPropertyName("minimum_item_level")]
        ushort MinimumItemLevel,
        [property: JsonPropertyName("parties")]
        byte Parties,
        [property: JsonPropertyName("slots_available")]
        byte SlotsAvailable,
        [property: JsonPropertyName("slots_filled")]
        byte SlotsFilled,
        [property: JsonPropertyName("objective")]
        byte Objective,
        [property: JsonPropertyName("beginners_welcome")]
        bool BeginnersWelcome,
        [property: JsonPropertyName("conditions")]
        byte Conditions,
        [property: JsonPropertyName("duty_finder_settings")]
        byte DutyFinderSettings,
        [property: JsonPropertyName("loot_rule")]
        byte LootRule,
        [property: JsonPropertyName("is_alliance")]
        bool IsAlliance,
        [property: JsonPropertyName("search_area")]
        uint SearchArea,
        [property: JsonPropertyName("jobs_present")]
        int[] JobsPresent,
        [property: JsonPropertyName("slot_flags")]
        string[] SlotFlags,
        [property: JsonPropertyName("leader_content_id")]
        string? LeaderContentID = null);

    public sealed record Snapshot(
        [property: JsonPropertyName("schema_version")]
        int SchemaVersion,
        [property: JsonPropertyName("revision")]
        long Revision,
        [property: JsonPropertyName("generated_at")]
        long GeneratedAt,
        [property: JsonPropertyName("listings")]
        Listing[] Listings);
}
