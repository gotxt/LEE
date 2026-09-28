#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class RegionGrowthRuntimeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Get<T>(TraceStrikeGame game, string field) => (T)typeof(TraceStrikeGame).GetField(field, Flags).GetValue(game);
        private static void Set(TraceStrikeGame game, string field, object value) => typeof(TraceStrikeGame).GetField(field, Flags).SetValue(game, value);
        private static object Call(TraceStrikeGame game, string method, params object[] args) => typeof(TraceStrikeGame).GetMethod(method, Flags).Invoke(game, args);
        [UnityTest] public IEnumerator RealGameGrowthPauseBurnEnrageSchedulingAndRestart()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            string before = JsonUtility.ToJson(saved); var copy = Object.Instantiate(saved);
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                game.PreviewPattern(saved, new EncounterPattern { minimumDuration = 999 });
                Set(game, "activeBoss", copy);
                // This test isolates growth/enrage scheduling; the optional HP policy has its own integration test.
                copy.phases[0].mechanics.RemoveAll(m => m is EnrageSurvivalMechanic);
                var growth = copy.phases[0].mechanics.OfType<RegionGrowthMechanic>().Single();
                growth.seedsPerSpawn = 2500; // Test-only saturation: exercise every tile/cleanup in the real host.
                var fire = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire");
                copy.arena.SetSpecialTile(new Vector2Int(15, 2), fire);
                copy.arena.SetSpecialTile(new Vector2Int(15, 12), fire);
                Call(game, "StartSpecialTiles"); Call(game, "StartMechanics");
                Set(game, "bossHealth", 73);
                var session = Get<BossMechanicSession>(game, "mechanicSession");
                var runtime = session.Runtimes.OfType<RegionGrowthRuntime>().Single();
                var model = Get<TrailFieldModel>(game, "model");
                var beforePattern = new EncounterPattern { combatCondition = PatternCombatCondition.BeforeEnrage };
                var afterPattern = new EncounterPattern { name = "Enraged Test", combatCondition = PatternCombatCondition.Enraged };
                copy.phases[0].patterns.Clear(); copy.phases[0].patterns.Add(beforePattern); copy.phases[0].patterns.Add(afterPattern);
                Assert.AreSame(beforePattern, Call(game, "ChooseNextPattern"));
                using var warning = ((IPatternHost)game).Mark(new[] { new Vector2Int(15, 5) }, Color.red, true);
                Call(game, "AdvanceMechanics", 5f); Assert.AreEqual(745, runtime.Seeds.Count);
                var deviceRoot = Get<RectTransform>(game, "mechanicVisualRoot");
                var warningTransform = Get<RectTransform>(game, "mainGrid").Cast<Transform>().First(t => t.name == "Timeline Warning");
                Assert.Greater(warningTransform.GetSiblingIndex(), deviceRoot.GetSiblingIndex());
                Call(game, "Move", Vector2Int.up); Assert.AreEqual(20, game.FireMovesRemaining);
                Assert.IsFalse(runtime.Seeds.Any(s => s.Cell == new Vector2Int(15, 2)));
                Call(game, "RefreshBoard"); yield return null; Capture(game, "Seeds");

                Set(game, "inputLocked", true); Call(game, "AdvanceMechanics", 20f);
                Assert.AreEqual(5, runtime.Elapsed); Assert.IsFalse(runtime.Seeds.Any(s => s.Mature));
                Assert.IsFalse(game.IsEnraged);
                Set(game, "inputLocked", false);
                int signals = 0; game.PatternSignal += (name, argument) => { if (name == "combat.enraged") signals++; };
                var currentAttack = Get<PatternRunner>(game, "timeline");
                Call(game, "AdvanceMechanics", 20f);
                Assert.IsTrue(game.IsEnraged); Assert.AreEqual(1, signals); Assert.AreEqual(73, Get<int>(game, "bossHealth"));
                Assert.Greater(warningTransform.GetSiblingIndex(), deviceRoot.GetSiblingIndex());
                Assert.AreSame(session, Get<BossMechanicSession>(game, "mechanicSession"));
                Assert.AreSame(currentAttack, Get<PatternRunner>(game, "timeline"));
                Assert.AreSame(afterPattern, Call(game, "ChooseNextPattern"));
                currentAttack.Advance(1000);
                copy.phases[0].interval = copy.phases[0].minimumInterval = copy.phases[0].acceleration = 0;
                Set(game, "<IsPatternPreview>k__BackingField", false);
                Call(game, "TickTimeline");
                Assert.AreNotSame(currentAttack, Get<PatternRunner>(game, "timeline"));
                Assert.AreEqual(afterPattern.name, Get<UnityEngine.UI.Text>(game, "statusText").text);
                Assert.AreSame(session, Get<BossMechanicSession>(game, "mechanicSession"));
                Call(game, "AdvanceMechanics", 1f); Assert.AreEqual(1, signals);
                var field = Get<SpecialTileField>(game, "placedSpecialTiles");
                Assert.AreSame(growth.overgrownTile, field.At(new Vector2Int(15, 12)));
                Call(game, "Move", Vector2Int.up); Assert.AreEqual(new Vector2Int(15, 3), model.Player);
                Assert.AreEqual(19, game.FireMovesRemaining);
                Assert.IsTrue(runtime.Seeds.Single(s => s.Cell == model.Player).Mature);
                var state = Get<SpecialTilePlayerState>(game, "tilePlayerState"); Assert.AreEqual(1, state.StunRemaining);
                Call(game, "Move", Vector2Int.up); Assert.AreEqual(new Vector2Int(15, 3), model.Player);
                state.Advance(1); Call(game, "Move", Vector2Int.up); Assert.AreEqual(new Vector2Int(15, 4), model.Player);
                Assert.AreEqual(1, state.StunRemaining);
                Assert.AreEqual(0, session.ResolvePlayerAttack(new[] { model.Player }, 73, 100)); // No HP lock in this mechanic.
                Call(game, "RefreshBoard"); Call(game, "TickSpecialTiles", 0f); yield return null; Capture(game, "Overgrown");

                game.StopPatternPreview(); Assert.IsFalse(game.IsEnraged); Assert.AreEqual(0, game.FireMovesRemaining);
                Assert.IsEmpty(runtime.Seeds); Assert.IsEmpty(runtime.Regions);
                Assert.AreEqual(0, Get<BossMechanicSession>(game, "mechanicSession").Runtimes.OfType<RegionGrowthRuntime>().Single().Elapsed);
                game.enabled = false; Assert.IsNull(Get<BossMechanicSession>(game, "mechanicSession"));
                Assert.IsNull(Get<SpecialTileField>(game, "placedSpecialTiles"));
                Assert.AreEqual(before, JsonUtility.ToJson(saved));
            }
            finally { Time.timeScale = oldScale; Object.Destroy(copy); }
            yield return new ExitPlayMode();
        }
        private static void Capture(TraceStrikeGame game, string name)
        {
            var camera = Get<Camera>(game, "uiCamera"); var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/RegionGrowth");
                File.WriteAllBytes("Logs/PatternValidation/RegionGrowth/" + name + ".png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
