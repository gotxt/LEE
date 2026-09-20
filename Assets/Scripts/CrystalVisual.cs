using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    // Presentation only: the existing crystal rules still own placement, blocking and attacks.
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class CrystalVisual : MonoBehaviour
    {
        [SerializeField] private RectTransform body = null;
        [SerializeField] private Image shadow = null;

        public RectTransform Body => body;
        public Image Shadow => shadow;

        public void SetCellSize(float cellSize) =>
            ((RectTransform)transform).sizeDelta = Vector2.one * cellSize;

        // The body pivots at its lower tip; the contact shadow stays fixed on the ground.
        public void SetPulse(float scale) => body.localScale = Vector3.one * scale;
    }
}
