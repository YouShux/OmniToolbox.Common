using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace OmniToolbox.Data;

internal sealed class GameDataSnapshot : IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<string, List<Input>> inputs = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Input> selected = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Input> embedded = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (Dictionary<string, string> Files, string Hash)> bundles = new(StringComparer.Ordinal);
    private readonly HashSet<Input> validated = [];
    private readonly HashSet<(Input CSV, Input Metadata)> validatedEquipment = [];
    private bool disposed;

    internal Input GetInput(string name, string content)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!inputs.TryGetValue(name, out var versions))
                inputs.Add(name, versions = []);
            var existing = versions.FirstOrDefault(input => string.Equals(input.Content, content, StringComparison.Ordinal));
            if (existing is not null)
                return existing;
            var input = new Input(name, content);
            versions.Add(input);
            return input;
        }
    }

    internal Input GetSelected(string name)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (selected.TryGetValue(name, out var input))
                return input;
            input = ReadEmbedded(name);
            selected.Add(name, input);
            return input;
        }
    }

    internal Input ReadEmbedded(string name)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (embedded.TryGetValue(name, out var input))
                return input;
            using var stream = typeof(EmbeddedData).Assembly.GetManifestResourceStream("OmniToolbox.Data." + name)
                ?? throw new InvalidDataException($"Embedded data resource is missing: {name}");
            using var reader = new StreamReader(stream);
            input = GetInput(name, reader.ReadToEnd());
            input.IsEmbedded = true;
            StartupTiming.Count(name, "read", bytes: input.Bytes.Length);
            embedded.Add(name, input);
            return input;
        }
    }

    internal bool TryGetBundle(string group, out (Dictionary<string, string> Files, string Hash) bundle)
    {
        lock (sync)
            return bundles.TryGetValue(group, out bundle);
    }

    internal void SetBundle(string group, Dictionary<string, string> files, string hash)
    {
        lock (sync)
            bundles.Add(group, (files, hash));
    }

    internal void Select(IReadOnlyDictionary<string, byte[]> overrides)
    {
        lock (sync)
        {
            foreach (var (name, input) in embedded)
                selected[name] = input;
            foreach (var (name, bytes) in overrides)
                selected[name] = inputs[name].Single(input => ReferenceEquals(input.Bytes, bytes));
        }
    }

    internal bool IsValidated(Input input)
    {
        lock (sync)
            return validated.Contains(input);
    }

    internal void MarkValidated(Input input)
    {
        lock (sync)
            validated.Add(input);
    }

    internal bool HasEquipmentValidation(Input csv, Input metadata)
    {
        lock (sync)
            return validatedEquipment.Contains((csv, metadata));
    }

    internal void MarkEquipmentValidated(Input csv, Input metadata)
    {
        lock (sync)
            validatedEquipment.Add((csv, metadata));
    }

    internal void ReleaseDocuments()
    {
        lock (sync)
            foreach (var input in inputs.Values.SelectMany(versions => versions))
                input.ReleaseDocument();
    }

    internal void CompleteStartup()
    {
        lock (sync)
        {
            ReleaseDocuments();
            foreach (var name in new[] { "ItemPatch.csv", "ItemSupplement.csv", "StoreItem.csv", "BlueSpells.csv",
                         "RelicItemIdToAchievement.csv", "EquipmentSourceSupplement.csv", "RelicMaterialSources.csv" })
                if (inputs.TryGetValue(name, out var versions))
                    foreach (var input in versions)
                        input.ReleaseCSV();
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed)
                return;
            ReleaseDocuments();
            disposed = true;
            inputs.Clear();
            selected.Clear();
            embedded.Clear();
            bundles.Clear();
            validated.Clear();
            validatedEquipment.Clear();
        }
    }

    internal sealed class Input(string name, string content)
    {
        private readonly object sync = new();
        private JsonDocument? document;
        private GameDataCSV? csv;
        private string[]? header;
        private DateTimeOffset? generatedAtUTC;
        private int rowCount;

        internal string Content { get; } = content;
        internal bool IsEmbedded { get; set; }
        internal byte[] Bytes { get; } = Encoding.UTF8.GetBytes(content);
        internal int RowCount => rowCount;

        internal DateTimeOffset GeneratedAtUTC
        {
            get
            {
                lock (sync)
                {
                    if (generatedAtUTC is { } captured)
                        return captured;
                    StartupTiming.Count(name, "metadata-read");
                    var reader = new Utf8JsonReader(Bytes);
                    while (reader.Read())
                    {
                        if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1 ||
                            !reader.ValueTextEquals("generatedAtUtc") || !reader.Read())
                            continue;
                        var value = reader.GetDateTimeOffset();
                        generatedAtUTC = value;
                        return value;
                    }
                    throw new InvalidDataException($"Missing snapshot timestamp: {name}");
                }
            }
        }

        internal JsonDocument Document
        {
            get
            {
                lock (sync)
                {
                    if (document is null)
                    {
                        StartupTiming.Count(name, "parse");
                        document = JsonDocument.Parse(Bytes);
                    }
                    return document;
                }
            }
        }

        internal GameDataCSV GetCSV(CancellationToken cancellationToken)
        {
            lock (sync)
            {
                if (csv is null)
                {
                    StartupTiming.Count(name, "parse");
                    csv = GameDataCSV.Parse(Content, cancellationToken);
                    rowCount = csv.Rows.Count;
                    header = csv.Header;
                    StartupTiming.Count(name, "rows", csv.Rows.Count);
                }
                return csv;
            }
        }

        internal string[] Header
        {
            get
            {
                lock (sync)
                {
                    if (header is null)
                    {
                        StartupTiming.Count(name, "header-parse");
                        header = GameDataCSV.ReadHeader(Content);
                    }
                    return header;
                }
            }
        }

        internal void ReleaseDocument()
        {
            lock (sync)
            {
                document?.Dispose();
                document = null;
            }
        }

        internal void ReleaseCSV()
        {
            lock (sync)
                csv = null;
        }
    }
}
