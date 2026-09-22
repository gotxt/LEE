#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NHN.TraceStrike.Effects;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace NHN.TraceStrike.Tests
{
    public sealed class UiEffectPrefabTests
    {
        private static GameObject Prefab(string name) => Resources.Load<GameObject>("Effects/Prefabs/Impact/" + name);

        [TestCase("VFX_TileImpact", 1, 0.3f)]
        [TestCase("VFX_CrystalSparks", 4, 0.117647f)]
        [TestCase("VFX_DirtLaneEruption", 16, 0.38f)]
        [TestCase("VFX_DirtAreaExplosion", 47, 0.5f)]
        public void PrefabsLoadGenerateUiGraphicsAndScaleToTile(string name, int count, float duration)
        {
            var prefab = Prefab(name);
            Assert.IsNotNull(prefab);
            var instance = Object.Instantiate(prefab);
            try
            {
                var effect = instance.GetComponent<UiEffectPlayer>();
                Assert.IsNotNull(effect);
                effect.Play(effect.referenceCellSize / 2);
                Assert.AreEqual(count, effect.ParticleCount);
                Assert.That(effect.Duration, Is.EqualTo(duration).Within(0.00001f));
                Assert.That(instance.transform.localScale.x, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.AreEqual(count, instance.GetComponentsInChildren<Image>().Length);
                Assert.IsTrue(instance.GetComponentsInChildren<Image>().All(i => !i.raycastTarget));
                effect.Sample(duration + 0.1f);
                Assert.IsEmpty(instance.GetComponentsInChildren<Image>());
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void PlaybackIsRepeatableAndDoesNotConsumeGameplayRandomState()
        {
            var instance = Object.Instantiate(Prefab("VFX_DirtAreaExplosion"));
            var state = Random.state;
            float expectedRandom = Random.value;
            Random.state = state;
            try
            {
                var effect = instance.GetComponent<UiEffectPlayer>();
                effect.Play(effect.referenceCellSize);
                effect.Sample(0.1f);
                var positions = instance.GetComponentsInChildren<Image>().Select(i => i.rectTransform.anchoredPosition).ToArray();
                effect.Sample(0.4f); effect.Sample(0.1f);
                CollectionAssert.AreEqual(positions, instance.GetComponentsInChildren<Image>().Select(i => i.rectTransform.anchoredPosition));
                effect.Play(effect.referenceCellSize); effect.Sample(0.1f);
                Assert.AreEqual(47, instance.transform.childCount);
                CollectionAssert.AreEqual(positions, instance.GetComponentsInChildren<Image>().Select(i => i.rectTransform.anchoredPosition));
                Assert.AreEqual(expectedRandom, Random.value);
            }
            finally { Random.state = state; Object.DestroyImmediate(instance); }
        }

        [Test]
        public void TileImpactMatchesOriginalFallbackColorAndSize()
        {
            var instance = Object.Instantiate(Prefab("VFX_TileImpact"));
            try
            {
                var effect = instance.GetComponent<UiEffectPlayer>(); effect.Play(100);
                var image = instance.GetComponentInChildren<Image>();
                Assert.AreEqual(new VfxEvent().color, image.color);
                Assert.That(image.rectTransform.sizeDelta.x * instance.transform.localScale.x, Is.EqualTo(70).Within(0.001));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void MovedAudioRetainsOriginalAssetGuids()
        {
            Assert.AreEqual("a96e060000004acaa50000000000001e", AssetDatabase.AssetPathToGUID("Assets/Resources/Effects/Audio/Warning.wav"));
            Assert.AreEqual("a96e060000004acaa50000000000001f", AssetDatabase.AssetPathToGUID("Assets/Resources/Effects/Audio/Impact.wav"));
            foreach (string cue in new[] { "Warning", "Impact" })
            {
                string path = "Assets/Resources/Effects/Audio/" + cue + ".wav";
                var asset = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                Assert.IsNotNull(asset, "Audio asset import failed at " + path);
                Assert.AreSame(asset, Resources.Load<AudioClip>("Effects/Audio/" + cue),
                    "Resource lookup failed. Asset path: " + AssetDatabase.GetAssetPath(asset) +
                    "; found: " + string.Join(", ", Resources.LoadAll<AudioClip>("Effects").Select(a => a.name)));
            }
        }

        [UnityTest]
        public IEnumerator ActualPatternSpawnsOneEffectPerTileAndCleansUpOnEndAndCancel()
        {
            var listener = new GameObject("VFX test listener", typeof(AudioListener));
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            Assert.IsNotNull(game);
            var host = (IPatternHost)game;
            var grid = (RectTransform)typeof(TraceStrikeGame).GetField("mainGrid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            var cells = host.Walkable.Take(2).ToList();
            var action = new VfxEvent { prefab = Prefab("VFX_DirtLaneEruption"),
                tiles = new TileSelection { anchor = TileAnchor.Absolute, shape = TileShape.Cells, cells = cells } };
            var pattern = new EncounterPattern { minimumDuration = 0.5f };
            pattern.clips.Add(new PatternClip { duration = 0.4f, action = action });
            var cellSize = (float)typeof(TraceStrikeGame).GetField("mainCellSize", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game);
            try
            {
                using (var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell)))
                {
                    runner.Advance(0);
                    var live = grid.GetComponentsInChildren<UiEffectPlayer>();
                    Assert.AreEqual(2, live.Length);
                    Assert.IsTrue(live.All(e => e.ParticleCount == 16));
                    Assert.That(live[0].transform.localScale.x, Is.EqualTo(cellSize / live[0].referenceCellSize).Within(0.001));
                    runner.Advance(0.5f);
                    yield return null;
                    Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
                }
                using (var runner = new PatternRunner(pattern, new PatternContext(host, host.CenterCell))) runner.Advance(0);
                yield return null;
                Assert.IsEmpty(grid.GetComponentsInChildren<UiEffectPlayer>());
            }
            finally { if (listener != null) Object.Destroy(listener); }
            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
