using OmenTools;
using OmenTools.Dalamud.Services.Game.Object.Abstractions;
using OmenTools.Dalamud.Services.Game.Object.Abstractions.ObjectKinds;

namespace OmniToolbox.Game;

internal static class CombatCharacterSnapshot
{
    private static readonly List<IPlayerCharacter> PlayerSnapshot = new(100);
    private static readonly List<IBattleChara> BattleCharaSnapshot = new(100);
    private static readonly Dictionary<uint, IGameObject> ObjectsByEntityID = new(100);
    private static readonly Dictionary<ulong, IGameObject> ObjectsByGameObjectID = new(100);
    public static IReadOnlyList<IPlayerCharacter> Players => PlayerSnapshot;
    public static IReadOnlyList<IBattleChara> BattleCharas => BattleCharaSnapshot;

    public static void Refresh()
    {
        Clear();

        var objectTable = DService.Instance().ObjectTable;
        var characterCount = Math.Min(objectTable.Length, IObjectTable.CharactersRange.End.Value);
        for (var index = 0; index < characterCount; index++)
        {
            if (objectTable[index] is not ICharacter character ||
                character.EntityID == 0 ||
                !character.IsValid())
            {
                continue;
            }

            if (character is IPlayerCharacter player)
            {
                PlayerSnapshot.Add(player);
            }

            if (character is IBattleChara battleChara)
            {
                BattleCharaSnapshot.Add(battleChara);
            }

            ObjectsByEntityID[character.EntityID] = character;
            if (character.GameObjectID != 0)
            {
                ObjectsByGameObjectID[character.GameObjectID] = character;
            }
        }
    }

    public static IGameObject? Find(uint id)
    {
        if (id == 0)
        {
            return null;
        }

        return ObjectsByEntityID.TryGetValue(id, out var entityObject)
            ? entityObject
            : ObjectsByGameObjectID.TryGetValue(id, out var gameObject)
                ? gameObject
                : null;
    }

    public static IGameObject? Find(ulong gameObjectID, uint entityID) =>
        gameObjectID != 0 && ObjectsByGameObjectID.TryGetValue(gameObjectID, out var gameObject)
            ? gameObject
            : Find(entityID);

    public static void Clear()
    {
        PlayerSnapshot.Clear();
        BattleCharaSnapshot.Clear();
        ObjectsByEntityID.Clear();
        ObjectsByGameObjectID.Clear();
    }
}
