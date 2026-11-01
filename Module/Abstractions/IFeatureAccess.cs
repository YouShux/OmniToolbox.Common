namespace OmniToolbox.Common.Module.Abstractions;

public interface IFeatureAccess
{
    bool CanUsePrivateFeature(out string reason);
}
