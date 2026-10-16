namespace OmniToolbox.Commands;

public sealed record OmniCommandDefinition(
    string Name,
    string ChineseName,
    string Parameters,
    string Description,
    string? ModuleName,
    OmniCommandArguments Arguments)
{
    public string NameKey => $"Command.{Name}.Name";

    public string DescriptionKey => $"Command.{Name}.Description";

    public string UsageKey => $"Command.{Name}.Usage";
}
