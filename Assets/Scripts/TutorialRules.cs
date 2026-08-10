using UnityEngine;

namespace NHN.TraceStrike
{
    public static class TutorialRules
    {
        public const int Size = 4;
        public static readonly Vector2Int Start = new Vector2Int(0, 0);
        public static readonly Vector2Int End = new Vector2Int(3, 3);

        private static readonly SpecialTileType[] Sequence =
        {
            SpecialTileType.Power,
            SpecialTileType.Amplify,
            SpecialTileType.Mud,
            SpecialTileType.Curse
        };

        private static readonly Vector2Int[] Targets =
        {
            new Vector2Int(2, 3),
            new Vector2Int(1, 3),
            new Vector2Int(1, 2),
            new Vector2Int(2, 2)
        };

        public static int SpecialStepCount => Sequence.Length;

        public static bool IsInside(Vector2Int cell)
        {
            return cell.x >= 0 && cell.x < Size && cell.y >= 0 && cell.y < Size;
        }

        public static SpecialTileType GetSpecialType(int specialStep)
        {
            return Sequence[Mathf.Clamp(specialStep, 0, Sequence.Length - 1)];
        }

        public static Vector2Int GetSpecialTarget(int specialStep)
        {
            return Targets[Mathf.Clamp(specialStep, 0, Targets.Length - 1)];
        }
    }
}
