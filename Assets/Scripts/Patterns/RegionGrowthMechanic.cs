using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    // Data selects regions and effects; no boss identity or geometric petal assumptions.
    [Serializable]
    public sealed class RegionGrowthMechanic : BossMechanicDefinition
    {
        public List<string> regionIds = new List<string>();
        [Min(0)] public float initialDelay = 5;
        [Min(.01f)] public float interval = 5;
        [Range(1, 2500)] public int seedsPerSpawn = 5;
        [Min(.01f)] public float growthSeconds = 20;
        [Range(.001f, 1)] public float coverage = .3f;
        public SpecialTileDefinition overgrownTile;
        public List<string> enrageWhenAll = new List<string>();
        public List<string> enrageWhenAny = new List<string>();
        public GameObject seedPrefab, maturePrefab;
        public Sprite seedSprite, matureSprite;
        public Color seedTint = new Color(.95f, .72f, .25f, .9f);
        public Color matureTint = new Color(.32f, .2f, .48f, .95f);
        public RegionGrowthMechanic() { name = "구역 씨앗 성장"; }
        public override BossMechanicRuntime Create(MechanicContext context) => new RegionGrowthRuntime(this, context);
        public static int Threshold(int cells, float ratio) => !Finite(ratio) || ratio <= 0 || ratio > 1 ? 0 :
            Math.Max(1, (int)Math.Ceiling(cells * (decimal)ratio));
        internal void ValidateSettings(List<string> errors)
        {
            if (!Finite(initialDelay) || initialDelay < 0 || !Finite(interval) || interval < .01f ||
                !Finite(growthSeconds) || growthSeconds < .01f || seedsPerSpawn < 1 || seedsPerSpawn > 2500 ||
                !Finite(coverage) || coverage <= 0 || coverage > 1)
                errors.Add(name + ": 생성/성장 시간, 개수, 비율을 확인하세요.");
            if (overgrownTile == null) errors.Add(name + ": 덩굴화 때 적용할 특수 타일을 지정하세요.");
            else overgrownTile.Validate(errors);
            if (regionIds == null || regionIds.Count == 0 || regionIds.Distinct().Count() != regionIds.Count)
                errors.Add(name + ": 서로 다른 생성 구역을 하나 이상 선택하세요.");
            foreach (var ids in new[] { enrageWhenAll, enrageWhenAny })
                if (ids != null && (ids.Distinct().Count() != ids.Count || ids.Any(id => regionIds == null || !regionIds.Contains(id))))
                    errors.Add(name + ": 광폭화 조건은 생성 구역에서 선택하세요.");
            foreach (var prefab in new[] { seedPrefab, maturePrefab })
                if (prefab != null && prefab.GetComponent<RectTransform>() == null)
                    errors.Add(name + ": 씨앗 외형은 UI 프리팹을 사용하세요.");
        }
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public override void Validate(BossEncounterDefinition boss, List<string> errors)
        {
            ValidateSettings(errors);
            var floor = boss.arena.GetCells(); floor.ExceptWith(boss.bossVisual?.OccupiedCells() ?? new HashSet<Vector2Int>());
            ValidateRegions(boss.tileRegions, floor, errors);
        }
        internal void ValidateRegions(IReadOnlyList<EncounterTileRegion> available, ICollection<Vector2Int> floor, List<string> errors)
        {
            var used = new HashSet<Vector2Int>();
            foreach (var id in regionIds ?? new List<string>())
            {
                var region = available?.FirstOrDefault(r => r != null && r.id == id);
                if (region == null) { errors.Add(name + ": 없는 구역 ID " + id); continue; }
                var cells = new HashSet<Vector2Int>(region.cells ?? new List<Vector2Int>()); cells.IntersectWith(floor);
                if (cells.Count == 0) errors.Add(region.name + ": 유효한 바닥이 없습니다.");
                if (cells.Any(c => !used.Add(c))) errors.Add(name + ": 성장 구역은 서로 겹치지 않게 칠하세요.");
            }
        }
    }

    public sealed class RegionGrowthRuntime : BossMechanicRuntime
    {
        public sealed class RegionState
        {
            public string Id { get; internal set; }
            public string Name { get; internal set; }
            public IReadOnlyList<Vector2Int> Cells { get; internal set; }
            public int Threshold { get; internal set; }
            public int MatureCount { get; internal set; }
            public bool Overgrown { get; internal set; }
            internal IPatternLease layer;
        }
        public sealed class SeedState
        {
            public Vector2Int Cell { get; internal set; }
            public bool Mature { get; internal set; }
            public double MaturesAt { get; internal set; }
            internal RegionState region;
            internal IPatternLease visual;
        }
        private readonly MechanicContext context;
        private readonly ISpecialTileHost tiles;
        private readonly List<RegionState> regions = new List<RegionState>();
        private readonly Dictionary<Vector2Int, SeedState> seeds = new Dictionary<Vector2Int, SeedState>();
        private readonly System.Random random;
        private readonly float interval, growthSeconds;
        private readonly int spawnCount;
        private readonly SpecialTileDefinition overgrownTile;
        private readonly HashSet<string> all, any;
        private readonly GameObject seedPrefab, maturePrefab;
        private readonly Sprite seedSprite, matureSprite;
        private readonly Color seedTint, matureTint;
        private double clock, nextSpawn;
        private bool disposed, enraged;
        public IReadOnlyList<RegionState> Regions => regions;
        public IReadOnlyCollection<SeedState> Seeds => seeds.Values;
        public double Elapsed => clock;
        public override bool IsEnraged => !disposed && enraged;
        public override string Status => disposed ? "" : $"덩굴 {regions.Count(r => r.Overgrown)}/{regions.Count}";

        public RegionGrowthRuntime(RegionGrowthMechanic definition, MechanicContext context, int? randomSeed = null)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            tiles = context.Host as ISpecialTileHost ?? throw new InvalidOperationException("성장 기믹에는 특수 타일 호스트가 필요합니다.");
            var errors = new List<string>(); definition.ValidateSettings(errors);
            definition.ValidateRegions((context.Host as IEncounterRegionHost)?.TileRegions, new HashSet<Vector2Int>(context.Host.Walkable), errors);
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            random = randomSeed.HasValue ? new System.Random(randomSeed.Value) : new System.Random(UnityEngine.Random.Range(0, int.MaxValue));
            interval = definition.interval; nextSpawn = definition.initialDelay; growthSeconds = definition.growthSeconds;
            spawnCount = definition.seedsPerSpawn; overgrownTile = definition.overgrownTile;
            all = new HashSet<string>(definition.enrageWhenAll ?? new List<string>());
            any = new HashSet<string>(definition.enrageWhenAny ?? new List<string>());
            seedPrefab = definition.seedPrefab; maturePrefab = definition.maturePrefab;
            seedSprite = definition.seedSprite; matureSprite = definition.matureSprite;
            seedTint = definition.seedTint; matureTint = definition.matureTint;
            foreach (var id in definition.regionIds)
            {
                var source = ((IEncounterRegionHost)context.Host).TileRegions.First(r => r.id == id);
                // Snapshot denominator: temporary walls must not lower the threshold.
                var cells = source.cells.Distinct().Where(c => context.Host.Walkable.Contains(c)).OrderBy(c => c.y).ThenBy(c => c.x).ToArray();
                regions.Add(new RegionState { Id = id, Name = source.name, Cells = cells,
                    Threshold = RegionGrowthMechanic.Threshold(cells.Length, definition.coverage) });
            }
        }
        public override void Advance(float delta)
        {
            if (!RegionGrowthMechanic.Finite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
            if (disposed || !context.Host.IsAlive) return;
            double end = clock + delta;
            // Process due instants in order, so a large frame cannot age newly spawned seeds early.
            while (true)
            {
                double maturity = seeds.Values.Where(s => !s.Mature).Select(s => s.MaturesAt).DefaultIfEmpty(double.PositiveInfinity).Min();
                double next = Math.Min(nextSpawn, maturity);
                if (next > end) break;
                clock = next;
                foreach (var seed in seeds.Values.Where(s => !s.Mature && s.MaturesAt <= clock).ToArray())
                {
                    seed.Mature = true; seed.region.MatureCount++;
                    seed.visual?.Dispose(); seed.visual = null;
                    seed.visual = context.ShowDevice(seed.Cell, maturePrefab, matureSprite, matureTint);
                }
                foreach (var region in regions)
                    if (!region.Overgrown && region.MatureCount >= region.Threshold)
                    { region.layer = tiles.PlaceSpecialTiles(overgrownTile, region.Cells.ToArray()); region.Overgrown = true; }
                enraged |= (all.Count > 0 && all.All(id => regions.Any(r => r.Id == id && r.Overgrown))) ||
                    regions.Any(r => r.Overgrown && any.Contains(r.Id));
                if (nextSpawn <= clock) { SpawnBatch(); nextSpawn += interval; }
            }
            clock = end;
        }
        private void SpawnBatch()
        {
            var open = new HashSet<Vector2Int>(context.Host.Traversable);
            var candidates = regions.Select(r => (region: r, cells: r.Cells.Where(c => open.Contains(c) && c != context.Host.PlayerCell && !seeds.ContainsKey(c)).ToList()))
                .Where(r => r.cells.Count > 0).ToList();
            for (int i = 0; i < spawnCount && candidates.Count > 0; i++)
            {
                int regionIndex = random.Next(candidates.Count); var candidate = candidates[regionIndex];
                int cellIndex = random.Next(candidate.cells.Count); var cell = candidate.cells[cellIndex];
                var seed = new SeedState { Cell = cell, region = candidate.region, MaturesAt = clock + growthSeconds };
                seeds.Add(cell, seed); seed.visual = context.ShowDevice(cell, seedPrefab, seedSprite, seedTint);
                candidate.cells.RemoveAt(cellIndex); if (candidate.cells.Count == 0) candidates.RemoveAt(regionIndex);
            }
        }
        public override void OnPlayerStep(PlayerTileStep step)
        {
            if (disposed || !context.Host.IsAlive || !step.HasFireOnArrival || !seeds.TryGetValue(step.To, out var seed) || seed.Mature) return;
            seed.visual?.Dispose(); seeds.Remove(step.To);
        }
        public override void Dispose()
        {
            if (disposed) return; disposed = true;
            var leases = seeds.Values.Select(s => s.visual).Concat(regions.Select(r => r.layer)).ToArray();
            seeds.Clear(); regions.Clear();
            var errors = new List<Exception>();
            foreach (var lease in leases) try { lease?.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException(errors);
        }
    }
}
