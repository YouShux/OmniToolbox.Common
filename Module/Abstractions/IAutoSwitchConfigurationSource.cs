using System.Threading;
using System.Threading.Tasks;
using OmniToolbox.Common.Module.Models;

namespace OmniToolbox.Common.Module.Abstractions;

public interface IAutoSwitchConfigurationSource
{
    IReadOnlyList<PluginCollectionOption> GetPluginCollections();

    IReadOnlyList<ToolbarCommandOption> GetToolbarCommands();

    Task<bool> SetPluginCollectionEnabledAsync(Guid profileID, bool enabled, CancellationToken cancellationToken);
}
