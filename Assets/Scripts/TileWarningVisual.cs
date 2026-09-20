using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    // Presentation only. The owning warning event still controls its area and lifetime.
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class TileWarningVisual : MonoBehaviour
    {
        [SerializeField] private Image indicator = null;
        [SerializeField] private Image progressFill = null;
        [SerializeField, Range(0, 1)] private float progressOpacity = .65f;

        public Image Indicator => indicator;
        public Image ProgressFill => progressFill;

        public void SetColor(Color color)
        {
            color.a *= progressOpacity;
            progressFill.color = color;
        }

        public void SetProgress(float progress)
        {
            // Keep the complete warning symbol legible from the first frame.
            progressFill.rectTransform.localScale = Vector3.one * Mathf.Clamp01(progress);
        }
    }
}
