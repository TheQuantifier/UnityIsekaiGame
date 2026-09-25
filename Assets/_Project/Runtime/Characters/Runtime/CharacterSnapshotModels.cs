using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.CharacterSystem
{
    public sealed class CharacterIdentitySnapshot
    {
        public CharacterIdentitySnapshot(
            string accountId,
            string playerId,
            string personId,
            string actorId,
            string displayName,
            string originFamilyId,
            string originId,
            string birthGiftId,
            CharacterReadinessState readiness,
            long revision)
        {
            AccountId = accountId ?? string.Empty;
            PlayerId = playerId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            OriginFamilyId = originFamilyId ?? string.Empty;
            OriginId = originId ?? string.Empty;
            BirthGiftId = birthGiftId ?? string.Empty;
            Readiness = readiness;
            Revision = revision;
        }

        public string AccountId { get; }
        public string PlayerId { get; }
        public string PersonId { get; }
        public string ActorId { get; }
        public string DisplayName { get; }
        public string OriginFamilyId { get; }
        public string OriginId { get; }
        public string BirthGiftId { get; }
        public CharacterReadinessState Readiness { get; }
        public long Revision { get; }
    }

    public sealed class CharacterProgressionSnapshot
    {
        public CharacterProgressionSnapshot(
            OverallLevelBreakdown overallLevel,
            IReadOnlyList<RuntimeSkillRecord> learnedSkills,
            IReadOnlyList<TraitSnapshot> traits,
            IReadOnlyList<RuntimeRoleRecord> roles,
            IReadOnlyList<RuntimeSocialStatusRecord> socialStatuses,
            IReadOnlyList<RuntimeTitleRecord> titles)
        {
            OverallLevel = overallLevel;
            LearnedSkills = learnedSkills == null ? Array.Empty<RuntimeSkillRecord>() : learnedSkills.ToArray();
            Traits = traits == null ? Array.Empty<TraitSnapshot>() : traits.ToArray();
            Roles = roles == null ? Array.Empty<RuntimeRoleRecord>() : roles.ToArray();
            SocialStatuses = socialStatuses == null ? Array.Empty<RuntimeSocialStatusRecord>() : socialStatuses.ToArray();
            Titles = titles == null ? Array.Empty<RuntimeTitleRecord>() : titles.ToArray();
        }

        public OverallLevelBreakdown OverallLevel { get; }
        public IReadOnlyList<RuntimeSkillRecord> LearnedSkills { get; }
        public IReadOnlyList<TraitSnapshot> Traits { get; }
        public IReadOnlyList<RuntimeRoleRecord> Roles { get; }
        public IReadOnlyList<RuntimeSocialStatusRecord> SocialStatuses { get; }
        public IReadOnlyList<RuntimeTitleRecord> Titles { get; }
    }

    public sealed class CharacterNumericalSnapshot
    {
        public CharacterNumericalSnapshot(
            IReadOnlyList<RuntimeAttributeValueRecord> baseAttributes,
            IReadOnlyDictionary<string, float> calculatedStats,
            IReadOnlyList<ResourceSnapshot> resources)
        {
            BaseAttributes = baseAttributes == null ? Array.Empty<RuntimeAttributeValueRecord>() : baseAttributes.ToArray();
            CalculatedStats = calculatedStats == null
                ? new Dictionary<string, float>(StringComparer.Ordinal)
                : new Dictionary<string, float>(calculatedStats, StringComparer.Ordinal);
            Resources = resources == null ? Array.Empty<ResourceSnapshot>() : resources.ToArray();
        }

        public IReadOnlyList<RuntimeAttributeValueRecord> BaseAttributes { get; }
        public IReadOnlyDictionary<string, float> CalculatedStats { get; }
        public IReadOnlyList<ResourceSnapshot> Resources { get; }
    }

    public sealed class CharacterSocialSnapshot
    {
        public CharacterSocialSnapshot(
            IReadOnlyList<RuntimeRoleRecord> roles,
            IReadOnlyList<RuntimeSocialStatusRecord> socialStatuses,
            IReadOnlyList<RuntimeTitleRecord> titles)
        {
            Roles = roles == null ? Array.Empty<RuntimeRoleRecord>() : roles.ToArray();
            SocialStatuses = socialStatuses == null ? Array.Empty<RuntimeSocialStatusRecord>() : socialStatuses.ToArray();
            Titles = titles == null ? Array.Empty<RuntimeTitleRecord>() : titles.ToArray();
        }

        public IReadOnlyList<RuntimeRoleRecord> Roles { get; }
        public IReadOnlyList<RuntimeSocialStatusRecord> SocialStatuses { get; }
        public IReadOnlyList<RuntimeTitleRecord> Titles { get; }
    }

    public sealed class CharacterCapabilitySnapshot
    {
        public CharacterCapabilitySnapshot(
            IReadOnlyList<CapabilitySnapshot> capabilities,
            IReadOnlyDictionary<string, float> resistances,
            IReadOnlyList<string> immunities)
        {
            Capabilities = capabilities == null ? Array.Empty<CapabilitySnapshot>() : capabilities.ToArray();
            Resistances = resistances == null
                ? new Dictionary<string, float>(StringComparer.Ordinal)
                : new Dictionary<string, float>(resistances, StringComparer.Ordinal);
            Immunities = immunities == null ? Array.Empty<string>() : immunities.ToArray();
        }

        public IReadOnlyList<CapabilitySnapshot> Capabilities { get; }
        public IReadOnlyDictionary<string, float> Resistances { get; }
        public IReadOnlyList<string> Immunities { get; }
    }

    public sealed class CharacterFullSnapshot
    {
        public const int CurrentSchemaVersion = 1;

        public CharacterFullSnapshot(
            int schemaVersion,
            long revision,
            CharacterIdentitySnapshot identity,
            CharacterProgressionSnapshot progression,
            CharacterNumericalSnapshot numerical,
            CharacterSocialSnapshot social,
            CharacterCapabilitySnapshot capabilities,
            bool developmentView,
            string diagnostics)
        {
            SchemaVersion = schemaVersion;
            Revision = revision;
            Identity = identity;
            Progression = progression;
            Numerical = numerical;
            Social = social;
            Capabilities = capabilities;
            DevelopmentView = developmentView;
            Diagnostics = diagnostics ?? string.Empty;
        }

        public int SchemaVersion { get; }
        public long Revision { get; }
        public CharacterIdentitySnapshot Identity { get; }
        public CharacterProgressionSnapshot Progression { get; }
        public CharacterNumericalSnapshot Numerical { get; }
        public CharacterSocialSnapshot Social { get; }
        public CharacterCapabilitySnapshot Capabilities { get; }
        public bool DevelopmentView { get; }
        public string Diagnostics { get; }
    }
}
