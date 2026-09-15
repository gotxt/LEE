using System;
using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    [Serializable]
    public sealed class BossSocket
    {
        public string name;
        public Transform target;
    }

    // Place this component on the one prefab representing a boss.
    public sealed class BossActor : MonoBehaviour
    {
        public Animator animator;
        public List<BossSocket> sockets = new List<BossSocket>();
        public Animator Animator => animator != null ? animator : GetComponentInChildren<Animator>(true);
        public Transform Socket(string socket)
        {
            if (string.IsNullOrEmpty(socket)) return transform;
            foreach (var item in sockets)
                if (item != null && item.name == socket && item.target != null) return item.target;
            throw new InvalidOperationException("Unknown boss socket: " + socket);
        }
    }

    [Serializable]
    public sealed class BossVisualDefinition
    {
        public BossActor prefab;
        [Tooltip("Grid coordinates; empty floor cells are valid.")]
        public Vector2 position = new Vector2(8, 8);
        [Min(0.01f), Tooltip("Prefab units measured in tiles.")]
        public float size = 1f;
        [Min(0)] public int animationLayer;
        public string idleState = "";

        public void Validate(List<string> errors, BossArenaDefinition arena)
        {
            if (prefab == null) return; // Existing bosses retain their original presentation.
            if (!BossEvent.Finite(size) || size <= 0 || !BossEvent.Finite(position.x) || !BossEvent.Finite(position.y))
                errors.Add("Boss visual: size and position must be finite; size must be positive.");
            if (arena != null && (position.x < 0 || position.y < 0 || position.x > arena.GridSize - 1 || position.y > arena.GridSize - 1))
                errors.Add("Boss visual: position must be inside the coordinate grid (empty cells are allowed).");
            var names = new HashSet<string>();
            foreach (var socket in prefab.sockets)
                if (socket == null || string.IsNullOrWhiteSpace(socket.name) || !names.Add(socket.name) ||
                    socket.target == null || !socket.target.IsChildOf(prefab.transform))
                    errors.Add("Boss visual: sockets need unique names and a transform inside the prefab.");
            if (prefab.Animator != null && (prefab.Animator.runtimeAnimatorController == null ||
                string.IsNullOrWhiteSpace(idleState) || animationLayer < 0))
                errors.Add("Boss visual: assign an Animator controller, layer and full idle state path.");
        }
    }

    public interface IBossPatternHost
    {
        IPatternLease BossAnimation(BossAnimationEvent action);
        IPatternLease BossVfx(BossVfxEvent action);
        IPatternLease BossMotion(BossMotionEvent action, float duration);
    }
}
