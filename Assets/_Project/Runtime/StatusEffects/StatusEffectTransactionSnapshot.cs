using System.Collections.Generic;
using UnityEngine;

namespace UnityIsekaiGame.StatusEffects
{
    public sealed class StatusEffectTransactionSnapshot
    {
        internal StatusEffectTransactionSnapshot(string definitionId, IReadOnlyList<Entry> entries)
        {
            DefinitionId = definitionId ?? string.Empty;
            Entries = entries;
        }

        internal sealed class Entry
        {
            public StatusEffectDefinition Definition;
            public string ApplicationId;
            public string SourceId;
            public GameObject Source;
            public float RemainingDuration;
            public float ElapsedDuration;
            public int StackCount;
            public float AppliedAt;
        }

        internal string DefinitionId { get; }
        internal IReadOnlyList<Entry> Entries { get; }
    }
}
