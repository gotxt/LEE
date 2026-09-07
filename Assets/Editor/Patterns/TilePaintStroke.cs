#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    // One pointer capture and one undo snapshot per gesture; revisits never toggle tiles.
    internal sealed class TilePaintStroke
    {
        private int control;
        private bool erase;
        private Vector2Int? previous;
        private Action finish;

        public void Cancel()
        {
            if (control != 0 && GUIUtility.hotControl == control) GUIUtility.hotControl = 0;
            control = 0;
            previous = null;
            var action = finish;
            finish = null;
            action?.Invoke();
        }

        public void Handle(Rect board, int size, bool eraseTool, Action begin,
            Action<Vector2Int, bool> paint, Action changed, Action end)
        {
            int id = GUIUtility.GetControlID("EncounterTileBrush".GetHashCode(), FocusType.Passive, board);
            Event e = Event.current;
            if (e.type == EventType.MouseDown && board.Contains(e.mousePosition) &&
                (e.button == 0 || e.button == 1))
            {
                control = id;
                GUIUtility.hotControl = id;
                erase = eraseTool || e.button == 1 || e.shift;
                finish = end;
                previous = null;
                begin();
                PaintAt(board, size, e.mousePosition, paint);
                changed();
                e.Use();
            }
            else if (control == id && GUIUtility.hotControl == id)
            {
                if (e.type == EventType.MouseDrag)
                {
                    PaintAt(board, size, e.mousePosition, paint);
                    changed();
                    e.Use();
                }
                else if (e.rawType == EventType.MouseUp)
                {
                    Cancel();
                    e.Use();
                }
            }
        }

        private void PaintAt(Rect board, int size, Vector2 pointer, Action<Vector2Int, bool> paint)
        {
            if (!board.Contains(pointer)) { previous = null; return; }
            var cell = new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt((pointer.x - board.x) * size / board.width), 0, size - 1),
                size - 1 - Mathf.Clamp(Mathf.FloorToInt((pointer.y - board.y) * size / board.height), 0, size - 1));
            RasterLine(previous ?? cell, cell, c => paint(c, erase));
            previous = cell;
        }

        internal static void RasterLine(Vector2Int from, Vector2Int to, Action<Vector2Int> paint)
        {
            int dx = Math.Abs(to.x - from.x), dy = -Math.Abs(to.y - from.y);
            int sx = from.x < to.x ? 1 : -1, sy = from.y < to.y ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                paint(from);
                if (from == to) break;
                int twice = 2 * error;
                if (twice >= dy) { error += dy; from.x += sx; }
                if (twice <= dx) { error += dx; from.y += sy; }
            }
        }
    }
}
#endif
