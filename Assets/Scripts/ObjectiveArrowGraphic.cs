using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    /// <summary>
    /// Code-native pixel UI matching the reference's white wedge and black border.
    /// No opaque reference-image background is drawn.
    /// </summary>
    public sealed class ObjectiveArrowGraphic : MaskableGraphic
    {
        private static readonly int[] RowWidths = { 2, 3, 4, 6, 8, 9, 11, 11, 9, 8, 6, 4, 3, 2 };

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float pixel = Mathf.Min(rect.width / 11f, rect.height / 14f);
            Vector2 origin = rect.center - new Vector2(5.5f, 7f) * pixel;
            for (int row = 0; row < RowWidths.Length; row++)
            {
                int width = RowWidths[row];
                AddQuad(mesh, origin + new Vector2(0f, row * pixel), width * pixel, pixel,
                    new Color(0.015f, 0.012f, 0.018f, color.a));
                if (row > 0 && row < RowWidths.Length - 1 && width > 2)
                    AddQuad(mesh, origin + new Vector2(pixel, row * pixel),
                        (width - 2) * pixel, pixel, color);
            }
        }

        private static void AddQuad(VertexHelper mesh, Vector2 position, float width, float height, Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(position, tint, Vector2.zero);
            mesh.AddVert(position + Vector2.up * height, tint, Vector2.zero);
            mesh.AddVert(position + new Vector2(width, height), tint, Vector2.zero);
            mesh.AddVert(position + Vector2.right * width, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
