#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NHN.TraceStrike.Patterns;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace NHN.TraceStrike.Tests
{
    public sealed class SpecialTileRuntimeTests
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
        private static T Get<T>(TraceStrikeGame game, string name) => (T)typeof(TraceStrikeGame).GetField(name, Flags).GetValue(game);
        private static void Set(TraceStrikeGame game, string name, object value) => typeof(TraceStrikeGame).GetField(name, Flags).SetValue(game, value);
        private static void Call(TraceStrikeGame game, string name, params object[] args) => typeof(TraceStrikeGame).GetMethod(name, Flags).Invoke(game, args);
        private sealed class StepObserver : BossMechanicRuntime
        {
            public readonly List<PlayerTileStep> Steps = new List<PlayerTileStep>();
            public override void Advance(float delta) { }
            public override void OnPlayerStep(PlayerTileStep step) { Steps.Add(step); }
            public override void Dispose() { }
        }
        [Serializable] private sealed class ObserverDefinition : BossMechanicDefinition
        {
            public readonly StepObserver Observer = new StepObserver();
            public override BossMechanicRuntime Create(MechanicContext context) => Observer;
            public override void Validate(BossEncounterDefinition boss, List<string> errors) { }
        }

        [UnityTest] public IEnumerator ActualGameContactPauseEndAttackRestartAndRendering()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("Graphics device required.");
            yield return new EnterPlayMode();
            var game = Object.FindAnyObjectByType<TraceStrikeGame>();
            // The empty test scene has no listener; supply one only in the test instance.
            if (Object.FindAnyObjectByType<AudioListener>() == null) game.gameObject.AddComponent<AudioListener>();
            var saved = Resources.Load<BossEncounterDefinition>("Patterns/BossData_.RottenBloom");
            string before = JsonUtility.ToJson(saved);
            var copy = Object.Instantiate(saved);
            var fire = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Fire");
            var vine = Resources.Load<SpecialTileDefinition>("SpecialTiles/TileData_Vines");
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                game.PreviewPattern(saved, new EncounterPattern { minimumDuration = 999 });
                Set(game, "activeBoss", copy);
                copy.arena.SetSpecialTile(new Vector2Int(15, 2), fire);
                copy.arena.SetSpecialTile(new Vector2Int(15, 3), vine);
                copy.arena.SetSpecialTile(new Vector2Int(15, 4), vine);
                Call(game, "StartSpecialTiles");
                var model = Get<TrailFieldModel>(game, "model");
                model.SetEndpointRegions(new[] { new Vector2Int(15, 2) }, new[] { new Vector2Int(15, 4) });
                model.BeginRound(0, true, new Vector2Int(15, 1));
                var observer = new ObserverDefinition();
                Set(game, "mechanicSession", new BossMechanicSession(new[] { observer }, new MechanicContext(game, copy.FindPattern)));
                Call(game, "Move", Vector2Int.up);
                Assert.AreEqual(20, game.FireMovesRemaining); Assert.AreEqual(1, observer.Observer.Steps.Count);
                Assert.IsTrue(observer.Observer.Steps[0].HasFireOnArrival);
                Call(game, "Move", Vector2Int.up);
                var state = Get<SpecialTilePlayerState>(game, "tilePlayerState");
                Assert.AreEqual(1, state.StunRemaining); Assert.AreEqual(19, game.FireMovesRemaining);
                Call(game, "Move", Vector2Int.up); Assert.AreEqual(new Vector2Int(15, 3), model.Player);
                Assert.AreEqual(2, observer.Observer.Steps.Count);

                // Stun clock stops with attack presentation, then resumes independently of movement input.
                Set(game, "inputLocked", true); Call(game, "TickSpecialTiles", .5f);
                Assert.AreEqual(1, state.StunRemaining); Assert.AreEqual(19, game.FireMovesRemaining);
                Set(game, "inputLocked", false); Call(game, "TickSpecialTiles", .25f);
                Assert.AreEqual(.75f, state.StunRemaining);
                state.Advance(1);
                Call(game, "Move", Vector2Int.up); // END must still deliver contact before attack starts.
                Assert.AreEqual(new Vector2Int(15, 4), model.Player); Assert.AreEqual(3, observer.Observer.Steps.Count);
                Assert.IsTrue(observer.Observer.Steps[2].HasFireOnArrival); Assert.AreEqual(18, game.FireMovesRemaining);
                Assert.IsTrue(state.IsStunned); Assert.IsTrue(Get<bool>(game, "inputLocked"));
                Call(game, "RefreshBoard"); Call(game, "TickSpecialTiles", 0f); yield return null;
                Assert.IsTrue(Get<RectTransform[,]>(game, "specialItemVisuals")[15, 3].gameObject.activeSelf);
                Capture(game);
                game.StopAllCoroutines(); game.StopPatternPreview();
                Assert.AreEqual(0, game.FireMovesRemaining); Assert.IsFalse(state.IsStunned);
                Assert.AreEqual(before, JsonUtility.ToJson(saved));

                // Runtime placement owns only its own layer and never changes the asset.
                var lease = game.PlaceSpecialTiles(fire, new[] { new Vector2Int(15, 2) });
                var field = Get<SpecialTileField>(game, "placedSpecialTiles"); Assert.AreSame(fire, field.At(new Vector2Int(15, 2)));
                lease.Dispose(); Assert.IsNull(field.At(new Vector2Int(15, 2)));
                game.enabled = false; Assert.AreEqual(0, game.FireMovesRemaining); Assert.IsNull(Get<SpecialTileField>(game, "placedSpecialTiles"));
                // Unity Test Runner still fails errors/exceptions. Informational scene audio logs are unrelated.
            }
            finally { Time.timeScale = oldScale; Object.Destroy(copy); }
            yield return new ExitPlayMode();
        }
        private static void Capture(TraceStrikeGame game)
        {
            var camera = Get<Camera>(game, "uiCamera"); var rt = new RenderTexture(1600, 900, 24);
            var read = new Texture2D(1600, 900, TextureFormat.RGB24, false); var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); rt.Create();
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                RenderTexture.active = rt; read.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); read.Apply();
                Directory.CreateDirectory("Logs/PatternValidation/SpecialTiles");
                File.WriteAllBytes("Logs/PatternValidation/SpecialTiles/Contact.png", read.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; rt.Release(); Object.Destroy(rt); Object.Destroy(read); }
        }
        [UnityTearDown] public IEnumerator LeavePlayMode() { if (Application.isPlaying) yield return new ExitPlayMode(); }
    }
}
#endif
