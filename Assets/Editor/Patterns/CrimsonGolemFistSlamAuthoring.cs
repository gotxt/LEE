#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NHN.TraceStrike.Effects;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    // Explicit, one-time content migration. Never invoked on import and never rebuilds an encounter.
    public static class CrimsonGolemFistSlamAuthoring
    {
        public const string PatternId = "p1-fist-ripple";
        public const string Art = "Assets/Art/Bosses/CrimsonGolem";
        public const string EncounterPath = "Assets/Resources/Patterns/BossData_CrimsonGolem.asset";
        const string Baseline = "23CFE68E71E0BCD2B8C37DEABAA30173845BE1FE7B0CB81E46317B920A22AEC6";

        public static void Build()
        {
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(EncounterPath))).Replace("-", "") != Baseline)
                    throw new InvalidOperationException("Encounter changed since the reviewed baseline; reconcile edits before authoring.");
            var boss = AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(EncounterPath);
            if (boss.FindPattern(PatternId) != null || boss.bossVisual.prefab != null)
                throw new InvalidOperationException("Fist content or a boss prefab already exists. Edit it with the encounter editor.");
            string arenaBefore = JsonUtility.ToJson(boss.arena);
            var patternsBefore = boss.AllPatterns().Select(JsonUtility.ToJson).ToArray();
            var sprites = ImportFrames();
            var actor = CreateActor(sprites);
            var colors = new[] { new Color(1, .12f, .2f, .95f), new Color(.85f, .12f, .62f, .95f), new Color(.52f, .2f, 1, .95f) };
            var effects = Enumerable.Range(0, 3).Select(i => CreateWaveEffect(i + 1, colors[i])).ToArray();
            var pattern = new EncounterPattern { id = PatternId, name = "주먹 내려치기 · Fist Ripple", minimumDuration = 3.2f };
            // The contact state starts at exactly the damage boundary, before the damage clip.
            // The contact frame is selected at that boundary; extreme frame hitches can still skip rendered poses.
            Add(pattern, "주먹 들어올리기", 0, 1.2f, new BossAnimationEvent { state = "Base Layer.FistWindup", transition = 0 });
            Add(pattern, "주먹 착지 / 회복", 1.2f, 2, new BossAnimationEvent { state = "Base Layer.FistContact", transition = 0 });
            var warningSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Effects/Audio/Warning.wav");
            var impactSound = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Effects/Audio/Impact.wav");
            for (int i = 0; i < 3; i++)
            {
                float hit = 1.2f + i * .55f;
                string key = "fist_ripple_" + (i + 1);
                string name = new[] { "1 · 붉은 내측 파동", "2 · 자주색 중간 파동", "3 · 보라색 외측 파동" }[i];
                Func<TileSelection> area = () => new TileSelection { shape = TileShape.Diamond, anchor = TileAnchor.Absolute,
                    offset = new Vector2Int(19, 8), radius = i + 2, snapshotKey = key, ensureEscape = false };
                var warning = Add(pattern, "경고", hit - 1, 1, new WarningEvent { tiles = area(), color = colors[i] }, key);
                warning.attackName = name;
                Add(pattern, "경고 소리", hit - 1, 1, new SfxEvent { clip = warningSound, volume = .35f }, key);
                Add(pattern, "타격", hit, .3f, new DamageEvent { tiles = area(), escapeGrace = .18f, reason = name }, key, true);
                Add(pattern, "파동 VFX", hit, .35f, new VfxEvent { tiles = area(), prefab = effects[i], color = colors[i] }, key, true);
                Add(pattern, "타격 소리", hit, .3f, new SfxEvent { clip = impactSound, volume = .65f - i * .1f }, key, true);
                Add(pattern, "카메라", hit, .3f, new CameraEvent { shake = 9 - i * 2 }, key, true);
            }
            Add(pattern, "착지 먼지 (시각 효과만)", 1.2f, .5f, new VfxEvent {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Effects/Prefabs/Impact/VFX_DirtAreaExplosion.prefab"),
                tiles = new TileSelection { anchor = TileAnchor.Absolute, offset = new Vector2Int(19, 8), snapshotKey = "fist_contact_visual" }
            });
            Undo.RecordObject(boss, "Add Crimson Golem fist ripple");
            boss.bossVisual.prefab = actor;
            boss.bossVisual.idleState = "Base Layer.Idle";
            // Preserve the authored position (19,8), size, map, endpoints, phases and existing pattern order.
            boss.phases[0].patterns.Add(pattern);
            if (arenaBefore != JsonUtility.ToJson(boss.arena) ||
                !patternsBefore.SequenceEqual(boss.AllPatterns().Where(p => p.id != PatternId).Select(JsonUtility.ToJson)))
                throw new InvalidOperationException("Unrelated encounter content changed.");
            var errors = boss.ValidateDefinition();
            if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
            if (AttackStepEditing.Find(pattern).Count != 3) throw new InvalidOperationException("Simple attack grouping lost.");
            EditorUtility.SetDirty(boss);
            AssetDatabase.SaveAssetIfDirty(boss);
            Debug.Log("FIST RIPPLE authored: P1 appended, 3 attack groups / 21 events, existing content preserved.");
        }

        static PatternClip Add(EncounterPattern pattern, string label, float start, float duration, PatternEvent action,
            string key = "", bool impact = false)
        {
            var clip = new PatternClip { label = label, start = start, duration = duration, action = action,
                attackGroupKey = key, attackAtImpact = impact };
            pattern.clips.Add(clip); return clip;
        }

        static Sprite[] ImportFrames()
        {
            string path = Art + "/CaveGolemGroundSlam.png";
            var raw = new Texture2D(2, 2);
            if (!raw.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Sprite atlas could not be read.");
            if (raw.width != 960 || raw.height != 80) throw new InvalidOperationException("Expected the supplied 12 x 80px sprite sheet.");
            var metadata = new SpriteMetaData[12];
            for (int part = 0; part < 12; part++)
            {
                metadata[part] = new SpriteMetaData { name = "Slam_" + part.ToString("D2"),
                    rect = new Rect(part * 80, 0, 80, 80),
                    alignment = (int)SpriteAlignment.Custom, pivot = new Vector2(.5f, .125f) };
            }
            if (raw.GetPixel(0, 0).a > .01f) throw new InvalidOperationException("Sheet background must be transparent.");
            UnityEngine.Object.DestroyImmediate(raw);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 24;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
#pragma warning disable 618
            importer.spritesheet = metadata;
#pragma warning restore 618
            importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        }

        static BossActor CreateActor(Sprite[] sprites)
        {
            var idle = Clip("Idle");
            Frames(idle, sprites, new[] { 0f, 1f }, new[] { 0, 0 });
            var settings = AnimationUtility.GetAnimationClipSettings(idle); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(idle, settings);
            var windup = Clip("FistWindup");
            Frames(windup, sprites, new[] { 0f, .2f, .4f, .6f, .8f, 1.04f, 1.12f, 1.2f }, new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            var contact = Clip("FistContact");
            Frames(contact, sprites, new[] { 0f, .12f, .28f, .48f, .7f, .94f, 1.2f, 2f }, new[] { 7, 7, 8, 9, 10, 11, 0, 0 });
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Art + "/BossAnimator_CrimsonGolem.controller");
            foreach (var clip in new[] { idle, windup, contact })
            {
                AssetDatabase.CreateAsset(clip, Art + "/" + clip.name + ".anim");
                var state = controller.layers[0].stateMachine.AddState(clip.name); state.motion = clip;
                if (clip == idle) controller.layers[0].stateMachine.defaultState = state;
            }
            AssetDatabase.SaveAssetIfDirty(controller);
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) throw new InvalidOperationException("Sprite unlit shader missing.");
            var material = new Material(shader); AssetDatabase.CreateAsset(material, Art + "/BossMaterial_CrimsonGolem.mat");
            var root = new GameObject("BossVisual_CrimsonGolem");
            try
            {
                var actor = root.AddComponent<BossActor>();
                var rig = new GameObject("Body"); rig.transform.SetParent(root.transform, false);
                var renderer = rig.AddComponent<SpriteRenderer>(); renderer.sprite = sprites[0]; renderer.sharedMaterial = material;
                // Frame animation has no moving bone sockets. Only a fixed ground impact origin is provided.
                var socket = new GameObject("GroundImpact"); socket.transform.SetParent(root.transform, false);
                actor.sockets.Add(new BossSocket { name = "GroundImpact", target = socket.transform });
                actor.animator = rig.AddComponent<Animator>(); actor.animator.runtimeAnimatorController = controller;
                return PrefabUtility.SaveAsPrefabAsset(root, Art + "/BossVisual_CrimsonGolem.prefab").GetComponent<BossActor>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static AnimationClip Clip(string name) => new AnimationClip { name = name, frameRate = 60 };
        static void Frames(AnimationClip clip, Sprite[] sprites, float[] times, int[] indices)
        {
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, binding,
                times.Select((t, i) => new ObjectReferenceKeyframe { time = t, value = sprites[indices[i]] }).ToArray());
        }

        static GameObject CreateWaveEffect(int wave, Color color)
        {
            var root = new GameObject("VFX_CrimsonGolem_FistRipple" + wave, typeof(RectTransform));
            try
            {
                var effect = root.AddComponent<UiEffectPlayer>();
                effect.emitters.Add(new UiEffectEmitter { motion = UiEffectMotion.Flash, tileSizeRatio = .85f,
                    lifetime = new Vector2(.35f, .35f), colors = new[] { color } });
                effect.emitters.Add(new UiEffectEmitter { motion = UiEffectMotion.Spark, count = 3,
                    size = new Vector2(7, 12), lifetime = new Vector2(.18f, .3f), colors = new[] { color }, speed = new Vector2(35, 80) });
                return PrefabUtility.SaveAsPrefabAsset(root, Art + "/" + root.name + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
#endif
