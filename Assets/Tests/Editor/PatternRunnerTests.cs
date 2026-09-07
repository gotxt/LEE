#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class PatternRunnerTests
    {
        private sealed class TestLease : IPatternLease
        {
            private Action cleanup;
            public TestLease(Action cleanup) { this.cleanup = cleanup; }
            public void SetProgress(float p) { }
            public void Dispose() { var action = cleanup; cleanup = null; action?.Invoke(); }
        }
        private sealed class Host : IPatternHost
        {
            public Vector2Int player;
            public int hits, live, released;
            public readonly List<string> events = new List<string>();
            public readonly List<Vector2Int> cells = new List<Vector2Int> { Vector2Int.zero, Vector2Int.up, Vector2Int.right };
            public Vector2Int PlayerCell => player;
            public Vector2Int CenterCell => Vector2Int.zero;
            public IReadOnlyCollection<Vector2Int> Walkable => cells;
            public IReadOnlyCollection<Vector2Int> Traversable => cells;
            public bool IsAlive => true;
            private IPatternLease Lease() { live++; return new TestLease(() => { live--; released++; }); }
            public IPatternLease Mark(IReadOnlyCollection<Vector2Int> c, Color color, bool warning) => Lease();
            public IPatternLease Block(IReadOnlyCollection<Vector2Int> c) => Lease();
            public IPatternLease Hazard(IReadOnlyCollection<Vector2Int> c, string reason) => Lease();
            public IPatternLease Spawn(string key, GameObject p, Vector2Int cell, Sprite s, Color color) => Lease();
            public IPatternLease Sound(AudioClip clip, float volume) => Lease();
            public IPatternLease Motion(string key, Vector2Int cell) => Lease();
            public IPatternLease Camera(Vector2 offset, float shake) => Lease();
            public void Damage(string reason) { hits++; }
            public void Signal(string name, string argument) { events.Add(name); }
        }
        private sealed class Probe : PatternEvent
        {
            public string name;
            public bool fail;
            public override PatternAction Create(PatternContext c, float duration) => new ProbeAction(c, name, fail);
        }
        private sealed class ProbeAction : PatternAction
        {
            private readonly PatternContext c; private readonly string name; private readonly bool fail;
            public ProbeAction(PatternContext c, string name, bool fail) { this.c = c; this.name = name; this.fail = fail; }
            public override void Begin() { c.Host.Signal(name + "+", ""); if (fail) throw new InvalidOperationException("test"); }
            public override void End(bool cancelled) => c.Host.Signal(name + (cancelled ? "!" : "-"), "");
        }
        private static PatternClip Clip(float start, float length, PatternEvent action) => new PatternClip { start = start, duration = length, action = action };
        private static PatternRunner Runner(Host h, float length, params PatternClip[] clips) => new PatternRunner(clips, length, new PatternContext(h, Vector2Int.zero));

        [Test]
        public void OverlapAndSameTimestampHaveDeterministicOrder()
        {
            var h = new Host();
            using (var runner = Runner(h, 3, Clip(0, 2, new Probe { name = "A" }), Clip(1, 2, new Probe { name = "B" }), Clip(2, 0, new Probe { name = "C" }))) runner.Advance(5);
            CollectionAssert.AreEqual(new[] { "A+", "B+", "A-", "C+", "C-", "B-" }, h.events);
        }
        [Test]
        public void HitchDoesNotSkipZeroOrShortEvents()
        {
            var h = new Host();
            using (var runner = Runner(h, 1, Clip(0.01f, 0.001f, new Probe { name = "A" }), Clip(0.5f, 0, new Probe { name = "B" }))) runner.Advance(10);
            CollectionAssert.AreEqual(new[] { "A+", "A-", "B+", "B-" }, h.events);
        }
        [Test]
        public void CancellationReleasesOverlappingPersistentResourcesOnce()
        {
            var h = new Host();
            var runner = Runner(h, 10, Clip(0, 0, new HazardEvent { persist = true }), Clip(0, 5, new ObstacleEvent()));
            runner.Advance(0); Assert.AreEqual(2, h.live);
            runner.Dispose(); runner.Dispose(); Assert.AreEqual(0, h.live); Assert.AreEqual(2, h.released);
        }
        [Test]
        public void ExplicitRemoveReleasesOnlyMatchingResourceKey()
        {
            var h = new Host();
            using (var runner = Runner(h, 3, Clip(0, 0, new HazardEvent { persist = true, key = "pool" }),
                Clip(0, 0, new ObstacleEvent { persist = true, key = "wall" }), Clip(1, 0, new RemoveResourceEvent { key = "pool" })))
            { runner.Advance(1); Assert.AreEqual(1, h.live); runner.Advance(2); Assert.AreEqual(0, h.live); }
        }
        [Test]
        public void NaturalCompletionCleansPersistentObjects()
        {
            var h = new Host();
            using (var runner = Runner(h, 1, Clip(0, 0, new SpawnEvent { persist = true })))
            { runner.Advance(0); Assert.AreEqual(1, h.live); runner.Advance(1); Assert.AreEqual(0, h.live); Assert.IsTrue(runner.IsComplete); }
        }
        [Test]
        public void DamageAllowsEscapeDuringGrace()
        {
            var h = new Host();
            using (var runner = Runner(h, 1, Clip(0, 0.3f, new DamageEvent())))
            { runner.Advance(0.1f); h.player = Vector2Int.right; runner.Advance(0.2f); Assert.AreEqual(0, h.hits); }
        }
        [Test]
        public void DamageDoesNotHitLateArrival()
        {
            var h = new Host { player = Vector2Int.right };
            using (var runner = Runner(h, 1, Clip(0, 0.3f, new DamageEvent())))
            { runner.Advance(0.1f); h.player = Vector2Int.zero; runner.Advance(0.2f); Assert.AreEqual(0, h.hits); }
        }
        [Test]
        public void DamageHitsOnceEvenWhenAdvancedAcrossWholeClip()
        {
            var h = new Host();
            using (var runner = Runner(h, 1, Clip(0, 0.3f, new DamageEvent()))) { runner.Advance(1); runner.Advance(1); Assert.AreEqual(1, h.hits); }
        }
        [Test]
        public void WarningSnapshotDoesNotFollowPlayerAndIsLocalToRun()
        {
            var h = new Host();
            var selection = new TileSelection { anchor = TileAnchor.Player, snapshotKey = "aim" };
            using (var c = new PatternContext(h, Vector2Int.zero))
            {
                Assert.IsTrue(selection.Resolve(c).Contains(Vector2Int.zero));
                h.player = Vector2Int.right; Assert.IsTrue(selection.Resolve(c).Contains(Vector2Int.zero));
                using (var c2 = new PatternContext(h, Vector2Int.zero)) Assert.IsTrue(selection.Resolve(c2).Contains(Vector2Int.right));
            }
        }
        [Test]
        public void RuntimeStateIsNotSharedBetweenSimultaneousRuns()
        {
            var action = new DamageEvent(); var h1 = new Host(); var h2 = new Host();
            using (var a = Runner(h1, 1, Clip(0, 0.3f, action))) using (var b = Runner(h2, 1, Clip(0, 0.3f, action)))
            { a.Advance(1); b.Advance(1); Assert.AreEqual(1, h1.hits); Assert.AreEqual(1, h2.hits); }
        }
        [Test]
        public void ExceptionCancelsStartedEventsAndResources()
        {
            var h = new Host();
            using (var runner = Runner(h, 2, Clip(0, 1, new WarningEvent()), Clip(0, 1, new Probe { name = "X", fail = true })))
            { Assert.Throws<InvalidOperationException>(() => runner.Advance(0)); Assert.IsTrue(runner.IsComplete); Assert.AreEqual(0, h.live); CollectionAssert.Contains(h.events, "X!"); }
        }
        [Test]
        public void DisabledEventNeverExecutes()
        {
            var h = new Host(); var clip = Clip(0, 1, new Probe { name = "X" }); clip.enabled = false;
            using (var r = Runner(h, 1, clip)) r.Advance(1);
            Assert.IsEmpty(h.events);
        }
        [Test]
        public void NegativeTimeAndOverlongClipAreRejected()
        {
            var h = new Host();
            Assert.Throws<ArgumentException>(() => Runner(h, 1, Clip(0, 2, new WaitEvent())));
            using (var r = Runner(h, 1)) Assert.Throws<ArgumentOutOfRangeException>(() => r.Advance(-1));
        }
        [Test]
        public void EncounterLocalCallsResolveAndRejectCycles()
        {
            var helper = new EncounterPattern { id = "helper", name = "Helper", minimumDuration = 0.2f };
            helper.clips.Add(Clip(0, 0.2f, new Probe { name = "H" }));
            var attack = new EncounterPattern { id = "attack", name = "Attack", minimumDuration = 0.2f };
            attack.clips.Add(Clip(0, 0.2f, new CallEncounterPatternEvent { patternId = helper.id }));
            var patterns = new Dictionary<string, EncounterPattern>
            { { helper.id, helper }, { attack.id, attack } };
            var h = new Host();
            using (var runner = new PatternRunner(attack,
                new PatternContext(h, Vector2Int.zero, 0, id => patterns[id])))
                runner.Advance(1f);
            CollectionAssert.AreEqual(new[] { "H+", "H-" }, h.events);

            helper.clips.Add(Clip(0, 0.2f,
                new CallEncounterPatternEvent { patternId = attack.id }));
            Assert.IsNotEmpty(PatternValidation.Errors(attack, id => patterns[id]));
        }
        [Test]
        public void CompactArenaStaysWithinRequestedSquare()
        {
            foreach (int size in new[] { 5, 7, 9, 11, 13, 15, 17 }) for (int shape = 0; shape < 3; shape++)
            {
                var model = new TrailFieldModel(); model.CreateField(shape, size); model.BeginRound(0);
                foreach (var cell in model.Walkable) { Assert.LessOrEqual(Mathf.Abs(cell.x - 8), size / 2); Assert.LessOrEqual(Mathf.Abs(cell.y - 8), size / 2); }
                Assert.AreNotEqual(model.Start, model.End);
            }
        }
    }
}
#endif
