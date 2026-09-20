using System;
using System.Collections.Generic;
using NHN.TraceStrike.Patterns;
using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike
{
    public sealed partial class TraceStrikeGame : IPatternHost
    {
        private BossEncounterDefinition activeBoss;
        private int activePhaseIndex;
        private BossPhaseDefinition ActivePhase => activeBoss.phases[activePhaseIndex];
        private bool HasNextBossPhase => activeBoss != null && activePhaseIndex + 1 < activeBoss.phases.Count;
        private string ActiveBossName => activeBoss != null ? activeBoss.displayName : "크림슨 골렘";
        private float ActiveBattleCameraZoom => activeBoss != null && activeBoss.arena != null
            ? activeBoss.arena.cameraZoom : BattleCameraZoom;
        private float ActiveBattlePlayerSizeRatio => activeBoss != null && activeBoss.arena != null
            ? activeBoss.arena.playerSizeRatio : BattlePlayerSizeRatio;
        private PatternRunner timeline, backgroundTimeline;
        private int timelineVersion = -1, patternCursor;
        private float timelineWait;
        private string pendingTimelineDamage;
        public bool IsPatternPreview { get; private set; }
        private readonly Dictionary<int, HashSet<Vector2Int>> timelineWalls = new Dictionary<int, HashSet<Vector2Int>>();
        private readonly Dictionary<int, HashSet<Vector2Int>> timelineDanger = new Dictionary<int, HashSet<Vector2Int>>();
        private readonly Dictionary<int, KeyValuePair<HashSet<Vector2Int>, string>> timelineHazards =
            new Dictionary<int, KeyValuePair<HashSet<Vector2Int>, string>>();
        private readonly Dictionary<string, Transform> timelineObjects = new Dictionary<string, Transform>();
        private readonly Dictionary<int, Vector2> timelineCamera = new Dictionary<int, Vector2>();
        private int leaseId;
        public event Action<string, string> PatternSignal;

        private sealed class Lease : IPatternLease
        {
            private Action cleanup;
            private readonly Action<float> update;
            public Lease(Action cleanup, Action<float> update = null) { this.cleanup = cleanup; this.update = update; }
            public void SetProgress(float t) { if (cleanup != null) update?.Invoke(Mathf.Clamp01(t)); }
            public void Dispose() { var action = cleanup; cleanup = null; action?.Invoke(); }
        }

        private void ConfigureBoss(int index)
        {
            StopMechanics();
            IsPatternPreview = false;
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog");
            if (catalog == null || index < 0 || index >= catalog.bosses.Count || catalog.bosses[index] == null)
                throw new InvalidOperationException("Missing boss catalog entry. Use Trace Strike/Patterns/Create Crimson Golem Encounter.");
            activeBoss = catalog.bosses[index];
            var errors = activeBoss.ValidateDefinition();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            activePhaseIndex = 0;
        }

        private int StartingBoss()
        {
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog");
            return catalog != null ? catalog.startingBoss : 0;
        }

        private void ApplyBossPortrait()
        {
            if (activeBoss.portrait == null || arenaBossCore == null) return;
            var image = arenaBossCore.GetComponent<Image>();
            if (image != null) image.sprite = activeBoss.portrait;
        }

        private void CancelTimeline()
        {
            try { timeline?.Dispose(); } catch (Exception error) { Debug.LogException(error, this); }
            timeline = null;
            try { backgroundTimeline?.Dispose(); } catch (Exception error) { Debug.LogException(error, this); }
            backgroundTimeline = null;
            timelineVersion = -1;
            pendingTimelineDamage = null;
        }

        private void OnDisable() { CancelTimeline(); StopMechanics(); }

        private void TickTimeline()
        {
            if (activeBoss == null || titleActive || tutorialActive || hubActive || playerDead || gameCleared) return;
            if (timelineVersion != patternVersion)
            {
                CancelTimeline();
                timelineVersion = patternVersion;
                timelineWait = ActivePhase.initialDelay;
                patternCursor = 0;
            }
            if (inputLocked) return; // Single scaled gameplay clock for all timeline events.
            try
            {
                float dt = Time.deltaTime;
                if (!IsPatternPreview) mechanicSession?.Advance(dt);
                if (!IsPatternPreview && ActivePhase.backgroundEnabled &&
                    ActivePhase.background != null && ActivePhase.background.enabled)
                {
                    if (backgroundTimeline == null || backgroundTimeline.IsComplete)
                        backgroundTimeline = NewRunner(ActivePhase.background);
                    backgroundTimeline.Advance(dt);
                }
                if (timeline != null && !timeline.IsComplete) timeline.Advance(dt);
                else if (!IsPatternPreview)
                {
                    if (timeline != null)
                    {
                        timeline = null;
                        bossAttackCount++;
                        timelineWait = Mathf.Max(ActivePhase.minimumInterval, ActivePhase.interval - bossAttackCount * ActivePhase.acceleration);
                    }
                    timelineWait -= dt;
                    if (timelineWait <= 0)
                    {
                        EncounterPattern pattern = ChooseNextPattern();
                        if (pattern == null) timelineWait = 0.1f;
                        else
                        {
                            statusText.text = pattern.name;
                            timeline = NewRunner(pattern);
                            timeline.Advance(0);
                        }
                    }
                }
                foreach (var hazard in timelineHazards.Values)
                    if (hazard.Key.Contains(model.Player)) { pendingTimelineDamage = hazard.Value; break; }
                if (pendingTimelineDamage != null)
                {
                    string reason = pendingTimelineDamage;
                    StartCoroutine(KillPlayer(reason));
                }
            }
            catch (Exception error)
            {
                CancelTimeline();
                StopMechanics();
                inputLocked = true;
                Debug.LogException(error, this);
                statusText.text = "Pattern configuration error — see Console";
            }
        }

        private EncounterPattern ChooseNextPattern()
        {
            int enabledCount = 0;
            foreach (EncounterPattern pattern in ActivePhase.patterns)
                if (pattern != null && pattern.enabled) enabledCount++;
            if (enabledCount == 0) return null;

            int target = ActivePhase.shuffle
                ? UnityEngine.Random.Range(0, enabledCount)
                : patternCursor++ % enabledCount;
            foreach (EncounterPattern pattern in ActivePhase.patterns)
                if (pattern != null && pattern.enabled && target-- == 0) return pattern;
            return null;
        }

        private PatternRunner NewRunner(EncounterPattern pattern) =>
            new PatternRunner(pattern, new PatternContext(this,
                ((IPatternHost)this).CenterCell, 0, activeBoss.FindPattern));

        private PatternRunner NewRunner(PatternSequence pattern) =>
            new PatternRunner(pattern, new PatternContext(this,
                ((IPatternHost)this).CenterCell, 0, activeBoss.FindPattern));

        public void PreviewPattern(PatternSequence pattern)
        {
            StartStage(stage);
            CancelTimeline();
            StopMechanics();
            IsPatternPreview = true;
            bossPhaseSkipped = true;
            stageTimerRunning = false;
            timelineVersion = patternVersion;
            timeline = NewRunner(pattern);
            inputLocked = false;
        }

        public void PreviewPattern(BossEncounterDefinition encounter, EncounterPattern pattern)
        {
            if (encounter == null || pattern == null) return;
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog");
            int catalogIndex = catalog != null ? catalog.bosses.IndexOf(encounter) : -1;
            if (catalogIndex < 0)
                throw new InvalidOperationException("Add the encounter to BossCatalog before Play Mode preview.");
            StartStage(catalogIndex);
            CancelTimeline();
            StopMechanics();
            IsPatternPreview = true;
            bossPhaseSkipped = true;
            stageTimerRunning = false;
            timelineVersion = patternVersion;
            timeline = NewRunner(pattern);
            inputLocked = false;
        }

        public void StopPatternPreview() => StartStage(stage);

        Vector2Int IPatternHost.PlayerCell => model.Player;
        Vector2Int IPatternHost.CenterCell => model.CenterCell;
        IReadOnlyCollection<Vector2Int> IPatternHost.Walkable => model.Walkable;
        IReadOnlyCollection<Vector2Int> IPatternHost.Traversable => model.Traversable;
        bool IPatternHost.IsAlive => !playerDead && !gameCleared && pendingTimelineDamage == null;
        void IPatternHost.Damage(string reason) { pendingTimelineDamage = reason; }
        void IPatternHost.Signal(string name, string argument) => PatternSignal?.Invoke(name, argument);

        IPatternLease IPatternHost.Mark(IReadOnlyCollection<Vector2Int> cells, Color color, bool warning)
        {
            int id = ++leaseId;
            timelineDanger[id] = new HashSet<Vector2Int>(cells);
            var objects = new List<RectTransform>();
            var warnings = new List<TileWarningVisual>();
            foreach (var cell in cells)
            {
                RectTransform rect;
                if (warning)
                {
                    var view = CreateTileWarning(mainGrid, "Timeline Warning");
                    view.SetColor(color);
                    view.SetProgress(0.15f);
                    warnings.Add(view);
                    rect = (RectTransform)view.transform;
                }
                else
                {
                    rect = CreateRect("Timeline Damage", mainGrid);
                    var graphic = rect.gameObject.AddComponent<Image>();
                    graphic.color = color;
                    graphic.raycastTarget = false;
                }
                rect.anchoredPosition = GridPosition(cell.x, cell.y, mainCellSize);
                rect.sizeDelta = Vector2.one * (mainCellSize - 10);
                objects.Add(rect);
            }
            return new Lease(() => { timelineDanger.Remove(id); foreach (var rect in objects) if (rect != null) Destroy(rect.gameObject); },
                t => { foreach (var view in warnings) if (view != null) view.SetProgress(Mathf.Lerp(0.15f, 1, t)); });
        }

        IPatternLease IPatternHost.Hazard(IReadOnlyCollection<Vector2Int> cells, string reason)
        {
            int id = ++leaseId;
            var copy = new HashSet<Vector2Int>(cells);
            timelineHazards.Add(id, new KeyValuePair<HashSet<Vector2Int>, string>(copy, reason));
            var visual = ((IPatternHost)this).Mark(copy, new Color(0.9f, 0.15f, 0.6f, 0.65f), false);
            // Includes immediate contact even if the entire clip fits within one frame.
            if (copy.Contains(model.Player)) pendingTimelineDamage = reason;
            return new Lease(() => { timelineHazards.Remove(id); visual.Dispose(); });
        }

        IPatternLease IPatternHost.Block(IReadOnlyCollection<Vector2Int> cells)
        {
            var accepted = new HashSet<Vector2Int>(cells);
            accepted.IntersectWith(model.Walkable);
            accepted.Remove(model.Player); accepted.Remove(model.Start); accepted.Remove(model.End);
            accepted.ExceptWith(model.Trail);
            if (mechanicSession != null) accepted.ExceptWith(mechanicSession.RequiredCells);
            var combined = CombinedWalls(); combined.UnionWith(accepted);
            if (!IsConnectedWithout(combined) || !model.HasEndpointPair(combined))
            { Debug.LogWarning("Pattern wall rejected: would disconnect the arena or exhaust START/END regions."); return new Lease(() => { }); }
            int id = ++leaseId;
            timelineWalls.Add(id, accepted);
            model.SetBlockedCells(combined);
            var visual = ((IPatternHost)this).Mark(accepted, new Color(0.25f, 0.3f, 0.4f, 1), false);
            return new Lease(() => { timelineWalls.Remove(id); model.SetBlockedCells(CombinedWalls()); visual.Dispose(); });
        }

        private HashSet<Vector2Int> CombinedWalls()
        {
            var combined = new HashSet<Vector2Int>(crystalCells);
            foreach (var wall in timelineWalls.Values) combined.UnionWith(wall);
            return combined;
        }

        private bool IsConnectedWithout(HashSet<Vector2Int> blocked)
        {
            var cells = new HashSet<Vector2Int>(model.Walkable); cells.ExceptWith(blocked);
            if (cells.Count == 0) return false;
            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            queue.Enqueue(model.Player);
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                if (!cells.Contains(cell) || !visited.Add(cell)) continue;
                queue.Enqueue(cell + Vector2Int.up); queue.Enqueue(cell + Vector2Int.down);
                queue.Enqueue(cell + Vector2Int.left); queue.Enqueue(cell + Vector2Int.right);
            }
            return visited.Count == cells.Count;
        }

        IPatternLease IPatternHost.Spawn(string key, GameObject prefab, Vector2Int cell, Sprite sprite, Color color)
        {
            if (timelineObjects.ContainsKey(key)) throw new InvalidOperationException("Duplicate live spawn key: " + key);
            GameObject instance;
            if (prefab != null)
            {
                instance = Instantiate(prefab, mainGrid, false);
                instance.transform.localPosition = GridPosition(cell.x, cell.y, mainCellSize);
                // Only our UI effect prefabs opt into tile-relative sizing.
                // Unrelated user prefabs retain their own transforms and behaviour.
                var uiEffect = instance.GetComponent<Effects.UiEffectPlayer>();
                if (uiEffect != null) uiEffect.Play(mainCellSize);
            }
            else
            {
                var rect = CreateRect("Pattern Object", mainGrid);
                rect.anchoredPosition = GridPosition(cell.x, cell.y, mainCellSize);
                rect.sizeDelta = Vector2.one * mainCellSize * 0.7f;
                instance = rect.gameObject;
                var image = instance.AddComponent<Image>(); image.sprite = sprite; image.color = color; image.raycastTarget = false;
            }
            timelineObjects[key] = instance.transform;
            return new Lease(() => { timelineObjects.Remove(key); if (instance != null) Destroy(instance); });
        }

        IPatternLease IPatternHost.Sound(AudioClip clip, float volume)
        {
            var go = new GameObject("Pattern SFX"); go.transform.SetParent(transform);
            var source = go.AddComponent<AudioSource>(); source.clip = clip; source.volume = volume;
            source.spatialBlend = 0; source.Play();
            return new Lease(() => { if (go != null) Destroy(go); });
        }

        private sealed class MotionState { public Transform target; public Vector3 from, to, value; }
        private readonly Dictionary<Transform, List<MotionState>> motions = new Dictionary<Transform, List<MotionState>>();
        private readonly Dictionary<Transform, Vector3> motionBases = new Dictionary<Transform, Vector3>();
        IPatternLease IPatternHost.Motion(string key, Vector2Int target)
        {
            if (key == "$boss" && bossRenderStage != null)
                throw new InvalidOperationException("Use BossMotionEvent for the prefab boss. Legacy $boss movement targets the old UI portrait.");
            Transform obj = key == "$boss" ? arenaBossCore : timelineObjects.TryGetValue(key, out var found) ? found : null;
            if (obj == null) { Debug.LogWarning("Missing pattern movement target: " + key); return new Lease(() => { }); }
            if (!motions.TryGetValue(obj, out var list))
            { motions[obj] = list = new List<MotionState>(); motionBases[obj] = obj.localPosition; }
            Vector3 destination = mainGrid.TransformPoint(GridPosition(target.x, target.y, mainCellSize));
            var state = new MotionState { target = obj, from = obj.localPosition, value = obj.localPosition,
                to = obj.parent.InverseTransformPoint(destination) };
            list.Add(state);
            return new Lease(() => {
                list.Remove(state);
                if (obj != null) obj.localPosition = list.Count > 0 ? list[list.Count - 1].value : motionBases[obj];
                if (list.Count == 0) { motions.Remove(obj); motionBases.Remove(obj); }
            }, t => { state.value = Vector3.Lerp(state.from, state.to, t); if (obj != null && list[list.Count - 1] == state) obj.localPosition = state.value; });
        }

        IPatternLease IPatternHost.Camera(Vector2 offset, float shake)
        {
            int id = ++leaseId;
            timelineCamera[id] = offset;
            return new Lease(() => timelineCamera.Remove(id), t => timelineCamera[id] = offset +
                new Vector2(Mathf.Sin(t * 113), Mathf.Sin(t * 157)) * shake * (1 - t));
        }
        private Vector2 TimelineCameraOffset()
        {
            Vector2 offset = Vector2.zero;
            foreach (var value in timelineCamera.Values) offset += value;
            return offset;
        }
    }
}
