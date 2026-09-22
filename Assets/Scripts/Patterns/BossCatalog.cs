using System.Collections.Generic;
using UnityEngine;

namespace NHN.TraceStrike.Patterns
{
    [CreateAssetMenu(fileName = "BossCatalog_Main", menuName = "Trace Strike/Boss Catalog")]
    public sealed class BossCatalog : ScriptableObject
    {
        [Min(0)] public int startingBoss;
        public List<BossEncounterDefinition> bosses = new List<BossEncounterDefinition>();
    }
}
