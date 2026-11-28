using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Newtonsoft.Json;

namespace OmniToolbox.IO;

public static class OmniJson
{
    public static JsonSerializerOptions SystemTextOptions { get; } = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    public static JsonSerializerOptions IgnoreNullOptions { get; } = new()
    {
        TypeInfoResolver       = new DefaultJsonTypeInfoResolver(),
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonSerializerOptions SnakeCaseOptions { get; } = new()
    {
        TypeInfoResolver     = new DefaultJsonTypeInfoResolver(),
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static JsonSerializerSettings SerializerSettings { get; } = new()
    {
        Formatting = Formatting.Indented,
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };
}
