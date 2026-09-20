#if UNITY_EDITOR
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    internal static class PatternPreviewGridGUI
    {
        private static readonly Color GridColor = new Color(0.22f, 0.23f, 0.26f, 1f);

        // Visual centre of the configured bounds, not the floor's centroid or
        // the runtime anchor tile. Small maps retain the legacy 17-cell canvas.
        public static RectInt ArenaCenterCells(BossArenaDefinition arena)
        {
            int offset = (arena.GridSize - arena.size) / 2;
            int first = offset + (arena.size - 1) / 2;
            int width = arena.size % 2 == 0 ? 2 : 1;
            return new RectInt(first, first, width, width);
        }

        public static void DrawCenterHighlight(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.85f, 1f, 0.3f));
            DrawOutline(rect, new Color(0.35f, 0.9f, 1f, 1f));
        }

        // Selection is an editing aid, not another attack-colour fill.
        public static void DrawSelectionOutline(Rect rect) => DrawOutline(rect, Color.green);

        private static void DrawOutline(Rect rect, Color outline)
        {
            float inset = 1f / EditorGUIUtility.pixelsPerPoint;
            rect = Rect.MinMaxRect(rect.xMin + inset, rect.yMin + inset,
                rect.xMax - inset, rect.yMax - inset);
            float thickness = Mathf.Min(2f, rect.width * 0.2f, rect.height * 0.2f);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, thickness), outline);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), outline);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, thickness, rect.height), outline);
            EditorGUI.DrawRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), outline);
        }

        // Returns true while Unity is preparing the atlas-safe asset preview.
        public static bool DrawTileSprite(Rect rect, Sprite sprite)
        {
            if (sprite == null) return false;
            if (sprite.packed)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(sprite);
                if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill, true);
                return preview == null && AssetPreview.IsLoadingAssetPreview(sprite.GetEntityId());
            }
            // textureRect selects the correct sub-sprite from a sliced spritesheet.
            Rect source = sprite.textureRect;
            Texture2D texture = sprite.texture;
            GUI.DrawTextureWithTexCoords(rect, texture, new Rect(source.x / texture.width,
                source.y / texture.height, source.width / texture.width, source.height / texture.height), true);
            return false;
        }

        public static Rect CellRect(Rect board, int x, int y, int gridSize)
        {
            int row = gridSize - 1 - y;
            float xMin = Snap(board.x + board.width * x / gridSize);
            float xMax = Snap(board.x + board.width * (x + 1) / gridSize);
            float yMin = Snap(board.y + board.height * row / gridSize);
            float yMax = Snap(board.y + board.height * (row + 1) / gridSize);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        public static void DrawLines(Rect board, int gridSize)
        {
            float pixel = 1f / EditorGUIUtility.pixelsPerPoint;
            float left = Snap(board.x);
            float top = Snap(board.y);
            float right = Snap(board.xMax);
            float bottom = Snap(board.yMax);
            for (int i = 0; i <= gridSize; i++)
            {
                float x = Snap(board.x + board.width * i / gridSize);
                float y = Snap(board.y + board.height * i / gridSize);
                if (i == gridSize) { x -= pixel; y -= pixel; }
                EditorGUI.DrawRect(new Rect(x, top, pixel, bottom - top), GridColor);
                EditorGUI.DrawRect(new Rect(left, y, right - left, pixel), GridColor);
            }
        }

        private static float Snap(float value)
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            return Mathf.Round(value * scale) / scale;
        }
    }
}
#endif
