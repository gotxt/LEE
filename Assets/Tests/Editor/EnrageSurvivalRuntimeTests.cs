#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class EnrageSurvivalRuntimeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Get<T>(TraceStrikeGame game, string field) => (T)typeof(TraceStrikeGame).GetField(field, Flags).GetValue(game);
        private static void Set(TraceStrikeGame game, string field, object value) => typeof(TraceStrikeGame).GetField(field, Flags).SetValue(game, value);
        private static object Call(TraceStrikeGame game, string method, params object[] args) => typeof(TraceStrikeGame).GetMethod(method, Flags).Invoke(game, args);

        private static BossEncounterDefinition Prepare(TraceStrikeGame game, BossEncounterDefinition saved)
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
            var hold = new EncounterPattern { minimumDuration = 1000, clips = new List<PatternClip> {
                new PatternClip { duration = 1000, action = new WarningEvent { tiles = new TileSelection { shape = TileShape.All } } }
            } };
            game.PreviewPattern(saved, hold);
            var copy = Object.Instantiate(saved);
            Set(game, "activeBoss", copy);
            var growth = copy.phases[0].mechanics.OfType<RegionGrowthMechanic>().Single();
            growth.regionIds = new List<string> { growth.enrageWhenAny.Single() };
            growth.enrageWhenAll.Clear(); growth.initialDelay = 0; growth.growthSeconds = 1;
            growth.seedsPerSpawn = 1; growth.coverage = .001f;
            Call(game, "StartMechanics");
            Get<PatternRunner>(game, "timeline").Advance(0);
            return copy;
        }

        [UnityTest] public IEnumerator RealGameBonusImmunityPauseCountdownClearAndRestart()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            string before = JsonUtility.ToJson(saved); float oldScale = Time.timeScale;
            BossEncounterDefinition copy = null;
            try
            {
                Time.timeScale = 0; copy = Prepare(game, saved);
                Set(game, "bossHealth", 40);
                var runner = Get<PatternRunner>(game, "timeline");
                var session = Get<BossMechanicSession>(game, "mechanicSession");
                var growth = session.Runtimes.OfType<RegionGrowthRuntime>().Single();
                int signals = 0; void OnSignal(string name, string argument) { if (name == "combat.enraged") signals++; }
                game.PatternSignal += OnSignal;
                try
                {
                    Call(game, "AdvanceMechanics", .5f); Assert.AreEqual(40, Get<int>(game, "bossHealth"));
                    Assert.IsFalse(game.IsEnraged);
                    Call(game, "AdvanceMechanics", .5f);
                    Assert.IsTrue(game.IsEnraged); Assert.AreEqual(100, Get<int>(game, "bossHealth"));
                    Assert.AreEqual(210, Get<int>(game, "bossMaxHealth"));
                    Assert.That(Get<Image>(game, "bossHealthFill").fillAmount, Is.EqualTo(100f / 210).Within(.0001f));
                    Assert.AreSame(runner, Get<PatternRunner>(game, "timeline")); Assert.AreSame(session, Get<BossMechanicSession>(game, "mechanicSession"));
                    Assert.IsTrue(growth.Regions.Single().Overgrown);
                    object[] attack = { 999, 0 };
                    Assert.IsTrue((bool)Call(game, "TryResolvePlayerBossDamage", attack)); Assert.AreEqual(100, attack[1]);
                    Set(game, "inputLocked", true); Call(game, "AdvanceMechanics", 100f);
                    Assert.AreEqual(100, Get<int>(game, "bossHealth"));
                    Set(game, "inputLocked", false); Call(game, "TickTimeline"); // Time.deltaTime is zero while paused.
                    Assert.AreEqual(100, Get<int>(game, "bossHealth"));
                    Call(game, "AdvanceMechanics", .75f); Assert.AreEqual(100, Get<int>(game, "bossHealth"));
                    Call(game, "AdvanceMechanics", .25f); Assert.AreEqual(99, Get<int>(game, "bossHealth"));
                    Set(game, "movementFrozen", true); Call(game, "AdvanceMechanics", 1f);
                    Assert.AreEqual(98, Get<int>(game, "bossHealth")); Set(game, "movementFrozen", false);
                    Call(game, "AdvanceMechanics", 97f); Assert.AreEqual(1, Get<int>(game, "bossHealth"));
                    Assert.IsFalse(Get<bool>(game, "gameCleared"));
                    Call(game, "AdvanceMechanics", .999f); Assert.AreEqual(1, Get<int>(game, "bossHealth"));
                    Call(game, "AdvanceMechanics", .001f); Assert.AreEqual(0, Get<int>(game, "bossHealth"));
                    Assert.AreEqual(1, signals);
                    Call(game, "TickTimeline");
                    Assert.IsTrue(Get<bool>(game, "gameCleared")); Assert.IsTrue(Get<bool>(game, "inputLocked"));
                    Assert.That(Get<Text>(game, "statusText").text, Does.Contain("STAGE CLEAR"));
                    Assert.IsNull(Get<BossMechanicSession>(game, "mechanicSession"));
                    Assert.IsNull(Get<PatternRunner>(game, "timeline")); Assert.IsEmpty(growth.Seeds);
                    Assert.IsEmpty(Get<Dictionary<int, HashSet<Vector2Int>>>(game, "timelineDanger"));
                    Assert.IsNull(Get<SpecialTileField>(game, "placedSpecialTiles"));
                    Call(game, "TickTimeline"); Assert.AreEqual(0, Get<int>(game, "bossHealth"));
                    game.StopPatternPreview();
                    Assert.AreEqual(150, Get<int>(game, "bossHealth")); Assert.IsFalse(game.IsEnraged);
                    Assert.IsFalse(Get<BossMechanicSession>(game, "mechanicSession").BlocksPlayerDamage);
                    game.enabled = false; Assert.IsNull(Get<BossMechanicSession>(game, "mechanicSession"));
                    Assert.AreEqual(before, JsonUtility.ToJson(saved));
                }
                finally { game.PatternSignal -= OnSignal; }
            }
            finally { game.StopAllCoroutines(); game.enabled = false; Time.timeScale = oldScale; if (copy != null) Object.Destroy(copy); }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator PendingPlayerDeathWinsOverTimerExpiration()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            float oldScale = Time.timeScale; BossEncounterDefinition copy = null;
            try
            {
                Time.timeScale = 0; copy = Prepare(game, Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom"));
                Set(game, "bossHealth", 1); Call(game, "AdvanceMechanics", 1f);
                Call(game, "AdvanceMechanics", 61f); Assert.AreEqual(0, Get<int>(game, "bossHealth"));
                Set(game, "pendingTimelineDamage", "same-frame hit"); Call(game, "TickTimeline");
                Assert.IsTrue(Get<bool>(game, "playerDead")); Assert.IsFalse(Get<bool>(game, "gameCleared"));
                Assert.IsNull(Get<BossMechanicSession>(game, "mechanicSession"));
                Call(game, "AdvanceMechanics", 100f); Assert.IsFalse(Get<bool>(game, "gameCleared"));
            }
            finally { game.StopAllCoroutines(); game.enabled = false; Time.timeScale = oldScale; if (copy != null) Object.Destroy(copy); }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator TimedDefeatUsesNormalNextPhaseWhenOneExists()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            float oldScale = Time.timeScale; BossEncounterDefinition copy = null;
            try
            {
                Time.timeScale = 0; copy = Prepare(game, Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom"));
                copy.phases.Add(new BossPhaseDefinition { health = 87, patterns = new List<EncounterPattern> { new EncounterPattern { minimumDuration = 10 } } });
                Set(game, "bossHealth", 1); Call(game, "AdvanceMechanics", 1f); Call(game, "AdvanceMechanics", 61f);
                Call(game, "TickTimeline"); Assert.IsTrue(Get<bool>(game, "inputLocked"));
                float deadline = Time.realtimeSinceStartup + 6;
                while (Get<bool>(game, "inputLocked") && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.AreEqual(1, Get<int>(game, "activePhaseIndex")); Assert.AreEqual(87, Get<int>(game, "bossHealth"));
                Assert.IsFalse(Get<bool>(game, "gameCleared")); Assert.IsFalse(Get<bool>(game, "inputLocked"));
                Assert.IsFalse(game.IsEnraged); Assert.IsFalse(Get<BossMechanicSession>(game, "mechanicSession").BlocksPlayerDamage);
            }
            finally { game.StopAllCoroutines(); game.enabled = false; Time.timeScale = oldScale; if (copy != null) Object.Destroy(copy); }
            yield return new ExitPlayMode();
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
