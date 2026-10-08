using System;
using NHN.TraceStrike.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    public sealed partial class TraceStrikeGame : IMechanicPresentationHost, IMechanicOutputHost
    {
        public System.Collections.Generic.IReadOnlyCollection<Vector2Int> CaptureMechanicPositions(string mechanicId, string outputKey) =>
            (mechanicSession ?? throw new InvalidOperationException("기믹 세션이 없습니다. 기믹 합동 미리보기 또는 일반 전투에서 실행하세요.")).CaptureMechanicPositions(mechanicId, outputKey);
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
            if (inputLocked || playerDead || gameCleared) return;
            mechanicSession?.Advance(delta);
            if (!IsEnraged && mechanicSession != null && mechanicSession.IsEnraged)
            {
                IsEnraged = true;
                int previousHealth = bossHealth;
                bossHealth = mechanicSession.EnterEnrage(bossHealth);
                bossMaxHealth = (int)Math.Min(int.MaxValue, (long)bossMaxHealth + bossHealth - previousHealth);
                patternCursor = 0;
                statusText.text = "광폭화";
                PatternSignal?.Invoke("combat.enraged", "");
            }
            if (mechanicSession != null) bossHealth = mechanicSession.ApplyTimedHealthChanges(bossHealth);
            if (bossHealthFill != null) bossHealthFill.fillAmount = bossMaxHealth > 0 ? (float)bossHealth / bossMaxHealth : 0;
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
                var completedTrail = new System.Collections.Generic.HashSet<Vector2Int>(model.Trail);
                health = mechanicSession != null
                    ? mechanicSession.ResolvePlayerAttack(completedTrail, bossHealth, damage)
                    : Mathf.Max(0, bossHealth - damage);
                // A valid attack still ignites tiles when a mechanic prevents HP damage (e.g. enrage).
                if (placedSpecialTiles?.OnCompletedAttack(completedTrail) == true) RefreshBoard();
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
                var progressVisual = instance.GetComponent<MechanicProgressVisual>();
                if (progressVisual != null)
                    return new Lease(lease.Dispose, progress =>
                    {
                        lease.SetProgress(progress);
                        if (progressVisual != null) progressVisual.SetProgress(progress);
                    });
                return lease;
            }
            catch { lease.Dispose(); throw; }
        }
    }
}
