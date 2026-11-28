using OmniToolbox.Operations;

namespace OmniToolbox.Equipment;

public interface IEquipmentOperationService : IOperationService
{
    bool IsBusy { get; }
}

public interface IAutoRepairService : IEquipmentOperationService
{
    bool TryStart(int thresholdPercent, string mode, out string operationID, out string error);
}

public interface IMateriaExtractionService : IEquipmentOperationService
{
    bool TryStart(string scope, out string operationID, out string error);
}

public interface IRecommendedGearService : IEquipmentOperationService
{
    bool TryStart(out string operationID, out string error);
}
