#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Effects;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace NHN.TraceStrike.Tests
{
    public sealed class RottenBloomPetalRuntimeTests
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        static T Field<T>(TraceStrikeGame game, string name) => (T)typeof(TraceStrikeGame).GetField(name, Flags).GetValue(game);
        static HashSet<Vector2Int> Danger(TraceStrikeGame game) => new HashSet<Vector2Int>(
            Field<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger").Values.SelectMany(c => c));
        static void Refresh(TraceStrikeGame game)
        {
            typeof(TraceStrikeGame).GetField("battleCameraInitialized", Flags).SetValue(game, false);
            typeof(TraceStrikeGame).GetMethod("RefreshBoard", Flags).Invoke(game, null);
        }

        [UnityTest]
        public IEnumerator ActualGameFreezesAreaAllowsElevenStepEscapeReselectsAndCleansUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required for real game presentation.");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            var boss = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            var pattern = boss.FindPattern("p1-petal-collapse");
            float oldTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                game.PreviewPattern(boss, pattern);
                var model = Field<TrailFieldModel>(game, "model");
                var runner = Field<PatternRunner>(game, "timeline");
                var grid = Field<RectTransform>(game, "mainGrid");
                Assert.IsTrue(model.TryPlacePlayer(new Vector2Int(15, 0))); Refresh(game);
                runner.Advance(.5f);
                CollectionAssert.AreEquivalent(boss.tileRegions[3].cells, Danger(game));
                for (int i = 0; i < 11; i++)
                {
                    Assert.AreNotEqual(MoveResult.Blocked, model.TryMove(Vector2Int.up));
                    runner.Advance(.35f);
                }
                Assert.AreEqual(new Vector2Int(15, 11), model.Player); Assert.Less(runner.Time, 5);
                Refresh(game); yield return null;
                Capture(game, "WarningAfterEscape");
                runner.Advance(5.01f - runner.Time);
                CollectionAssert.AreEquivalent(boss.tileRegions[3].cells, Danger(game));
                var effects = grid.GetComponentsInChildren<UiEffectPlayer>(); Assert.AreEqual(106, effects.Length);
                foreach (var effect in effects) effect.Sample(.01f);
                yield return null; Capture(game, "ImpactSamePetal");
                runner.Advance(.24f); Assert.IsNull(Field<string>(game, "pendingTimelineDamage"));
                runner.Advance(1); yield return null;
                Assert.IsEmpty(Danger(game)); Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());

                // New execution, same snapshot key, different player petal.
                game.PreviewPattern(boss, pattern); model = Field<TrailFieldModel>(game, "model");
                Assert.IsTrue(model.TryPlacePlayer(new Vector2Int(15, 31)));
                runner = Field<PatternRunner>(game, "timeline"); runner.Advance(0);
                CollectionAssert.AreEquivalent(boss.tileRegions[0].cells, Danger(game));
                game.StopPatternPreview(); yield return null; Assert.IsEmpty(Danger(game));

                // Central no-target is also frozen, not lazily chosen at impact.
                game.PreviewPattern(boss, pattern); model = Field<TrailFieldModel>(game, "model");
                Assert.IsTrue(model.TryPlacePlayer(new Vector2Int(15, 11)));
                runner = Field<PatternRunner>(game, "timeline"); runner.Advance(0);
                model.TryPlacePlayer(new Vector2Int(15, 0)); runner.Advance(5.2f);
                Assert.IsEmpty(Danger(game)); Assert.IsNull(Field<string>(game, "pendingTimelineDamage"));
                game.StopPatternPreview(); yield return null;

                // Staying in the selected petal really does request damage.
                game.PreviewPattern(boss, pattern);
                runner = Field<PatternRunner>(game, "timeline"); runner.Advance(5.2f);
                Assert.AreEqual("꽃잎 붕괴", Field<string>(game, "pendingTimelineDamage"));
                game.StopPatternPreview(); yield return null;
                Assert.IsEmpty(Danger(game)); Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());

                // Cancellation while impact VFX are live must also release them.
                game.PreviewPattern(boss, pattern);
                runner = Field<PatternRunner>(game, "timeline"); runner.Advance(5.01f);
                Assert.IsNotEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
                game.StopPatternPreview(); yield return null;
                Assert.IsEmpty(Danger(game)); Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
            }
            finally { Time.timeScale = oldTimeScale; }
            yield return new ExitPlayMode();
        }

        static void Capture(TraceStrikeGame game, string name)
        {
            var camera = Field<Camera>(game, "uiCamera");
            var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/RottenBloomPetals");
                File.WriteAllBytes("Logs/PatternValidation/RottenBloomPetals/" + name + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
