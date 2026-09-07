#if UNITY_EDITOR
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class PatternAssetTests
    {
        [Test]
        public void RecursivePatternCallsAreRejected()
        {
            var a = ScriptableObject.CreateInstance<PatternSequence>();
            var b = ScriptableObject.CreateInstance<PatternSequence>();
            try
            {
                a.clips.Add(new PatternClip { action = new CallPatternEvent { pattern = b }, duration = 2 });
                b.clips.Add(new PatternClip { action = new CallPatternEvent { pattern = a }, duration = 2 });
                Assert.IsNotEmpty(PatternValidation.Errors(a));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void RecursiveEncounterPatternCallsAreRejected()
        {
            var encounter = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                EncounterPattern pattern = encounter.phases[0].patterns[0];
                pattern.id = "recursive";
                pattern.minimumDuration = 1f;
                pattern.clips.Add(new PatternClip
                {
                    duration = 1f,
                    action = new CallEncounterPatternEvent { patternId = pattern.id }
                });
                Assert.IsNotEmpty(encounter.ValidateDefinition());
            }
            finally { Object.DestroyImmediate(encounter); }
        }
        [Test]
        public void ShippedBossPatternsLoadWithAllManagedEventTypes()
        {
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog");
            Assert.IsNotNull(catalog);
            Assert.IsNotEmpty(catalog.bosses);
            foreach (var boss in catalog.bosses)
            {
                Assert.IsNotNull(boss);
                Assert.IsNotEmpty(boss.phases);
                Assert.IsEmpty(boss.ValidateDefinition(), boss.displayName);
                foreach (var pattern in boss.AllPatterns())
                {
                    Assert.IsNotEmpty(pattern.id);
                    Assert.IsEmpty(PatternValidation.Errors(pattern, boss.FindPattern), pattern.name);
                }
            }
        }

        [Test]
        public void InlineEncounterPatternsSurviveEditorJsonRoundTrip()
        {
            var source = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            var copy = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            try
            {
                var helper = new EncounterPattern { id = "helper", name = "Helper" };
                helper.clips.Add(new PatternClip { duration = 0.2f, action = new WarningEvent() });
                source.libraryPatterns.Add(helper);
                source.phases[0].patterns[0].clips.Add(new PatternClip
                {
                    duration = helper.Duration,
                    action = new CallEncounterPatternEvent { patternId = helper.id }
                });
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source), copy);
                Assert.IsInstanceOf<CallEncounterPatternEvent>(copy.phases[0].patterns[0].clips[0].action);
                Assert.IsInstanceOf<WarningEvent>(copy.libraryPatterns[0].clips[0].action);
                Assert.IsEmpty(copy.ValidateDefinition());
            }
            finally { Object.DestroyImmediate(source); Object.DestroyImmediate(copy); }
        }
        [Test]
        public void ManagedReferencesSurviveEditorJsonRoundTrip()
        {
            var a = ScriptableObject.CreateInstance<PatternSequence>(); var b = ScriptableObject.CreateInstance<PatternSequence>();
            try
            {
                a.clips.Add(new PatternClip { start = 0.4f, duration = 2, action = new WarningEvent() });
                a.clips.Add(new PatternClip { start = 1, duration = 1, action = new DamageEvent() });
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(a), b);
                Assert.IsInstanceOf<WarningEvent>(b.clips[0].action); Assert.IsInstanceOf<DamageEvent>(b.clips[1].action);
                Assert.AreEqual(0.4f, b.clips[0].start);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }
    }
}
#endif
