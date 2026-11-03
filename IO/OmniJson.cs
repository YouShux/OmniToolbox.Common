using Newtonsoft.Json;

namespace OmniToolbox.IO;

public static class OmniJson
{
    public static System.Text.Json.JsonSerializerOptions SystemTextOptions { get; } = new()
    {
        TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver()
    };

    public static JsonSerializerSettings SerializerSettings { get; } = new()
    {
        Formatting = Formatting.Indented,
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };
}
