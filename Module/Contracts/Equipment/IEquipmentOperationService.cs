using OmniToolbox.Operations;

namespace OmniToolbox.Equipment;

public interface IEquipmentOperationService : IOperationService
{
    bool IsBusy { get; }
}
