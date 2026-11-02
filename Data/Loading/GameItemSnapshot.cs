using System.Collections.Frozen;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Lumina.Excel.Sheets;
using Lumina.Text.ReadOnly;
using OmenTools.Interop.Game.Lumina;
using OmniToolbox.Data;
using TreeTools.Threading;

namespace OmniToolbox.Items;

internal sealed class GameItemSnapshot
{
    private readonly SemaphoreSlim preparationLock = new(1, 1);
    private FrozenDictionary<uint, ItemName> names = FrozenDictionary<uint, ItemName>.Empty;
    private FrozenDictionary<uint, string> descriptions = FrozenDictionary<uint, string>.Empty;

    private GameItemSnapshot(Item[] rows, CancellationToken cancellationToken)
    {
        Rows = rows;
        ItemsByID = new(rows.Length);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row.RowId != 0)
                ItemsByID.Add(row.RowId, row);
        }
    }

    internal Item[] Rows { get; }

    internal Dictionary<uint, Item> ItemsByID { get; }

    internal static Task<GameItemSnapshot> CreateAsync(CancellationToken cancellationToken) =>
        Task.Run(() => new GameItemSnapshot(LuminaGetter.Get<Item>().ToArray(), cancellationToken), cancellationToken);

    internal Task PrepareNamesAsync(IReadOnlyList<uint> itemIDs, CancellationToken cancellationToken) =>
        PrepareTextAsync(itemIDs, false, cancellationToken);

    internal Task PrepareDescriptionsAsync(IReadOnlyList<uint> itemIDs, CancellationToken cancellationToken) =>
        PrepareTextAsync(itemIDs, true, cancellationToken);

    internal string GetName(Item row) => Volatile.Read(ref names)[row.RowId].Text;

    internal bool HasName(uint itemID) => itemID != 0 && !Volatile.Read(ref names)[itemID].IsEmpty;

    internal string GetDescription(Item row) => Volatile.Read(ref descriptions)[row.RowId];

    private async Task PrepareTextAsync(IReadOnlyList<uint> itemIDs, bool description,
        CancellationToken cancellationToken)
    {
        await preparationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var rows = await Task.Run(() => itemIDs.Distinct()
                .Where(id => !(description ? descriptions.ContainsKey(id) : names.ContainsKey(id)) && ItemsByID.ContainsKey(id))
                .Select(id => ItemsByID[id]).ToArray(), cancellationToken).ConfigureAwait(false);
            if (rows.Length == 0)
                return;

            var captured = new (uint ID, ReadOnlySeString Text)[rows.Length];
            var index = 0;
            // 字符串 getter 可能解析 RSV，只在框架线程读取；后台仅解码已取得的只读值。
            var captureStarted = Stopwatch.GetTimestamp();
            var captureWork = 0L;
            try
            {
                var captureTask = () => FrameworkWorkScheduler.RunAsync(rows, row =>
                {
                    var started = Stopwatch.GetTimestamp();
                    try
                    {
                        captured[index++] = (row.RowId, description ? row.Description : row.Name);
                    }
                    finally
                    {
                        Interlocked.Add(ref captureWork, Stopwatch.GetTimestamp() - started);
                    }
                }, cancellationToken);
                if (StartupTiming.Current is { } timing)
                    await timing.RunAsync(description ? "item-description-capture" : "item-name-capture", captureTask).ConfigureAwait(false);
                else
                    await captureTask().ConfigureAwait(false);
            }
            finally
            {
                var elapsed = Stopwatch.GetTimestamp() - captureStarted;
                var resource = description ? "item-description" : "item-name";
                StartupTiming.Count(resource, "processing-us", (long)(captureWork * 1_000_000d / Stopwatch.Frequency));
                StartupTiming.Count(resource, "waiting-us", (long)(Math.Max(0, elapsed - captureWork) * 1_000_000d / Stopwatch.Frequency));
                StartupTiming.Count(resource, "captures", index);
            }
            await Task.Run(() =>
            {
                if (description)
                {
                    var prepared = descriptions.ToDictionary();
                    foreach (var entry in captured)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        prepared.Add(entry.ID, entry.Text.ExtractText());
                    }
                    var frozen = prepared.ToFrozenDictionary();
                    cancellationToken.ThrowIfCancellationRequested();
                    Volatile.Write(ref descriptions, frozen);
                }
                else
                {
                    var prepared = names.ToDictionary();
                    foreach (var entry in captured)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        prepared.Add(entry.ID, new(entry.Text.ExtractText(), entry.Text.IsEmpty));
                    }
                    var frozen = prepared.ToFrozenDictionary();
                    cancellationToken.ThrowIfCancellationRequested();
                    Volatile.Write(ref names, frozen);
                }
            }, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            preparationLock.Release();
        }
    }

    private readonly record struct ItemName(string Text, bool IsEmpty);
}
