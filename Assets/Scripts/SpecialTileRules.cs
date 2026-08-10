using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike
{
    public enum SpecialTileType
    {
        Power,
        Amplify,
        Mud,
        Curse,
        Spike
    }

    public static class SpecialTileRules
    {
        public const int PowerFlatBonus = 25;
        public const float AmplifyMultiplier = 1.35f;
        public const float CurseMultiplier = 0.65f;
        public const float MudLockSeconds = 1f;
        private static readonly SpecialTileType[] TileSet =
        {
            SpecialTileType.Power,
            SpecialTileType.Amplify,
            SpecialTileType.Mud,
            SpecialTileType.Curse
        };

        public static Dictionary<Vector2Int, SpecialTileType> Generate(
            IReadOnlyCollection<Vector2Int> walkable,
            IReadOnlyCollection<Vector2Int> excluded,
            int seed)
        {
            var excludedSet = excluded as HashSet<Vector2Int> ?? new HashSet<Vector2Int>(excluded);
            var candidates = new List<Vector2Int>();
            foreach (Vector2Int cell in walkable)
            {
                if (!excludedSet.Contains(cell))
                {
                    candidates.Add(cell);
                }
            }
            candidates.Sort((a, b) => a.y == b.y ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));

            var result = new Dictionary<Vector2Int, SpecialTileType>();
            var random = new System.Random(seed * 7919 + 17);
            foreach (SpecialTileType type in TileSet)
            {
                if (candidates.Count == 0)
                {
                    break;
                }
                int index = random.Next(candidates.Count);
                Vector2Int cell = candidates[index];
                candidates.RemoveAt(index);
                result[cell] = type;
            }
            return result;
        }

        public static int ApplyDamageModifiers(int baseDamage, int flatBonus, float multiplier)
        {
            return Mathf.Max(1, Mathf.RoundToInt((baseDamage + Mathf.Max(0, flatBonus)) * Mathf.Max(0f, multiplier)));
        }

        public static bool IsBeneficial(SpecialTileType type)
        {
            return type == SpecialTileType.Power || type == SpecialTileType.Amplify;
        }
    }
}
