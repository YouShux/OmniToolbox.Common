using System.Globalization;

namespace OmniToolbox.Commands;

public static class OmniCommandMigration
{
    public static string? Migrate(string command, Func<string, string?> resolveModule)
    {
        if (command.AsSpan().IndexOfAny('\r', '\n') >= 0)
            return null;

        var text = command.TrimStart();
        if (text.StartsWith('／'))
            text = "/" + text[1..];
        else if (text.StartsWith("omni", StringComparison.OrdinalIgnoreCase) &&
                 (text.Length == 4 || char.IsWhiteSpace(text[4])))
            text = "/" + text;
        if (!text.StartsWith("/omni", StringComparison.OrdinalIgnoreCase) ||
            text.Length > 5 && !char.IsWhiteSpace(text[5]))
            return command;

        var body = text[5..].TrimStart();
        var (name, arguments) = OmniCommandCatalog.Split(body);
        if (name.Length == 0)
            return "/omni";

        if (OmniCommandCatalog.Find(name) is { } definition)
        {
            return definition.Name == "chatcn"
                ? OmniCommandCatalog.Format("chatcn") + body[name.Length..]
                : OmniCommandCatalog.Format(definition.Name, arguments);
        }

        var legacyName = name switch
        {
            "回放日志" or "回放日誌" => "replay",
            "一键打开雇员铃" or "一鍵打開雇員鈴" or "虚空传唤铃" or "虛空傳喚鈴" => "retainer",
            _ => null
        };
        if (legacyName is not null)
            return OmniCommandCatalog.Format(legacyName, arguments);

        if (string.Equals(name, "BetterBattlehornSettings", StringComparison.OrdinalIgnoreCase))
            return OmniCommandCatalog.Format("battlehorn", arguments);

        if (!string.IsNullOrWhiteSpace(arguments))
        {
            if (string.Equals(name, "FastWorldTravel", StringComparison.OrdinalIgnoreCase))
                return OmniCommandCatalog.Format("travel", arguments);
            if (string.Equals(name, "AutoLogin", StringComparison.OrdinalIgnoreCase))
                return OmniCommandCatalog.Format("login", arguments);
            if (string.Equals(name, "BetterNotebooks", StringComparison.OrdinalIgnoreCase))
                return OmniCommandCatalog.Format("battlehorn", arguments);
            if (string.Equals(name, "AutoIshgardRestoration", StringComparison.OrdinalIgnoreCase) &&
                (string.Equals(arguments.Trim(), "open", StringComparison.OrdinalIgnoreCase) || arguments.Trim() == "窗口"))
                return OmniCommandCatalog.Format("ishgard", arguments);
        }

        if (string.IsNullOrWhiteSpace(arguments))
        {
            if (name is "南方博兹雅战线" or "南方博茲雅戰線")
                return OmniCommandCatalog.Format("scenario", "920");
            if (name is "扎杜诺尔高原" or "扎杜諾爾高原")
                return OmniCommandCatalog.Format("scenario", "975");

            if ((name.StartsWith("部队特效", StringComparison.Ordinal) ||
                 name.StartsWith("部隊特效", StringComparison.Ordinal)) &&
                uint.TryParse(name.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var actionID) &&
                actionID > 0)
                return OmniCommandCatalog.Format("fcaction", actionID.ToString(CultureInfo.InvariantCulture));

            return resolveModule(name) is { } module
                ? OmniCommandCatalog.Format("toggle", module)
                : null;
        }

        return uint.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out var itemID) && itemID > 0
            ? OmniCommandCatalog.Format("buy", body)
            : null;
    }
}
