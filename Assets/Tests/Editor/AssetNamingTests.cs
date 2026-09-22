#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class AssetNamingTests
    {
        [TestCase("Assets/Resources/Patterns/BossData_CrimsonGolem.asset", "a96e060000004acaa5000000000000c8")]
        [TestCase("Assets/Resources/Patterns/BossCatalog_Main.asset", "a96e060000004acaa5000000000000c9")]
        [TestCase("Assets/Art/Bosses/CrimsonGolem/BossVisual_CrimsonGolem.prefab", "721b81301a9ed324e9c14035f9f8c65d")]
        [TestCase("Assets/Art/Bosses/CrimsonGolem/BossAnimator_CrimsonGolem.controller", "10c6700208ef8c649992d032ebabc3de")]
        [TestCase("Assets/Art/Bosses/CrimsonGolem/BossMaterial_CrimsonGolem.mat", "d6d0f13a91504494eb18aae5d98e2750")]
        [TestCase("Assets/Resources/Art/Crystals/MechanicVisual_CrystalSeal.prefab", "e7c91aa64e02033ee08be705fecce7ad")]
        [TestCase("Assets/Resources/Art/Warnings/TileVisual_AttackWarning.prefab", "5adf4ff6ad4a57a67910a856ab2ab013")]
        [TestCase("Assets/Resources/Effects/Prefabs/Impact/VFX_TileImpact.prefab", "ad75267d72b74bdc9aef895000000020")]
        [TestCase("Assets/Resources/Effects/Prefabs/Impact/VFX_CrystalSparks.prefab", "ad75267d72b74bdc9aef895000000021")]
        [TestCase("Assets/Resources/Effects/Prefabs/Impact/VFX_DirtLaneEruption.prefab", "ad75267d72b74bdc9aef895000000022")]
        [TestCase("Assets/Resources/Effects/Prefabs/Impact/VFX_DirtAreaExplosion.prefab", "ad75267d72b74bdc9aef895000000023")]
        [TestCase("Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_FistRipple1.prefab", "be8c67243922fa54aa5a3bd812eec3dd")]
        [TestCase("Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_FistRipple2.prefab", "3af803520efc8984f90bcd2fe56f44f4")]
        [TestCase("Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_FistRipple3.prefab", "410236a6d67577d4b9199381a79f999e")]
        [TestCase("Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_RockfallFalling.prefab", "9c5e92d094a41af44b4391668ec32517")]
        [TestCase("Assets/Art/Bosses/CrimsonGolem/VFX_CrimsonGolem_RockfallImpact.prefab", "7653b6835667c4548a08f14653ada6e7")]
        public void RenamedAssetsRetainGuidsRootNamesAndResourceLoading(string path, string guid)
        {
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path));
            Assert.AreEqual(path, AssetDatabase.GUIDToAssetPath(guid));
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.IsNotNull(asset, path);
            Assert.AreEqual(Path.GetFileNameWithoutExtension(path), asset.name);
            const string resources = "/Resources/";
            int index = path.IndexOf(resources, StringComparison.Ordinal);
            if (index < 0) return;
            string resource = path.Substring(index + resources.Length);
            resource = resource.Substring(0, resource.LastIndexOf('.'));
            var loaded = Resources.Load(resource);
            Assert.IsNotNull(loaded, resource);
            Assert.AreEqual(path, AssetDatabase.GetAssetPath(loaded));
        }

        [TestCase(typeof(BossEncounterDefinition), "BossData_NewBoss")]
        [TestCase(typeof(PatternSequence), "PatternData_NewPattern")]
        [TestCase(typeof(BossCatalog), "BossCatalog_Main")]
        public void NewAssetMenuSuggestsRolePrefixedName(Type type, string expected)
        {
            var attribute = (CreateAssetMenuAttribute)Attribute.GetCustomAttribute(type, typeof(CreateAssetMenuAttribute));
            Assert.IsNotNull(attribute);
            Assert.AreEqual(expected, attribute.fileName);
        }

        [TestCase("Assets/BossData_IceGolem.asset", "IceGolem")]
        [TestCase("Assets/MyBoss.asset", "MyBoss")]
        [TestCase("Assets/BossData_.asset", "NewBoss")]
        public void FileRolePrefixDoesNotLeakIntoNewBossDisplayName(string path, string expected)
        {
            Assert.AreEqual(expected, Editor.BossEncounterEditorWindow.EncounterDisplayNameFromPath(path));
        }

        [Test]
        public void CatalogBossVisualAndMechanicLinksSurviveRename()
        {
            var boss = Resources.Load<BossEncounterDefinition>("Patterns/BossData_CrimsonGolem");
            var catalog = Resources.Load<BossCatalog>("Patterns/BossCatalog_Main");
            Assert.IsNotNull(boss);
            CollectionAssert.Contains(catalog.bosses, boss);
            Assert.AreEqual("crimson-golem", boss.id);
            const string art = "Assets/Art/Bosses/CrimsonGolem/";
            Assert.AreEqual(art + "BossVisual_CrimsonGolem.prefab", AssetDatabase.GetAssetPath(boss.bossVisual.prefab));
            var actor = boss.bossVisual.prefab;
            Assert.AreEqual(art + "BossAnimator_CrimsonGolem.controller", AssetDatabase.GetAssetPath(actor.animator.runtimeAnimatorController));
            Assert.AreEqual(art + "BossMaterial_CrimsonGolem.mat", AssetDatabase.GetAssetPath(actor.GetComponentInChildren<SpriteRenderer>().sharedMaterial));
            var seal = boss.phases[1].mechanics.OfType<CrystalSealMechanic>().Single();
            Assert.IsNotNull(seal.activePrefab.GetComponent<CrystalVisual>());
            Assert.AreEqual("MechanicVisual_CrystalSeal", seal.activePrefab.name);
            Assert.IsNotNull(boss.FindPattern(seal.attackPatternId));
            // Keep editable map/pattern/mechanic content out of naming assertions.
            // A designer's unfinished unrelated mechanic must not be rewritten to satisfy these tests.
        }
    }
}
#endif
