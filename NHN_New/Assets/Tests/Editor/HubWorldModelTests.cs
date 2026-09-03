using NUnit.Framework;
using UnityEngine;

namespace NHN.TraceStrike.Tests
{
    public sealed class HubWorldModelTests
    {
        [Test]
        public void CharacterCollisionPreviewsThenSwapsAndMovesThrough()
        {
            var hub = new HubWorldModel();
            hub.Reset(0);

            Assert.AreEqual(HubMoveResult.Previewed, hub.TryMove(Vector2Int.up, out HubObjectData preview));
            Assert.AreEqual(HubObjectType.Character, preview.Type);
            Assert.AreEqual(new Vector2Int(1, 1), hub.Player);

            Assert.AreEqual(HubMoveResult.CharacterChanged,
                hub.TryMove(Vector2Int.up, out HubObjectData changed));
            Assert.AreEqual(changed.Index, hub.CurrentCharacter);
            Assert.AreEqual(new Vector2Int(1, 3), hub.Player);
            Assert.AreEqual(0, hub.Objects[new Vector2Int(1, 2)].Index);
            Assert.IsFalse(hub.FocusedCell.HasValue);
        }

        [Test]
        public void MovingTwoCellsFromFocusedObjectClearsPreview()
        {
            var hub = new HubWorldModel();
            hub.Reset();
            hub.TryMove(Vector2Int.up, out _);

            Assert.AreEqual(HubMoveResult.Moved, hub.TryMove(Vector2Int.right, out _));
            Assert.IsFalse(hub.FocusedCell.HasValue);
        }

        [Test]
        public void AvailableStageRequiresTwoCollisions()
        {
            var hub = new HubWorldModel();
            hub.Reset();
            MoveTo(hub, new Vector2Int(1, 8));

            Assert.AreEqual(HubMoveResult.Previewed, hub.TryMove(Vector2Int.up, out _));
            Assert.AreEqual(HubMoveResult.StageSelected, hub.TryMove(Vector2Int.up, out _));
        }

        [Test]
        public void FutureStageStaysLockedOnSecondCollision()
        {
            var hub = new HubWorldModel();
            hub.Reset();
            MoveTo(hub, new Vector2Int(3, 8));

            Assert.AreEqual(HubMoveResult.Previewed, hub.TryMove(Vector2Int.up, out _));
            Assert.AreEqual(HubMoveResult.StageLocked, hub.TryMove(Vector2Int.up, out _));
        }

        [Test]
        public void MapBoundaryBlocksMovement()
        {
            var hub = new HubWorldModel();
            hub.Reset();
            hub.TryMove(Vector2Int.down, out _);

            Assert.AreEqual(HubMoveResult.Blocked, hub.TryMove(Vector2Int.down, out _));
        }

        private static void MoveTo(HubWorldModel hub, Vector2Int destination)
        {
            while (hub.Player.x < 5) hub.TryMove(Vector2Int.right, out _);
            while (hub.Player.y < destination.y)
            {
                hub.TryMove(Vector2Int.up, out _);
            }
            while (hub.Player.x < destination.x) hub.TryMove(Vector2Int.right, out _);
            while (hub.Player.x > destination.x) hub.TryMove(Vector2Int.left, out _);
        }
    }
}
