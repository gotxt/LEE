#if UNITY_EDITOR
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class ArenaTileImageTests
    {
        private Texture2D texture;
        private Sprite first, second;
        private BossEncounterDefinition boss;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(8, 4);
            first = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            second = Sprite.Create(texture, new Rect(4, 0, 4, 4), Vector2.one * 0.5f);
            boss = ScriptableObject.CreateInstance<BossEncounterDefinition>();
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearUndo(boss);
            Object.DestroyImmediate(boss);
            Object.DestroyImmediate(first); Object.DestroyImmediate(second);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void DefaultOverrideAndEraseResolveWithoutChangingWalkability()
        {
            var arena = boss.arena;
            var cell = arena.CenterCell;
            var floor = arena.GetCells();
            Assert.IsNull(arena.ResolveTileSprite(cell, arena.BuildTileSpriteLookup()));
            arena.defaultTileSprite = first;
            Assert.AreSame(first, arena.ResolveTileSprite(cell, arena.BuildTileSpriteLookup()));
            arena.SetTileSprite(cell, second);
            arena.SetTileSprite(cell, second);
            Assert.AreEqual(1, arena.tileImages.Count);
            Assert.AreSame(second, arena.ResolveTileSprite(cell, arena.BuildTileSpriteLookup()));
            arena.SetTileSprite(cell, null);
            Assert.IsEmpty(arena.tileImages);
            Assert.AreSame(first, arena.ResolveTileSprite(cell, arena.BuildTileSpriteLookup()));
            CollectionAssert.AreEquivalent(floor, arena.GetCells());
        }

        [Test]
        public void ChangingBaseUpdatesOnlyUnpaintedTilesAndEraseUsesCurrentBase()
        {
            var arena = boss.arena;
            var painted = arena.CenterCell;
            var unpainted = painted + Vector2Int.right;
            arena.defaultTileSprite = first;
            arena.SetTileSprite(painted, first);
            var lookup = arena.BuildTileSpriteLookup();
            arena.defaultTileSprite = second;
            Assert.AreSame(first, arena.ResolveTileSprite(painted, lookup));
            Assert.AreSame(second, arena.ResolveTileSprite(unpainted, lookup));
            arena.SetTileSprite(painted, null);
            Assert.AreSame(second, arena.ResolveTileSprite(painted, arena.BuildTileSpriteLookup()));
            arena.defaultTileSprite = null;
            Assert.IsNull(arena.ResolveTileSprite(unpainted, arena.BuildTileSpriteLookup()));
        }

        [Test]
        public void PaletteReorderAndRemovalDoNotChangePaintedReferences()
        {
            var arena = boss.arena;
            arena.tilePalette.Add(first); arena.tilePalette.Add(second);
            arena.SetTileSprite(arena.CenterCell, second);
            arena.tilePalette.Reverse(); arena.tilePalette.Clear();
            Assert.AreSame(second, arena.ResolveTileSprite(arena.CenterCell, arena.BuildTileSpriteLookup()));
        }

        [Test]
        public void CroppingAndFloorRemovalPreserveHiddenImageAssignments()
        {
            var arena = boss.arena;
            arena.size = 50; arena.MakeCustom();
            var cell = new Vector2Int(49, 25);
            arena.SetTileSprite(cell, second);
            arena.size = 18;
            Assert.IsNull(arena.ResolveTileSprite(cell, arena.BuildTileSpriteLookup()));
            arena.size = 50;
            arena.floorCells.Remove(cell);
            Assert.IsFalse(arena.GetCells().Contains(cell));
            Assert.AreSame(second, arena.ResolveTileSprite(cell, arena.BuildTileSpriteLookup()));
            Assert.AreEqual(1, arena.tileImages.Count);
        }

        [Test]
        public void SerializationAndUndoRedoRetainSpriteReferences()
        {
            // Editor JSON stores asset GUID/fileID references, not transient
            // Sprite.Create objects. Use real sub-assets like a sliced sheet.
            string folder = "Assets/__TileImageTest_" + System.Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            try
            {
                AssetDatabase.CreateAsset(texture, folder + "/Sheet.asset");
                first.name = "First"; second.name = "Second";
                AssetDatabase.AddObjectToAsset(first, texture);
                AssetDatabase.AddObjectToAsset(second, texture);
                AssetDatabase.SaveAssetIfDirty(texture);
                var cell = boss.arena.CenterCell;
                boss.arena.defaultTileSprite = first;
                Undo.RegisterCompleteObjectUndo(boss, "Paint test tile");
                boss.arena.SetTileSprite(cell, second);
                EditorUtility.SetDirty(boss);
                string painted = EditorJsonUtility.ToJson(boss);
                Undo.PerformUndo();
                Assert.AreSame(first, boss.arena.ResolveTileSprite(cell, boss.arena.BuildTileSpriteLookup()));
                Undo.PerformRedo();
                Assert.AreSame(second, boss.arena.ResolveTileSprite(cell, boss.arena.BuildTileSpriteLookup()));
                boss.arena.SetTileSprite(cell, null);
                EditorJsonUtility.FromJsonOverwrite(painted, boss);
                Assert.AreSame(second, boss.arena.ResolveTileSprite(cell, boss.arena.BuildTileSpriteLookup()));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [Test]
        public void MissingSpriteAndOldDataUseDefaultWithoutExceptions()
        {
            var arena = boss.arena;
            arena.tileImages = null;
            Assert.IsEmpty(arena.BuildTileSpriteLookup());
            arena.SetTileSprite(arena.CenterCell, second);
            arena.tileImages.Add(null);
            Object.DestroyImmediate(second);
            arena.defaultTileSprite = first;
            Assert.AreSame(first, arena.ResolveTileSprite(arena.CenterCell, arena.BuildTileSpriteLookup()));
        }
    }
}
#endif
