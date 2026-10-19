using Newtonsoft.Json;

namespace OmniToolbox.IO;

public static class OmniJson
{
    public static JsonSerializerSettings SerializerSettings { get; } = new()
    {
        Formatting = Formatting.Indented,
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };
}
