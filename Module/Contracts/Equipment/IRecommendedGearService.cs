namespace OmniToolbox.Equipment;

public interface IRecommendedGearService : IEquipmentOperationService
{
    bool TryStart(out string operationID, out string error);
}
