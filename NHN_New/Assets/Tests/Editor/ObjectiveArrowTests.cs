#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class ObjectiveArrowTests
    {
        private static TrailFieldModel CreateModel()
        {
            var model = new TrailFieldModel();
            model.CreateField(0);
            model.BeginRound(0);
            return model;
        }

        private static Vector2Int NeighborDirection(TrailFieldModel model, Vector2Int cell)
        {
            foreach (Vector2Int direction in new[] { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left })
                if (model.IsTraversable(cell + direction) && cell + direction != model.End)
                    return direction;
            Assert.Fail("Expected an adjacent traversable cell.");
            return Vector2Int.zero;
        }

        [Test]
        public void BeforeTracingArrowTargetsStart()
        {
            TrailFieldModel model = CreateModel();
            model.TryPlacePlayer(new Vector2Int(TrailFieldModel.Size / 2, TrailFieldModel.Size / 2));
            Assert.That(model.IsTracing, Is.False);
            Assert.That(model.NavigationTarget, Is.EqualTo(model.Start));
        }

        [Test]
        public void SteppingOnStartSwitchesArrowToEndImmediately()
        {
            TrailFieldModel model = CreateModel();
            Vector2Int direction = NeighborDirection(model, model.Start);
            model.TryPlacePlayer(model.Start + direction);
            Assert.That(model.NavigationTarget, Is.EqualTo(model.Start));
            Assert.That(model.TryMove(-direction), Is.EqualTo(MoveResult.TrailStarted));
            Assert.That(model.NavigationTarget, Is.EqualTo(model.End));
        }

        [Test]
        public void RevisitedTrailSwitchesArrowBackToStart()
        {
            TrailFieldModel model = CreateModel();
            Vector2Int direction = NeighborDirection(model, model.Start);
            model.TryMove(direction);
            Assert.That(model.NavigationTarget, Is.EqualTo(model.End));
            Assert.That(model.TryMove(-direction), Is.EqualTo(MoveResult.TrailReset));
            Assert.That(model.NavigationTarget, Is.EqualTo(model.Start));
        }

        [Test]
        public void NewRoundTargetsNewStartWithoutTeleporting()
        {
            TrailFieldModel model = CreateModel();
            Vector2Int player = model.Player;
            Vector2Int oldStart = model.Start;
            model.BeginRound(1, false);
            Assert.That(model.Start, Is.Not.EqualTo(oldStart));
            Assert.That(model.Player, Is.EqualTo(player));
            Assert.That(model.NavigationTarget, Is.EqualTo(model.Start));
        }

        [Test]
        public void RespawnOnStartTargetsEndAndBlockedMovesDoNotChangeIt()
        {
            TrailFieldModel model = CreateModel();
            Assert.That(model.Player, Is.EqualTo(model.Start));
            Assert.That(model.NavigationTarget, Is.EqualTo(model.End));
            Assert.That(model.TryMove(new Vector2Int(1, 1)), Is.EqualTo(MoveResult.Blocked));
            Assert.That(model.NavigationTarget, Is.EqualTo(model.End));
        }
    }
}
#endif
