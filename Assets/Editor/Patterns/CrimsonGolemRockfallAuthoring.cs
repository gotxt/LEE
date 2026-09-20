#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NHN.TraceStrike.Effects;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    // Explicit additive authoring command; not a runtime pattern dispatcher or import callback.
    public static class CrimsonGolemRockfallAuthoring
    {
        public const string Id = "p1-alternating-rockfall";
        public const string Path = "Assets/Resources/Patterns/CrimsonGolem.asset";
        const string Art = "Assets/Art/Bosses/CrimsonGolem";
        const string Baseline = "E0339B62CDE92884B331A596005A54686FA24241C862DE5E31D56DA0FA82E9B4";

        [MenuItem("Trace Strike/Patterns/Add Alternating Rockfall")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Leave Play Mode before adding encounter content.");
            var boss = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(Path);
            if (boss.FindPattern(Id) != null) { Debug.Log("Alternating Rockfall already exists; edit it in Boss Encounter Editor."); return; }
            if (EditorUtility.IsDirty(boss)) throw new InvalidOperationException("Save your pending boss edits before adding Rockfall.");
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path))).Replace("-", "") != Baseline)
                    throw new InvalidOperationException("Encounter differs from the reviewed baseline. Reconcile changes before adding Rockfall.");
            if (boss.bossVisual.prefab?.Animator == null) throw new InvalidOperationException("Existing golem Animator is required.");
            var states = BossAnimatorOptions.States(boss.bossVisual.prefab, boss.bossVisual.animationLayer);
            if (!states.Contains("Base Layer.FistWindup") || !states.Contains("Base Layer.FistContact"))
                throw new InvalidOperationException("Existing slam states were not found.");
            string before = EditorJsonUtility.ToJson(boss);
            var floor = boss.arena.GetCells().OrderBy(c => c.y).ThenBy(c => c.x).ToList();
            var groups = new[] { floor.Where(c => ((c.x / 2 + c.y / 2) & 1) == 0).ToList(),
                floor.Where(c => ((c.x / 2 + c.y / 2) & 1) != 0).ToList() };
            ValidateEscape(floor, groups);
            var falling = Effect("RockfallFalling", true);
            var impact = Effect("RockfallImpact", false);
            var warningSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Effects/Audio/Warning.wav");
            var impactSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Effects/Audio/Impact.wav");
            var pattern = new EncounterPattern { id = Id, name = "교차 낙석 · Alternating Rockfall", minimumDuration = 4.6f };
            float[] starts = { 0, 2.2f }, hits = { 1.2f, 3.4f };
            for (int i = 0; i < 2; i++)
            {
                string key = i == 0 ? "rockfall_a" : "rockfall_b";
                string name = i == 0 ? "1 · 빨간 묶음 낙석" : "2 · 빈 묶음 반전 낙석";
                Add(pattern, name + " 준비", starts[i], 1.2f, new BossAnimationEvent { state = "Base Layer.FistWindup", transition = 0 });
                Add(pattern, name + " 착지", hits[i], 1.2f, new BossAnimationEvent { state = "Base Layer.FistContact", transition = 0 });
                Func<TileSelection> area = () => new TileSelection { shape = TileShape.Cells, anchor = TileAnchor.Absolute,
                    cells = new List<Vector2Int>(groups[i]), snapshotKey = key, ensureEscape = false };
                var warning = Add(pattern, "경고", starts[i], 1.2f, new WarningEvent { tiles = area(), color = new Color(1, .18f, .24f, .6f) }, key);
                warning.attackName = name;
                Add(pattern, "경고 소리", starts[i], 1.2f, new SfxEvent { clip = warningSound, volume = .4f }, key);
                // The falling stone is visual only and reaches the ground at this group's impact boundary.
                Add(pattern, "낙석 하강 VFX", hits[i] - .35f, .35f, new VfxEvent { tiles = area(), prefab = falling }, key, true);
                Add(pattern, "타격", hits[i], .3f, new DamageEvent { tiles = area(), escapeGrace = .18f, reason = name }, key, true);
                Add(pattern, "낙석 착지 VFX", hits[i], .4f, new VfxEvent { tiles = area(), prefab = impact }, key, true);
                Add(pattern, "타격 소리", hits[i], .3f, new SfxEvent { clip = impactSound, volume = .65f }, key, true);
                Add(pattern, "카메라", hits[i], .3f, new CameraEvent { shake = 7 }, key, true);
            }
            if (AttackStepEditing.Find(pattern).Count != 2) throw new InvalidOperationException("Simple attack grouping failed.");
            // Validate on an in-memory copy first. The actual encounter is mutated only after all checks succeed.
            var probe = UnityEngine.Object.Instantiate(boss);
            try
            {
                probe.phases[0].patterns.Add(pattern);
                var errors = probe.ValidateDefinition();
                if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
            if (before != EditorJsonUtility.ToJson(boss)) throw new InvalidOperationException("Encounter changed during preparation.");
            Undo.RecordObject(boss, "Add Alternating Rockfall");
            boss.phases[0].patterns.Add(pattern);
            EditorUtility.SetDirty(boss); AssetDatabase.SaveAssetIfDirty(boss);
            Debug.Log($"ROCKFALL COMPLETE: appended P1 pattern, A={groups[0].Count}, B={groups[1].Count}, 18 events; existing data preserved.");
        }

        static void ValidateEscape(List<Vector2Int> floor, List<Vector2Int>[] groups)
        {
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
            var walkable = new HashSet<Vector2Int>(floor);
            foreach (var group in groups)
            {
                var danger = new HashSet<Vector2Int>(group);
                var distances = floor.Where(c => !danger.Contains(c)).ToDictionary(c => c, c => 0);
                var queue = new Queue<Vector2Int>(distances.Keys);
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var d in directions)
                        if (walkable.Contains(cell + d) && !distances.ContainsKey(cell + d))
                        { distances[cell + d] = distances[cell] + 1; queue.Enqueue(cell + d); }
                }
                foreach (var cell in group)
                    if (!distances.ContainsKey(cell) || distances[cell] > 2)
                        throw new InvalidOperationException("Escape needs more than two steps at " + cell + "; review the painted floor.");
                Debug.Log("ROCKFALL ESCAPE: max " + distances.Values.Max() + " steps; " + distances.Values.Count(v => v == 2) + " cells require two steps.");
            }
        }

        static PatternClip Add(EncounterPattern pattern, string label, float start, float duration, PatternEvent action,
            string key = "", bool atImpact = false)
        {
            var clip = new PatternClip { label = label, start = start, duration = duration, action = action,
                attackGroupKey = key, attackAtImpact = atImpact };
            pattern.clips.Add(clip); return clip;
        }

        static GameObject Effect(string name, bool falling)
        {
            string path = Art + "/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) throw new InvalidOperationException("Do not overwrite existing effect: " + path);
            var root = new GameObject(name, typeof(RectTransform));
            try
            {
                var effect = root.AddComponent<UiEffectPlayer>();
                if (falling)
                    effect.emitters.Add(new UiEffectEmitter { motion = UiEffectMotion.Chunk, count = 1, tileSizeRatio = .38f,
                        colors = new[] { new Color(.45f, .39f, .3f, 1) }, offset = new Vector2(0, effect.referenceCellSize * 1.75f),
                        lifetime = new Vector2(.35f, .35f), velocityY = Vector2.one * (-effect.referenceCellSize * 1.75f / .35f),
                        gravity = Vector2.zero, spin = Vector2.zero });
                else
                {
                    effect.emitters.Add(new UiEffectEmitter { motion = UiEffectMotion.Flash, count = 1, tileSizeRatio = .7f,
                        colors = new[] { new Color(.7f, .43f, .24f, .85f) }, lifetime = new Vector2(.22f, .22f) });
                    effect.emitters.Add(new UiEffectEmitter { motion = UiEffectMotion.Chunk, count = 1, tileSizeRatio = .3f,
                        colors = new[] { new Color(.4f, .34f, .28f, 1) }, lifetime = new Vector2(.4f, .4f),
                        velocityX = new Vector2(-35, 35), velocityY = new Vector2(25, 45), gravity = new Vector2(200, 200) });
                }
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
#endif
