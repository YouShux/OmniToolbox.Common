using System.IO;
using System.Threading;
using MessagePack;
using MessagePack.Resolvers;

namespace OmniToolbox.Data;

internal static class GameDataPackageCodec
{
    internal static readonly MessagePackSerializerOptions Options = MessagePackSerializerOptions.Standard
        .WithResolver(CompositeResolver.Create(GameDataPackageResolver.Create(), BuiltinResolver.Instance))
        .WithSecurity(MessagePackSecurity.UntrustedData);

    internal static byte[] Serialize<T>(T value, CancellationToken cancellationToken = default) =>
        MessagePackSerializer.Serialize(value, Options, cancellationToken);

    internal static T Deserialize<T>(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (bytes.Length is 0 or > 64 * 1024 * 1024)
            throw new InvalidDataException("Invalid game data package size.");
        return MessagePackSerializer.Deserialize<T>(bytes, Options, cancellationToken)
            ?? throw new InvalidDataException("Game data package is null.");
    }
}

[GeneratedMessagePackResolver]
internal partial class GameDataPackageResolver
{
    internal static IFormatterResolver Create() => new GameDataPackageResolver();
}
