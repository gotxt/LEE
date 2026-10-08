#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class FireTileTests
    {
        private SpecialTileDefinition fire;
        private SpecialTileField field;
        private readonly Vector2Int a = new Vector2Int(5, 5), b = new Vector2Int(6, 5);
        [SetUp] public void Setup()
        {
            fire = Object.Instantiate(Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire"));
            field = new SpecialTileField(new[] {
                new SpecialTilePlacement { cell = a, tile = fire }, new SpecialTilePlacement { cell = b, tile = fire } });
        }
        [TearDown] public void Cleanup() { field.Dispose(); Object.DestroyImmediate(fire); }

        [TestCase("TileSprite_CampfireActive", "331C09796E9FAAD5C9A57859F1F7B3A8A8BE53A64F849BC93F59FCB97B72556A", true)]
        [TestCase("TileSprite_CampfireInactive", "34893A2F169232D2850A52327B72FE5F6D7D7B7C059682639257AF50B7885973", false)]
        public void OriginalSpritesUseSameFullCanvasAndPixelImport(string name, string hash, bool active)
        {
            string path = "Assets/Resources/Art/SpecialTiles/" + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.NotNull(sprite); Assert.AreEqual(sprite, active ? fire.sprite : fire.inactiveSprite);
            Assert.AreEqual(new Rect(0, 0, 32, 32), sprite.rect);
            Assert.AreEqual(new Vector2(16, 16), sprite.pivot); Assert.AreEqual(32, sprite.pixelsPerUnit);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.AreEqual(FilterMode.Point, importer.filterMode); Assert.IsFalse(importer.mipmapEnabled);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            using var sha = System.Security.Cryptography.SHA256.Create();
            Assert.AreEqual(hash, BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", ""));
        }

        [Test] public void DefaultInactiveThenOnlyCompletedTrailCellsIgniteAndRefreshWithoutStacking()
        {
            Assert.IsTrue(fire.requiresCompletedAttack); Assert.AreEqual(30, fire.activationSeconds); Assert.AreEqual(20, fire.fireMoveCount);
            string before = EditorJsonUtility.ToJson(fire);
            Assert.IsFalse(field.IsActive(a)); Assert.AreEqual(fire.inactiveSprite, field.SpriteAt(a));
            Assert.IsFalse(field.OnCompletedAttack(Array.Empty<Vector2Int>()));
            field.OnCompletedAttack(new[] { a, a, new Vector2Int(-1, -1) });
            Assert.IsTrue(field.IsActive(a)); Assert.IsFalse(field.IsActive(b)); Assert.AreEqual(30, field.ActiveSecondsRemaining(a));
            Assert.AreEqual(fire.sprite, field.SpriteAt(a));
            Assert.IsFalse(field.Advance(29.5f)); Assert.AreEqual(.5f, field.ActiveSecondsRemaining(a));
            field.OnCompletedAttack(new[] { a, b });
            Assert.AreEqual(30, field.ActiveSecondsRemaining(a)); Assert.AreEqual(30, field.ActiveSecondsRemaining(b));
            Assert.IsFalse(field.Advance(29.75f)); Assert.IsTrue(field.IsActive(a));
            Assert.IsTrue(field.Advance(.25f)); Assert.IsFalse(field.IsActive(a)); Assert.IsFalse(field.IsActive(b));
            Assert.AreEqual(fire.inactiveSprite, field.SpriteAt(a)); Assert.IsFalse(field.Advance(100));
            Assert.AreEqual(before, EditorJsonUtility.ToJson(fire));
        }

        [Test] public void InactiveDoesNotChargeActiveRefillsAndExpiryPreservesLastBurningMove()
        {
            var state = new SpecialTilePlayerState();
            Assert.IsFalse(state.Enter(a + Vector2Int.down, a, field.At(a)).HasFireOnArrival);
            Assert.AreEqual(0, state.FireMovesRemaining);
            field.OnCompletedAttack(new[] { a });
            state.Enter(a, a + Vector2Int.down, null);
            Assert.IsTrue(state.Enter(a + Vector2Int.down, a, field.At(a), field.IsActive(a)).HasFireOnArrival);
            Assert.AreEqual(20, state.FireMovesRemaining);
            state.Enter(a, b, field.At(b), field.IsActive(b)); Assert.AreEqual(19, state.FireMovesRemaining);
            state.Enter(b, a, field.At(a), field.IsActive(a)); Assert.AreEqual(20, state.FireMovesRemaining);
            field.Advance(30); Assert.AreEqual(20, state.FireMovesRemaining);
            for (int i = 0; i < 20; i++)
            {
                var from = i % 2 == 0 ? a : b; var to = i % 2 == 0 ? b : a;
                Assert.IsTrue(state.Enter(from, to, field.At(to), field.IsActive(to)).HasFireOnArrival);
            }
            Assert.AreEqual(0, state.FireMovesRemaining);
            Assert.IsFalse(state.Enter(a, b, field.At(b), field.IsActive(b)).HasFireOnArrival);
        }

        [Test] public void LayersCannotIgniteCoveredFireAndHiddenTimersExpireWithoutResurrection()
        {
            var vine = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines");
            field.OnCompletedAttack(new[] { a });
            using (field.Add(vine, new[] { a, b }))
            {
                Assert.IsFalse(field.OnCompletedAttack(new[] { a, b }));
                Assert.AreEqual(vine.sprite, field.SpriteAt(a));
                var state = new SpecialTilePlayerState();
                Assert.IsFalse(state.Enter(a + Vector2Int.down, a, field.At(a), field.IsActive(a)).HasFireOnArrival);
                Assert.AreEqual(1, state.StunRemaining);
                field.Advance(30);
            }
            Assert.IsFalse(field.IsActive(a)); Assert.IsFalse(field.IsActive(b));
            field.OnCompletedAttack(new[] { a });
            using (field.Add(fire, new[] { a }))
            {
                Assert.IsFalse(field.IsActive(a), "A new placement must not inherit the older one's activation.");
                field.OnCompletedAttack(new[] { a }); field.Advance(10);
            }
            Assert.AreEqual(20, field.ActiveSecondsRemaining(a));
            field.Dispose(); Assert.IsNull(field.At(a)); Assert.IsFalse(field.IsActive(a));
            Assert.Throws<ObjectDisposedException>(() => field.OnCompletedAttack(new[] { a }));
            field = new SpecialTileField(new[] { new SpecialTilePlacement { cell = a, tile = fire } });
            Assert.IsFalse(field.IsActive(a));
        }

        [Test] public void EditableDurationAndLegacyAlwaysOnTilesStayIndependent()
        {
            fire.activationSeconds = 7;
            field.OnCompletedAttack(new[] { a }); field.Advance(6.75f); Assert.IsTrue(field.IsActive(a));
            field.Advance(.25f); Assert.IsFalse(field.IsActive(a));
            fire.requiresCompletedAttack = false;
            Assert.IsTrue(field.IsActive(a)); Assert.AreEqual(fire.sprite, field.SpriteAt(a));
            Assert.IsFalse(field.OnCompletedAttack(new[] { a }));
            var state = new SpecialTilePlayerState(); state.Enter(a, b, fire); Assert.AreEqual(20, state.FireMovesRemaining);
        }

        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidActivationDurationsAreRejected(float seconds)
        {
            fire.activationSeconds = seconds; var errors = new List<string>(); fire.Validate(errors); Assert.IsNotEmpty(errors);
            Assert.Throws<ArgumentException>(() => field.Add(fire, new[] { a }));
        }

        [Test] public void BadClockInputIsRejectedAndLargeDeltaExpires()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Advance(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Advance(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Advance(float.PositiveInfinity));
            field.OnCompletedAttack(new[] { a }); Assert.IsTrue(field.Advance(10000)); Assert.IsFalse(field.IsActive(a));
        }

        [Test] public void SharedDataRoundTripKeepsReferencesAndSettings()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Tests/FireTile_RoundTrip.asset");
            var copy = Object.Instantiate(fire);
            try
            {
                AssetDatabase.CreateAsset(copy, path); AssetDatabase.SaveAssetIfDirty(copy);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var loaded = AssetDatabase.LoadAssetAtPath<SpecialTileDefinition>(path);
                Assert.IsTrue(loaded.requiresCompletedAttack); Assert.AreEqual(30, loaded.activationSeconds);
                Assert.AreEqual(fire.sprite, loaded.sprite); Assert.AreEqual(fire.inactiveSprite, loaded.inactiveSprite);
                Assert.AreEqual(20, loaded.fireMoveCount);
            }
            finally { AssetDatabase.DeleteAsset(path); if (copy != null) Object.DestroyImmediate(copy); }
        }
    }
}
#endif
