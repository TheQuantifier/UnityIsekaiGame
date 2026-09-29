using System;
using System.Collections.Generic;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;

namespace UnityIsekaiGame.Beings.Biology
{
    public sealed class BodyCapabilitySummary
    {
        public BodyCapabilitySummary(string capabilityId, bool booleanValue, float numericValue, bool blocked)
        {
            CapabilityId = capabilityId ?? string.Empty;
            BooleanValue = booleanValue;
            NumericValue = numericValue;
            Blocked = blocked;
        }

        public string CapabilityId { get; }
        public bool BooleanValue { get; }
        public float NumericValue { get; }
        public bool Blocked { get; }
    }

    public sealed class BodyTraitSummary
    {
        public BodyTraitSummary(string traitId, string displayName)
        {
            TraitId = traitId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
        }

        public string TraitId { get; }
        public string DisplayName { get; }
    }

    public sealed class BodyStatContributionSummary
    {
        public BodyStatContributionSummary(string statId, string sourceId, float magnitude, string direction)
        {
            StatId = statId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            Magnitude = magnitude;
            Direction = direction ?? string.Empty;
        }

        public string StatId { get; }
        public string SourceId { get; }
        public float Magnitude { get; }
        public string Direction { get; }
    }

    public sealed class BodySnapshot
    {
        public BodySnapshot(
            string actorBodyId,
            string personId,
            string speciesId,
            string speciesDisplayName,
            string classificationId,
            string bodyFormId,
            BodyReadinessState readiness,
            long bodyRevision,
            IReadOnlyList<string> speciesTags,
            IReadOnlyList<BodyCapabilitySummary> capabilities,
            IReadOnlyList<BodyTraitSummary> traits,
            IReadOnlyList<BodyStatContributionSummary> statContributions,
            string defeatPolicyId,
            bool requiresBreathing,
            bool hasBlood,
            bool canBecomeUnconscious,
            bool canDie,
            bool canBeRevived,
            bool acceptsBiologicalHealing,
            bool acceptsRepair,
            bool hasPhysicalBody,
            AnatomySnapshot anatomy,
            BodyConditionSnapshot condition,
            VitalProcessSnapshot vitalProcesses,
            BiologicalHazardSnapshot biologicalHazards,
            BiologicalCompatibilitySnapshot biologicalCompatibility,
            BiologicalRecoverySnapshot biologicalRecovery,
            bool coherent,
            IReadOnlyList<string> diagnostics)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            SpeciesId = speciesId ?? string.Empty;
            SpeciesDisplayName = speciesDisplayName ?? string.Empty;
            BiologicalClassificationId = classificationId ?? string.Empty;
            BodyFormId = bodyFormId ?? string.Empty;
            Readiness = readiness;
            BodyRevision = bodyRevision;
            SpeciesTags = speciesTags ?? Array.Empty<string>();
            BiologicalCapabilities = capabilities ?? Array.Empty<BodyCapabilitySummary>();
            SpeciesOwnedTraits = traits ?? Array.Empty<BodyTraitSummary>();
            BiologicalStatContributions = statContributions ?? Array.Empty<BodyStatContributionSummary>();
            DefeatPolicyId = defeatPolicyId ?? string.Empty;
            RequiresBreathing = requiresBreathing;
            HasBlood = hasBlood;
            CanBecomeUnconscious = canBecomeUnconscious;
            CanDie = canDie;
            CanBeRevived = canBeRevived;
            AcceptsBiologicalHealing = acceptsBiologicalHealing;
            AcceptsRepair = acceptsRepair;
            HasPhysicalBody = hasPhysicalBody;
            Anatomy = anatomy;
            Condition = condition;
            VitalProcesses = vitalProcesses;
            BiologicalHazards = biologicalHazards;
            BiologicalCompatibility = biologicalCompatibility;
            BiologicalRecovery = biologicalRecovery;
            Coherent = coherent;
            Diagnostics = diagnostics ?? Array.Empty<string>();
        }

        public string ActorBodyId { get; }
        public string PersonId { get; }
        public string SpeciesId { get; }
        public string SpeciesDisplayName { get; }
        public string BiologicalClassificationId { get; }
        public string BodyFormId { get; }
        public BodyReadinessState Readiness { get; }
        public long BodyRevision { get; }
        public IReadOnlyList<string> SpeciesTags { get; }
        public IReadOnlyList<BodyCapabilitySummary> BiologicalCapabilities { get; }
        public IReadOnlyList<BodyTraitSummary> SpeciesOwnedTraits { get; }
        public IReadOnlyList<BodyStatContributionSummary> BiologicalStatContributions { get; }
        public string DefeatPolicyId { get; }
        public bool RequiresBreathing { get; }
        public bool HasBlood { get; }
        public bool CanBecomeUnconscious { get; }
        public bool CanDie { get; }
        public bool CanBeRevived { get; }
        public bool AcceptsBiologicalHealing { get; }
        public bool AcceptsRepair { get; }
        public bool HasPhysicalBody { get; }
        public AnatomySnapshot Anatomy { get; }
        public BodyConditionSnapshot Condition { get; }
        public VitalProcessSnapshot VitalProcesses { get; }
        public BiologicalHazardSnapshot BiologicalHazards { get; }
        public BiologicalCompatibilitySnapshot BiologicalCompatibility { get; }
        public BiologicalRecoverySnapshot BiologicalRecovery { get; }
        public bool Coherent { get; }
        public IReadOnlyList<string> Diagnostics { get; }
    }
}
