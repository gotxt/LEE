using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NHN.TraceStrike.Effects
{
    public enum UiEffectMotion { Flash, Spark, Dust, Chunk, Shockwave }

    [Serializable]
    public sealed class UiEffectEmitter
    {
        public UiEffectMotion motion;
        [Min(1)] public int count = 1;
        public Color[] colors = { Color.white };
        public Vector2 offset;
        [Tooltip("크기를 타일 비율로 정합니다. 0이면 아래 픽셀 크기를 사용합니다.")]
        [Min(0)] public float tileSizeRatio;
        public Vector2 size = new Vector2(8, 17);
        public Vector2 lifetime = new Vector2(0.3f, 0.3f);
        public Vector2 velocityX, velocityY;
        public bool radial;
        public Vector2 speed = new Vector2(45, 115);
        public float angleJitter = 16;
        public Vector2 gravity;
        public Vector2 spin = new Vector2(-620, 620);
    }

    /// <summary>
    /// Canvas-compatible, prefab-authored pixel effects. The motion curves come
    /// from TraceStrikeGame's existing sparks, dirt chunks, dust and shockwave.
    /// No damage, tile selection, or attack scheduling lives in this component.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class UiEffectPlayer : MonoBehaviour
    {
        [Min(1)] public float referenceCellSize = 152.81818f;
        [Min(0.01f)] public float sizeMultiplier = 1f;
        [Tooltip("새 이펙트도 이 목록의 색·개수·크기·수명·운동 설정을 조합해서 제작합니다.")]
        public List<UiEffectEmitter> emitters = new List<UiEffectEmitter>();
        public int seed = 12345;
        public bool destroyWhenFinished;
        private readonly List<Particle> particles = new List<Particle>();
        private bool initialized;
        private float age;
        public int ParticleCount => particles.Count;
        public float Duration
        {
            get
            {
                float duration = 0.01f;
                foreach (var e in emitters)
                    if (e != null) duration = Mathf.Max(duration, e.lifetime.x, e.lifetime.y);
                return duration;
            }
        }

        private sealed class Particle
        {
            public UiEffectEmitter emitter;
            public RectTransform rect;
            public Image image;
            public Outline outline;
            public Vector2 velocity;
            public Color color;
            public float lifetime, gravity, spin;
        }

        private void Start() { if (!initialized) Play(referenceCellSize); }
        private void Update()
        {
            if (!initialized) return;
            Sample(age + Time.deltaTime);
            if (destroyWhenFinished && age >= Duration) Destroy(gameObject);
        }

        public void Play(float cellSize)
        {
            ClearParticles();
            initialized = true; age = 0;
            var root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one * 0.5f;
            root.sizeDelta = Vector2.one * referenceCellSize;
            root.localScale = Vector3.one * (Mathf.Max(1, cellSize) / Mathf.Max(1, referenceCellSize) * Mathf.Max(0.01f, sizeMultiplier));
            var random = new System.Random(seed);
            foreach (var e in emitters)
            {
                if (e == null) continue;
                for (int i = 0; i < Mathf.Clamp(e.count, 1, 256); i++)
                {
                    var go = new GameObject(e.motion.ToString(), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.layer = gameObject.layer;
                    var rect = (RectTransform)go.transform;
                    rect.SetParent(transform, false);
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * 0.5f;
                    var image = go.GetComponent<Image>(); image.raycastTarget = false;
                    float size = e.tileSizeRatio > 0 ? referenceCellSize * e.tileSizeRatio : Range(random, e.size);
                    rect.sizeDelta = Vector2.one * size;
                    var p = new Particle { emitter = e, rect = rect, image = image,
                        color = e.colors != null && e.colors.Length > 0 ? e.colors[i % e.colors.Length] : Color.white,
                        lifetime = Mathf.Max(0.01f, Range(random, e.lifetime)), gravity = Range(random, e.gravity),
                        spin = Range(random, e.spin), velocity = new Vector2(Range(random, e.velocityX), Range(random, e.velocityY)) };
                    float angle = (360f / Mathf.Max(1, e.count) * i + Range(random, new Vector2(-e.angleJitter, e.angleJitter))) * Mathf.Deg2Rad;
                    if (e.motion == UiEffectMotion.Spark)
                    {
                        p.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(random, e.speed);
                        rect.localRotation = Quaternion.Euler(0, 0, 45);
                    }
                    else if (e.motion == UiEffectMotion.Chunk)
                    {
                        rect.sizeDelta = new Vector2(Mathf.Max(8, Step(size, 4)), Mathf.Max(8, Step(size * Range(random, new Vector2(0.65f, 1.35f)), 4)));
                        if (e.radial)
                        {
                            float speed = Range(random, e.speed);
                            p.velocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Abs(Mathf.Sin(angle)) * speed + Range(random, e.velocityY));
                        }
                    }
                    else if (e.motion == UiEffectMotion.Dust) rect.localRotation = Quaternion.Euler(0, 0, random.Next(4) * 90);
                    else if (e.motion == UiEffectMotion.Shockwave)
                    {
                        p.outline = go.AddComponent<Outline>(); p.outline.effectDistance = new Vector2(4, -4);
                    }
                    particles.Add(p);
                }
            }
            Sample(0);
        }

        // Sampling is independent of frame rate, and can be used by editor tests.
        public void Sample(float seconds)
        {
            age = Mathf.Max(0, seconds);
            foreach (var p in particles)
            {
                bool active = age < p.lifetime;
                if (p.rect == null) continue;
                p.rect.gameObject.SetActive(active);
                if (!active) continue;
                float t = Mathf.Clamp01(age / p.lifetime);
                Vector2 position = p.emitter.offset;
                Vector3 scale = Vector3.one;
                float alpha = 1;
                switch (p.emitter.motion)
                {
                    case UiEffectMotion.Spark:
                        position += p.velocity * Mathf.Sin(t * Mathf.PI * 0.55f);
                        scale = Vector3.one * Step(1 - t * 0.65f, 0.125f);
                        alpha = Step(1 - t, 0.2f);
                        break;
                    case UiEffectMotion.Chunk:
                        position += p.velocity * age + Vector2.down * (p.gravity * age * age * 0.5f);
                        p.rect.localRotation = Quaternion.Euler(0, 0, Mathf.Round(p.spin * age / 90) * 90);
                        scale = Vector3.one * Step(Mathf.Lerp(1, 0.35f, t * t), 0.2f);
                        alpha = Step(t < 0.58f ? 1 : Mathf.Clamp01(1 - (t - 0.58f) / 0.42f), 0.2f);
                        break;
                    case UiEffectMotion.Dust:
                        position += Vector2.up * (t * 24);
                        scale = new Vector3(Step(Mathf.Lerp(0.22f, 1.55f, t), 0.2f), Step(Mathf.Lerp(0.18f, 0.88f, t), 0.2f), 1);
                        alpha = Step((1 - t) * 0.72f, 0.15f);
                        break;
                    case UiEffectMotion.Shockwave:
                        scale = Vector3.one * Step(Mathf.Lerp(0.28f, 2.35f, t), 0.2f);
                        alpha = Step((1 - t) * 0.2f, 0.05f);
                        p.outline.effectColor = new Color(p.color.r, p.color.g, p.color.b, Step((1 - t) * 0.95f, 0.2f) * p.color.a);
                        break;
                }
                p.rect.anchoredPosition = new Vector2(Step(position.x, 4), Step(position.y, 4));
                p.rect.localScale = scale;
                p.image.color = new Color(p.color.r, p.color.g, p.color.b, p.color.a * alpha);
            }
        }

        private void ClearParticles()
        {
            foreach (var p in particles)
            {
                if (p.rect == null) continue;
                p.rect.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(p.rect.gameObject);
                else DestroyImmediate(p.rect.gameObject);
            }
            particles.Clear();
        }

        private static float Range(System.Random random, Vector2 range) => Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        private static float Step(float value, float step) => Mathf.Round(value / step) * step;
    }
}
