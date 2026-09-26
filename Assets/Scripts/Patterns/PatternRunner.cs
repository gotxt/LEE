using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    // Advances at every clip boundary, so frame hitches cannot skip short events.
    // Assets hold configuration only. Every execution owns new actions and resources.
    public sealed class PatternRunner : IDisposable
    {
        private sealed class Entry
        {
            public float start, end;
            public PatternAction action;
            public bool started, ended;
        }
        private readonly List<Entry> entries = new List<Entry>();
        private readonly PatternContext context;
        private readonly float duration;
        private bool disposed;
        public float Time { get; private set; }
        public bool IsComplete => disposed;

        public PatternRunner(PatternSequence sequence, PatternContext context)
            : this(ValidatedClips(sequence), sequence.Duration, context) { }

        public PatternRunner(EncounterPattern sequence, PatternContext context)
            : this(ValidatedClips(sequence, context), sequence.Duration, context) { }

        private static IReadOnlyList<PatternClip> ValidatedClips(PatternSequence sequence)
        {
            var errors = PatternValidation.Errors(sequence);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            return sequence.clips;
        }

        private static IReadOnlyList<PatternClip> ValidatedClips(EncounterPattern sequence,
            PatternContext context)
        {
            var errors = PatternValidation.Errors(sequence, context.ResolveEncounterPattern);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            context.InitializeLocations(sequence.locationGroups);
            return sequence.clips;
        }

        // Data-only entry point for procedural pattern producers and headless tests.
        public PatternRunner(IReadOnlyList<PatternClip> clips, float duration, PatternContext context)
        {
            if (context.Depth > 16) throw new InvalidOperationException("Pattern call depth exceeded.");
            if (duration < 0 || float.IsNaN(duration) || float.IsInfinity(duration)) throw new ArgumentOutOfRangeException(nameof(duration));
            this.context = context;
            this.duration = duration;
            foreach (var clip in clips)
                if (clip != null && clip.enabled)
                {
                    if (clip.action == null || clip.start < 0 || clip.duration < 0 ||
                        float.IsNaN(clip.start) || float.IsInfinity(clip.start) ||
                        float.IsNaN(clip.duration) || float.IsInfinity(clip.duration) || clip.start + clip.duration > duration + 0.00001f)
                        throw new ArgumentException("Invalid clip or schedule duration.");
                    var eventErrors = new List<string>();
                    clip.action.Validate(eventErrors, clip.duration);
                    if (eventErrors.Count > 0) throw new ArgumentException(string.Join("\n", eventErrors));
                    entries.Add(new Entry { start = clip.start, end = clip.start + clip.duration,
                        action = clip.action.Create(context, clip.duration) });
                }
        }

        public void Advance(float delta)
        {
            if (disposed) return;
            if (float.IsNaN(delta) || float.IsInfinity(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            try
            {
                Boundary();
                float target = Mathf.Min(duration, Time + delta);
                while (!disposed && Time < target && context.Host.IsAlive)
                {
                    float next = target;
                    foreach (var entry in entries)
                    {
                        if (!entry.started && entry.start > Time) next = Mathf.Min(next, entry.start);
                        if (entry.started && !entry.ended && entry.end > Time) next = Mathf.Min(next, entry.end);
                    }
                    float step = next - Time;
                    Time = next;
                    foreach (var entry in entries)
                        if (entry.started && !entry.ended && context.Host.IsAlive)
                            entry.action.Tick(Time - entry.start, step);
                    Boundary();
                }
                if (Time >= duration || !context.Host.IsAlive) Dispose();
            }
            catch { Dispose(); throw; }
        }

        private void Boundary()
        {
            // Serialized order resolves simultaneous starts. End old intervals first.
            foreach (var entry in entries)
                if (entry.started && !entry.ended && entry.end <= Time)
                { entry.ended = true; entry.action.End(false); }
            foreach (var entry in entries)
            {
                if (entry.started || entry.start > Time || !context.Host.IsAlive) continue;
                entry.started = true;
                entry.action.Begin();
                entry.action.Tick(0, 0);
                if (entry.end <= Time) { entry.ended = true; entry.action.End(false); }
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            List<Exception> errors = null;
            foreach (var entry in entries)
                if (entry.started && !entry.ended)
                {
                    entry.ended = true;
                    try { entry.action.End(true); }
                    catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); }
                }
            try { context.Dispose(); }
            catch (Exception error) { if (errors == null) errors = new List<Exception>(); errors.Add(error); }
            if (errors != null) throw new AggregateException(errors);
        }
    }
}
