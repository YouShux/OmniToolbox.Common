namespace OmniToolbox.Equipment;

public interface IAutoRepairService : IEquipmentOperationService
{
    bool TryStart(int thresholdPercent, string mode, out string operationID, out string error);
}
