using OmenTools;
using OmenTools.Dalamud.Services.Game.Object.Abstractions;
using OmenTools.Dalamud.Services.Game.Object.Abstractions.ObjectKinds;

namespace OmniToolbox.Game;

internal static class CombatCharacterSnapshot
{
    private static readonly List<IPlayerCharacter> players = new(100);
    private static readonly List<IBattleChara> battleCharas = new(100);
    private static readonly Dictionary<uint, IGameObject> objectsByEntityID = new(100);
    private static readonly Dictionary<ulong, IGameObject> objectsByGameObjectID = new(100);
    public static IReadOnlyList<IPlayerCharacter> Players => players;
    public static IReadOnlyList<IBattleChara> BattleCharas => battleCharas;

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
                players.Add(player);
            }

            if (character is IBattleChara battleChara)
            {
                battleCharas.Add(battleChara);
            }

            objectsByEntityID[character.EntityID] = character;
            if (character.GameObjectID != 0)
            {
                objectsByGameObjectID[character.GameObjectID] = character;
            }
        }
    }

    public static IGameObject? Find(uint id)
    {
        if (id == 0)
        {
            return null;
        }

        return objectsByEntityID.TryGetValue(id, out var entityObject)
            ? entityObject
            : objectsByGameObjectID.TryGetValue(id, out var gameObject)
                ? gameObject
                : null;
    }

    public static IGameObject? Find(ulong gameObjectID, uint entityID) =>
        gameObjectID != 0 && objectsByGameObjectID.TryGetValue(gameObjectID, out var gameObject)
            ? gameObject
            : Find(entityID);

    public static void Clear()
    {
        players.Clear();
        battleCharas.Clear();
        objectsByEntityID.Clear();
        objectsByGameObjectID.Clear();
    }
}
