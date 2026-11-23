namespace OmniToolbox.Equipment;

public interface IMateriaExtractionService : IEquipmentOperationService
{
    bool TryStart(string scope, out string operationID, out string error);
}
