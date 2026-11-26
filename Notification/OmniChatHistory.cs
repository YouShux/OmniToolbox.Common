using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace OmniToolbox.Notifications;

internal static class OmniChatHistory
{
    public static event Action<IReadOnlyList<int>>? MessagesChanged;

    internal static unsafe void ReplaceLinkText(DalamudLinkPayload link, byte[] replacement)
    {
        var log = RaptureLogModule.Instance();
        if (log is null)
            return;

        HashSet<int> changed = [];
        List<byte[]> originals = [];
        var first = Math.Max(0, log->GetLogMessageOverflow());
        var count = log->LogMessageCount;
        for (var messageIndex = first; messageIndex < count; messageIndex++)
        {
            if (!log->GetLogMessageDetail(messageIndex, out _, out var message, out _, out _, out _, out _))
                continue;

            var payloads = SeString.Parse(message).Payloads;
            for (var index = 0; index < payloads.Count; index++)
            {
                var length = GetLinkLength(payloads, index, link);
                if (length == 0)
                    continue;

                var original = new SeString(payloads.GetRange(index, length)).Encode();
                if (!originals.Any(value => value.AsSpan().SequenceEqual(original)))
                    originals.Add(original);
                changed.Add(messageIndex);
                index += length - 1;
            }
        }

        var replaced = false;
        foreach (var original in originals)
        {
            var offset = 0;
            while (offset < log->LogMessageData.Count)
            {
                var match = log->LogMessageData.AsSpan(offset).IndexOf(original);
                if (match < 0)
                    break;
                var start = offset + match;
                var delta = replacement.Length - original.Length;
                log->LogMessageData.EnsureCapacity(log->LogMessageData.LongCount + Math.Max(0, delta));
                log->LogMessageData.RemoveRange(start, original.Length);
                log->LogMessageData.InsertSpanCopy(start, replacement);
                // 同步所有受替换影响的字节偏移，不假设索引对应记录的开头。
                for (var index = 0; index < log->LogMessageIndex.Count; index++)
                    if (log->LogMessageIndex[index] > start)
                        log->LogMessageIndex[index] += delta;
                offset = start + replacement.Length;
                replaced = true;
            }
        }
        RefreshVisibleText(link, replacement);
        if (replaced)
        {
            log->ChatTabIsPendingReload.Fill(true);
            MessagesChanged?.Invoke(changed.ToArray());
        }
    }

    private static unsafe void RefreshVisibleText(DalamudLinkPayload link, byte[] replacement)
    {
        var stage = AtkStage.Instance();
        var strings = stage is null ? null : stage->GetStringArrayData(StringArrayType.ChatLog);
        if (strings is null || strings->StringArray is null)
            return;

        var replacementPayloads = SeString.Parse(replacement).Payloads;
        // ChatLogStringArray 中四个页签的 VisibleChatText 槽位。
        ReadOnlySpan<int> visibleTextIndices = [1, 3, 5, 7];
        var refreshed = false;
        foreach (var slot in visibleTextIndices)
        {
            if (slot >= strings->Size || !strings->StringArray[slot].HasValue)
                continue;
            var text = SeString.Parse(strings->StringArray[slot].AsSpan());
            var updated = false;
            for (var index = 0; index < text.Payloads.Count; index++)
            {
                var length = GetLinkLength(text.Payloads, index, link);
                if (length == 0)
                    continue;
                text.Payloads.RemoveRange(index, length);
                text.Payloads.InsertRange(index, replacementPayloads);
                index += replacementPayloads.Count - 1;
                updated = true;
            }
            if (!updated)
                continue;
            var encoded = text.EncodeWithNullTerminator();
            fixed (byte* pointer = encoded)
                strings->SetValue(slot, pointer, managed: true, suppressUpdates: false);
            refreshed = true;
        }
        if (refreshed)
            strings->UpdateState = 2;
    }

    private static int GetLinkLength(List<Payload> payloads, int index, DalamudLinkPayload link)
    {
        if (payloads[index] is not DalamudLinkPayload candidate ||
            candidate.CommandId != link.CommandId ||
            !string.Equals(candidate.Plugin, link.Plugin, StringComparison.Ordinal))
            return 0;

        var terminator = RawPayload.LinkTerminator;
        var end = index + 1;
        while (end < payloads.Count && !payloads[end].Equals(terminator))
            end++;
        return end < payloads.Count ? end - index + 1 : 0;
    }
}
