using System;
using NHN.TraceStrike.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    public sealed partial class TraceStrikeGame : IBossPatternHost
    {
        private BossRenderStage bossRenderStage;
        private RawImage bossVisualImage;

        private void BuildEncounterBossVisual()
        {
            DisposeBossVisual();
            if (activeBoss.bossVisual == null || activeBoss.bossVisual.prefab == null) return;
            bossRenderStage = new BossRenderStage(activeBoss.bossVisual, model.GridSize, false);
            var rect = CreateRect("Encounter Boss Visual", mainGrid);
            rect.anchorMin = rect.anchorMax = Vector2.one * 0.5f;
            rect.sizeDelta = Vector2.one * (model.GridSize * mainCellSize);
            rect.anchoredPosition = Vector2.zero;
            bossVisualImage = rect.gameObject.AddComponent<RawImage>();
            bossVisualImage.texture = bossRenderStage.Texture; bossVisualImage.raycastTarget = false;
            rect.SetSiblingIndex(mainPlayer.GetSiblingIndex());
            bossRenderStage.Render();
        }

        private void DisposeBossVisual()
        {
            bossRenderStage?.Dispose(); bossRenderStage = null;
            if (bossVisualImage != null) Destroy(bossVisualImage.gameObject);
            bossVisualImage = null;
        }

        private void UpdateBossVisual()
        {
            if (bossRenderStage == null || bossVisualImage == null || !bossVisualImage.gameObject.activeInHierarchy) return;
            if (!inputLocked && !playerDead && !gameCleared)
                bossRenderStage.Presentation.Advance(Time.deltaTime);
            bossRenderStage.Render();
        }

        private BossPresentation BossPresentation => bossRenderStage?.Presentation ??
            throw new InvalidOperationException("Assign a BossActor prefab before running boss events.");
        IPatternLease IBossPatternHost.BossAnimation(BossAnimationEvent action) => BossPresentation.BossAnimation(action);
        IPatternLease IBossPatternHost.BossVfx(BossVfxEvent action) => BossPresentation.BossVfx(action);
        IPatternLease IBossPatternHost.BossMotion(BossMotionEvent action, float duration) => BossPresentation.BossMotion(action, duration);
    }
}
