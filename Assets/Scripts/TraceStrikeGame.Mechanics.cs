using System;
using NHN.TraceStrike.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    public sealed partial class TraceStrikeGame : IMechanicPresentationHost
    {
        private BossMechanicSession mechanicSession;

        private void StartMechanics()
        {
            StopMechanics();
            mechanicSession = new BossMechanicSession(ActivePhase.mechanics, new MechanicContext(this, activeBoss.FindPattern));
            RefreshMechanicHealthLabel();
        }
        private void StopMechanics()
        {
            try { mechanicSession?.Dispose(); }
            catch (Exception error) { Debug.LogException(error, this); }
            mechanicSession = null;
        }
        private void RefreshMechanicHealthLabel()
        {
            if (bossHealthText == null) return;
            bossHealthText.text = bossHealth + " / " + bossMaxHealth;
            if (mechanicSession != null && !string.IsNullOrEmpty(mechanicSession.Status))
                bossHealthText.text += "  [" + mechanicSession.Status + "]";
        }
        private int ResolvePlayerBossDamage(int damage)
        {
            int health = mechanicSession != null
                ? mechanicSession.ResolvePlayerAttack(new System.Collections.Generic.HashSet<Vector2Int>(model.Trail), bossHealth, damage)
                : Mathf.Max(0, bossHealth - damage);
            return health;
        }

        IPatternLease IMechanicPresentationHost.ShowDevice(Vector2Int cell, GameObject prefab, Sprite sprite, Color tint)
        {
            string key = "mechanic/" + Guid.NewGuid().ToString("N");
            var lease = ((IPatternHost)this).Spawn(key, prefab, cell, sprite, tint);
            try
            {
                var instance = timelineObjects[key];
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
