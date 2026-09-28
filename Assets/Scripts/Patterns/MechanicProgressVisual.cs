using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike.Patterns
{
    // The owning mechanic supplies progress on its gameplay clock; this view never ages itself.
    [RequireComponent(typeof(Image))]
    public sealed class MechanicProgressVisual : MonoBehaviour
    {
        [Tooltip("Ordered stages, including the initial sprite and the final sprite at full progress.")]
        public Sprite[] frames = System.Array.Empty<Sprite>();
        private Image target;

        private void Awake() => SetProgress(0);

        public void SetProgress(float progress)
        {
            if (frames == null || frames.Length == 0) return;
            if (target == null) target = GetComponent<Image>();
            progress = float.IsNaN(progress) ? 0 : Mathf.Clamp01(progress);
            int index = progress >= 1 ? frames.Length - 1 :
                Mathf.Min(Mathf.FloorToInt(progress * (frames.Length - 1)), Mathf.Max(0, frames.Length - 2));
            target.sprite = frames[index];
        }
    }
}
