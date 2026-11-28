using OmniToolbox.Common.Module.Abstractions;
using OmniToolbox.Common.Module.Cloud;
using OmniToolbox.PublicBridge;

namespace OmniToolbox.Verification;

public class FeatureGate : ITestFeatureAccess
{
    public virtual bool CanUsePrivateFeature(out string reason)
    {
        reason = "Status.PrivateProviderUnavailable";
        return false;
    }

    public bool CanUseTestFeature(out string reason) =>
        CanUseTestFeature(TestQualificationLevel.Level1, out reason);

    public bool CanUseTestFeature(int requiredLevel, out string reason) =>
        CanUseTestFeature((TestQualificationLevel)requiredLevel, out reason);

    public virtual bool CanUseTestFeature
    (
        TestQualificationLevel requiredLevel,
        out string             reason
    )
    {
        reason = "Status.PrivateProviderUnavailable";
        return false;
    }
}
