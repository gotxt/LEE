using System;
using NHN.TraceStrike.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    public sealed partial class TraceStrikeGame : IMechanicPresentationHost
    {
        private BossMechanicSession mechanicSession;
        private RectTransform mechanicVisualRoot;

        private void StartMechanics()
        {
            StopMechanics();
            mechanicSession = new BossMechanicSession(ActivePhase.mechanics, new MechanicContext(this, activeBoss.FindPattern));
            RefreshMechanicHealthLabel();
        }
        private void StopMechanics()
        {
            IsEnraged = false;
            try { mechanicSession?.Dispose(); }
            catch (Exception error) { Debug.LogException(error, this); }
            mechanicSession = null;
        }
        private void AdvanceMechanics(float delta)
        {
            if (inputLocked) return;
            mechanicSession?.Advance(delta);
            if (!IsEnraged && mechanicSession != null && mechanicSession.IsEnraged)
            {
                IsEnraged = true;
                patternCursor = 0;
                statusText.text = "광폭화";
                PatternSignal?.Invoke("combat.enraged", "");
            }
            RefreshMechanicHealthLabel();
        }
        private void RefreshMechanicHealthLabel()
        {
            if (bossHealthText == null) return;
            bossHealthText.text = bossHealth + " / " + bossMaxHealth;
            if (IsEnraged) bossHealthText.text += "  [광폭화]";
            if (mechanicSession != null && !string.IsNullOrEmpty(mechanicSession.Status))
                bossHealthText.text += "  [" + mechanicSession.Status + "]";
        }
        private bool TryResolvePlayerBossDamage(int damage, out int health)
        {
            health = bossHealth;
            try
            {
                health = mechanicSession != null
                    ? mechanicSession.ResolvePlayerAttack(new System.Collections.Generic.HashSet<Vector2Int>(model.Trail), bossHealth, damage)
                    : Mathf.Max(0, bossHealth - damage);
                return true;
            }
            catch (Exception error)
            {
                CancelTimeline();
                StopMechanics();
                inputLocked = true;
                Debug.LogException(error, this);
                statusText.text = "기믹 실행 오류 — Console을 확인하세요";
                return false;
            }
        }

        IPatternLease IMechanicPresentationHost.ShowDevice(Vector2Int cell, GameObject prefab, Sprite sprite, Color tint)
        {
            string key = "mechanic/" + Guid.NewGuid().ToString("N");
            var lease = ((IPatternHost)this).Spawn(key, prefab, cell, sprite, tint);
            try
            {
                var instance = timelineObjects[key];
                // Persistent devices stay below warnings even when growing/spawning mid-warning.
                if (mechanicVisualRoot != null) instance.SetParent(mechanicVisualRoot, false);
                if (prefab != null)
                {
                    var crystal = instance.GetComponent<CrystalVisual>();
                    if (crystal != null) crystal.SetCellSize(mainCellSize);
                    else if (instance is RectTransform rect) rect.sizeDelta = Vector2.one * mainCellSize;
                    foreach (var graphic in instance.GetComponentsInChildren<Graphic>(true))
                    { graphic.color *= tint; graphic.raycastTarget = false; }
                }
                mainPlayer?.SetAsLastSibling();
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
    }
}
