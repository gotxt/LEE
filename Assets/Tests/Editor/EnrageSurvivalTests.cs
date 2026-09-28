#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using NHN.TraceStrike.Editor;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class EnrageSurvivalTests
    {
        private BossEncounterDefinition boss;
        private PatternPreviewHost host;
        private MechanicContext context;
        [SetUp] public void SetUp()
        {
            boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            host = new PatternPreviewHost(boss); context = new MechanicContext(host, boss.FindPattern);
        }
        [TearDown] public void TearDown() { host.Dispose(); Object.DestroyImmediate(boss); }

        [TestCase(1, 61)] [TestCase(40, 100)] [TestCase(150, 210)]
        public void BonusIsAddedOnceAndFullHealthCanExceedOldMaximum(int current, int expected)
        {
            using var session = new BossMechanicSession(new[] { new EnrageSurvivalMechanic() }, context);
            session.Advance(500); // Time before enrage is never charged to the survival period.
            int health = session.EnterEnrage(current);
            Assert.AreEqual(expected, health);
            Assert.AreEqual(health, session.EnterEnrage(health));
            Assert.AreEqual(health, session.ApplyTimedHealthChanges(health));
            session.Advance(.5f); health = session.ApplyTimedHealthChanges(health); Assert.AreEqual(expected, health);
            session.Advance(.5f); health = session.ApplyTimedHealthChanges(health); Assert.AreEqual(expected - 1, health);
            session.Advance(expected - 1); Assert.AreEqual(0, session.ApplyTimedHealthChanges(health));
        }

        [Test] public void DamageWorksBeforeEnrageAndIsBlockedOnlyForConfiguredPhase()
        {
            using var session = new BossMechanicSession(new[] { new EnrageSurvivalMechanic() }, context);
            Assert.AreEqual(36, session.ResolvePlayerAttack(Array.Empty<Vector2Int>(), 40, 4));
            int health = session.EnterEnrage(40);
            Assert.AreEqual(100, session.ResolvePlayerAttack(Array.Empty<Vector2Int>(), health, int.MaxValue));
            session.Advance(1); Assert.AreEqual(99, session.ApplyTimedHealthChanges(health));
            using var disabled = new BossMechanicSession(new[] { new EnrageSurvivalMechanic { enabled = false } }, context);
            Assert.AreEqual(40, disabled.EnterEnrage(40));
            Assert.AreEqual(0, disabled.ResolvePlayerAttack(Array.Empty<Vector2Int>(), 40, 100));
            using var ordinary = new BossMechanicSession(Array.Empty<BossMechanicDefinition>(), context);
            Assert.AreEqual(40, ordinary.EnterEnrage(40));
            Assert.AreEqual(39, ordinary.ResolvePlayerAttack(Array.Empty<Vector2Int>(), 40, 1));
        }

        [Test] public void LargeAndSmallStepsAgreeAndConsumptionDoesNotDoubleCharge()
        {
            var definition = new EnrageSurvivalMechanic { bonusHealth = 10, tickInterval = .5f, healthPerTick = 2 };
            using var one = new BossMechanicSession(new[] { definition }, context);
            using var many = new BossMechanicSession(new[] { definition }, context);
            int a = one.EnterEnrage(90), b = many.EnterEnrage(90);
            one.Advance(12.25f); a = one.ApplyTimedHealthChanges(a);
            for (int i = 0; i < 49; i++) { many.Advance(.25f); b = many.ApplyTimedHealthChanges(b); }
            Assert.AreEqual(52, a); Assert.AreEqual(a, b);
            Assert.AreEqual(a, one.ApplyTimedHealthChanges(a));
            one.Advance(.25f); Assert.AreEqual(50, one.ApplyTimedHealthChanges(a));
        }

        [Test] public void ZeroHealthIsNotRevivedAndDisposalClearsImmunityAndPendingTicks()
        {
            var definition = new EnrageSurvivalMechanic();
            using var empty = new EnrageSurvivalRuntime(definition, context);
            Assert.AreEqual(0, empty.EnterEnrage(0)); Assert.IsFalse(empty.BlocksPlayerDamage);
            var runtime = new EnrageSurvivalRuntime(definition, context);
            Assert.AreEqual(100, runtime.EnterEnrage(40)); runtime.Advance(20);
            runtime.Dispose(); runtime.Dispose();
            Assert.IsFalse(runtime.BlocksPlayerDamage); Assert.AreEqual(40, runtime.ApplyTimedHealthChange(40));
            using var restart = new EnrageSurvivalRuntime(definition, context);
            Assert.AreEqual(100, restart.EnterEnrage(40));
            restart.Advance(.5f); Assert.AreEqual(100, restart.ApplyTimedHealthChange(100));
        }

        [Test] public void HealthArithmeticIsBoundedForExtremeConfiguredValues()
        {
            using var runtime = new EnrageSurvivalRuntime(new EnrageSurvivalMechanic { bonusHealth = int.MaxValue, healthPerTick = int.MaxValue }, context);
            Assert.AreEqual(int.MaxValue, runtime.EnterEnrage(150));
            runtime.Advance(float.MaxValue); Assert.AreEqual(0, runtime.ApplyTimedHealthChange(int.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => runtime.Advance(float.NaN));
        }

        [TestCase(-1, 1f, 1)] [TestCase(60, 0f, 1)] [TestCase(60, float.NaN, 1)]
        [TestCase(60, float.PositiveInfinity, 1)] [TestCase(60, 1f, 0)]
        public void InvalidSettingsFailBeforePlaying(int bonus, float interval, int loss)
        {
            var definition = new EnrageSurvivalMechanic { bonusHealth = bonus, tickInterval = interval, healthPerTick = loss };
            var errors = new List<string>(); definition.Validate(boss, errors);
            Assert.IsNotEmpty(errors); Assert.Throws<InvalidOperationException>(() => definition.Create(context));
        }

        [Test] public void DuplicatePoliciesAreRejectedInsteadOfDoublingHealthAndDrain()
        {
            var definition = new EnrageSurvivalMechanic();
            boss.phases[0].mechanics.Add(definition); boss.phases[0].mechanics.Add(new EnrageSurvivalMechanic());
            var errors = new List<string>(); definition.Validate(boss, errors); Assert.IsNotEmpty(errors);
            Assert.Throws<InvalidOperationException>(() => new BossMechanicSession(boss.phases[0].mechanics, context));
        }

        [Test] public void SavedBossAndExistingGenericEditorCanDiscoverAndSerializePolicy()
        {
            var saved = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>("Assets/Resources/Patterns/BossData_.RottenBloom.asset");
            var definition = saved.phases[0].mechanics.OfType<EnrageSurvivalMechanic>().Single();
            Assert.IsTrue(definition.enabled); Assert.AreEqual(60, definition.bonusHealth);
            Assert.AreEqual(1, definition.tickInterval); Assert.AreEqual(1, definition.healthPerTick);
            Assert.IsTrue(TypeCache.GetTypesDerivedFrom<BossMechanicDefinition>()
                .Any(t => t == typeof(EnrageSurvivalMechanic) && t.IsPublic && t.IsSerializable));
            var serialized = new SerializedObject(saved);
            int index = saved.phases[0].mechanics.IndexOf(definition);
            var property = serialized.FindProperty("phases").GetArrayElementAtIndex(0).FindPropertyRelative("mechanics").GetArrayElementAtIndex(index);
            Assert.AreEqual(60, property.FindPropertyRelative("bonusHealth").intValue);
            Assert.AreEqual(1, property.FindPropertyRelative("tickInterval").floatValue);
            const string path = "Assets/EnrageSurvival_RoundTrip_Test.asset";
            Assert.IsFalse(System.IO.File.Exists(path));
            try
            {
                var copy = Object.Instantiate(saved); AssetDatabase.CreateAsset(copy, path); AssetDatabase.SaveAssetIfDirty(copy);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var reloaded = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(path);
                Assert.AreEqual(60, reloaded.phases[0].mechanics.OfType<EnrageSurvivalMechanic>().Single().bonusHealth);
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
#endif
