using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike
{
    public enum HubObjectType
    {
        Campfire,
        Character,
        Stage
    }

    public enum HubMoveResult
    {
        Blocked,
        Moved,
        Previewed,
        CharacterChanged,
        StageSelected,
        StageLocked
    }

    public readonly struct HubObjectData
    {
        public HubObjectData(HubObjectType type, int index)
        {
            Type = type;
            Index = index;
        }

        public HubObjectType Type { get; }
        public int Index { get; }
    }

    public sealed class HubWorldModel
    {
        public const int Size = TrailFieldModel.Size;

        private readonly Dictionary<Vector2Int, HubObjectData> objects =
            new Dictionary<Vector2Int, HubObjectData>();

        public Vector2Int Player { get; private set; }
        public int CurrentCharacter { get; private set; }
        public Vector2Int? FocusedCell { get; private set; }
        public IReadOnlyDictionary<Vector2Int, HubObjectData> Objects => objects;

        public void Reset(int currentCharacter = 0)
        {
            CurrentCharacter = Mathf.Clamp(currentCharacter, 0, 3);
            Player = new Vector2Int(1, 1);
            FocusedCell = null;
            objects.Clear();

            objects[new Vector2Int(2, 2)] = new HubObjectData(HubObjectType.Campfire, 0);
            int characterIndex = 1;
            foreach (Vector2Int cell in new[]
                     {
                         new Vector2Int(1, 2),
                         new Vector2Int(3, 2),
                         new Vector2Int(2, 3)
                     })
            {
                while (characterIndex == CurrentCharacter)
                {
                    characterIndex++;
                }
                objects[cell] = new HubObjectData(HubObjectType.Character, characterIndex % 4);
                characterIndex++;
            }

            int[] stageXs = { 1, 3, 5, 7, 9 };
            for (int i = 0; i < stageXs.Length; i++)
            {
                objects[new Vector2Int(stageXs[i], 9)] = new HubObjectData(HubObjectType.Stage, i);
            }
        }

        public HubMoveResult TryMove(Vector2Int direction, out HubObjectData interactedObject)
        {
            interactedObject = default;
            if (Mathf.Abs(direction.x) + Mathf.Abs(direction.y) != 1)
            {
                return HubMoveResult.Blocked;
            }

            Vector2Int target = Player + direction;
            if (!IsInside(target))
            {
                return HubMoveResult.Blocked;
            }

            if (objects.TryGetValue(target, out HubObjectData targetObject))
            {
                interactedObject = targetObject;
                if (!FocusedCell.HasValue || FocusedCell.Value != target)
                {
                    FocusedCell = target;
                    return HubMoveResult.Previewed;
                }

                if (targetObject.Type == HubObjectType.Character)
                {
                    Vector2Int landing = target + direction;
                    if (!IsInside(landing) || objects.ContainsKey(landing))
                    {
                        return HubMoveResult.Blocked;
                    }

                    int previousCharacter = CurrentCharacter;
                    CurrentCharacter = targetObject.Index;
                    objects[target] = new HubObjectData(HubObjectType.Character, previousCharacter);
                    Player = landing;
                    FocusedCell = null;
                    return HubMoveResult.CharacterChanged;
                }

                if (targetObject.Type == HubObjectType.Stage)
                {
                    return targetObject.Index == 0
                        ? HubMoveResult.StageSelected
                        : HubMoveResult.StageLocked;
                }

                return HubMoveResult.Previewed;
            }

            Player = target;
            ClearFocusWhenFar();
            return HubMoveResult.Moved;
        }

        public bool TryGetFocusedObject(out Vector2Int cell, out HubObjectData data)
        {
            if (FocusedCell.HasValue && objects.TryGetValue(FocusedCell.Value, out data))
            {
                cell = FocusedCell.Value;
                return true;
            }

            cell = default;
            data = default;
            return false;
        }

        public static bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Size && cell.y >= 0 && cell.y < Size;
        }

        private void ClearFocusWhenFar()
        {
            if (!FocusedCell.HasValue)
            {
                return;
            }

            Vector2Int delta = Player - FocusedCell.Value;
            if (Mathf.Abs(delta.x) + Mathf.Abs(delta.y) >= 2)
            {
                FocusedCell = null;
            }
        }
    }
}
