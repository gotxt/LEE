#if UNITY_EDITOR
using System;
using System.IO;
using NHN.TraceStrike.Patterns;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Editor
{
    public static class PatternLibraryBuilder
    {
        private const string Root = "Assets/Resources/Patterns";

        [MenuItem("Trace Strike/Patterns/Create Crimson Golem Encounter")]
        public static void CreateLibrary()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            string encounterPath = Root + "/CrimsonGolem.asset";
            BossEncounterDefinition existing =
                AssetDatabase.LoadAssetAtPath<BossEncounterDefinition>(encounterPath);
            if (existing != null)
            {
                Selection.activeObject = existing;
                BossEncounterEditorWindow.Open(existing);
                Debug.Log("Existing Crimson Golem encounter preserved.");
                return;
            }

            var warning = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Effects/Audio/Warning.wav");
            var impact = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Effects/Audio/Impact.wav");
            var impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Effects/Prefabs/Impact/TileImpact.prefab");
            var boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
            boss.id = "crimson-golem";
            boss.displayName = "크림슨 골렘";
            boss.arena.size = 17;
            boss.phases.Clear();

            EncounterPattern targeted = Pattern("Targeted", 0.85f);
            boss.libraryPatterns.Add(targeted);
            var target = new TileSelection { anchor = TileAnchor.Origin, snapshotKey = "target" };
            Clip(targeted, "Target warning", 0, 0.65f,
                new WarningEvent { tiles = target, color = new Color(0.7f, 0.3f, 1, 0.7f) });
            Clip(targeted, "Target hit", 0.65f, 0.2f,
                new DamageEvent { tiles = target, reason = "위치 지정 폭발" });
            if (impact != null) Clip(targeted, "Target SFX", 0.65f, 0.2f,
                new SfxEvent { clip = impact });

            for (int phaseIndex = 0; phaseIndex < 2; phaseIndex++)
            {
                var phase = new BossPhaseDefinition
                {
                    name = phaseIndex == 0 ? "PHASE 1" : "PHASE 2 · ENRAGED",
                    health = 150,
                    interval = phaseIndex == 0 ? 1.85f : 1.05f,
                    initialDelay = phaseIndex == 0 ? 2.4f : 0.5f
                };
                boss.phases.Add(phase);
                for (int pass = 0; pass < 2; pass++)
                {
                    TileShape[] shapes = phaseIndex == 0
                        ? new[] { TileShape.Cross, TileShape.Diamond, TileShape.Diagonal }
                        : new[] { TileShape.Cross, TileShape.Diamond, TileShape.Diagonal,
                            TileShape.Combined, TileShape.Horizontal, TileShape.Vertical };
                    foreach (TileShape shape in shapes)
                    {
                        float delay = phaseIndex == 0 ? 2f : 1f;
                        EncounterPattern sequence = Pattern(
                            "P" + (phaseIndex + 1) + "_" + shape + "_" + pass, delay + 0.3f);
                        var tiles = new TileSelection
                        {
                            shape = shape,
                            snapshotKey = "glyph",
                            ensureEscape = true,
                            radius = shape == TileShape.Diamond && pass == 1 ? 8 : 5
                        };
                        Clip(sequence, "Warning", 0, delay, new WarningEvent { tiles = tiles });
                        if (warning != null) Clip(sequence, "Warning SFX", 0, delay,
                            new SfxEvent { clip = warning });
                        Clip(sequence, "Damage", delay, 0.3f,
                            new DamageEvent { tiles = tiles, reason = shape.ToString() });
                        if (impact != null) Clip(sequence, "Impact SFX", delay, 0.3f,
                            new SfxEvent { clip = impact });
                        Clip(sequence, "Impact VFX", delay, 0.3f, new VfxEvent { tiles = tiles, prefab = impactPrefab });
                        Clip(sequence, "Camera", delay, 0.3f, new CameraEvent { shake = 12 });
                        if (phaseIndex == 1)
                            Clip(sequence, "Targeted subpattern", 0.15f, targeted.Duration,
                                new CallEncounterPatternEvent
                                {
                                    patternId = targeted.id,
                                    anchorToPlayer = true
                                });
                        phase.patterns.Add(sequence);
                    }
                }
            }

            var crystalAttack = BossEncounterEditorWindow.CreateCrystalAttackPattern();
            boss.libraryPatterns.Add(crystalAttack);
            var seal = new CrystalSealMechanic { attackPatternId = crystalAttack.id,
                activePrefab = Resources.Load<GameObject>("Art/Crystals/PhaseTwoCrystal") };
            foreach (var cell in new[] { new Vector2Int(8, 13), new Vector2Int(13, 9),
                new Vector2Int(11, 4), new Vector2Int(5, 4), new Vector2Int(3, 9) })
                seal.crystals.Add(new CrystalPlacement { cell = cell });
            boss.phases[1].mechanics.Add(seal);
            AssetDatabase.CreateAsset(boss, encounterPath);
            var catalog = AssetDatabase.LoadAssetAtPath<BossCatalog>(Root + "/BossCatalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BossCatalog>();
                AssetDatabase.CreateAsset(catalog, Root + "/BossCatalog.asset");
            }
            catalog.bosses.Clear();
            catalog.bosses.Add(boss);
            catalog.startingBoss = 0;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = boss;
            BossEncounterEditorWindow.Open(boss);
        }

        private static EncounterPattern Pattern(string name, float duration)
        {
            return new EncounterPattern
            {
                id = Guid.NewGuid().ToString("N"),
                name = name,
                minimumDuration = duration
            };
        }

        private static void Clip(EncounterPattern pattern, string name, float start,
            float duration, PatternEvent action)
        {
            pattern.clips.Add(new PatternClip
            {
                label = name,
                start = start,
                duration = duration,
                action = action
            });
        }
    }
}
#endif
