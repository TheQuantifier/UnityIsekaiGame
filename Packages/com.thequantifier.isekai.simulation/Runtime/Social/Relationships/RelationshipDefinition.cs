using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Social.Relationships
{
    [Serializable]
    public sealed class RelationshipRoleDefinitionData
    {
        public string roleId;
        public string displayName;
        public bool required = true;

        public RelationshipRoleDefinitionData Clone()
        {
            return new RelationshipRoleDefinitionData
            {
                roleId = roleId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                required = required
            };
        }
    }

    [CreateAssetMenu(fileName = "RelationshipDefinition", menuName = "Unity Isekai Game/Social/Relationship Definition")]
    public sealed class RelationshipDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string relationshipDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private RelationshipCategory category = RelationshipCategory.Social;
        [SerializeField] private RelationshipDirectionality directionality = RelationshipDirectionality.Symmetric;
        [SerializeField] private RelationshipDuplicatePolicy duplicatePolicy = RelationshipDuplicatePolicy.OneActiveBetweenParticipants;
        [SerializeField] private RelationshipRoleDefinitionData[] roles;
        [SerializeField] private bool mayEnd = true;
        [SerializeField] private bool allowSelfRelationship;
        [SerializeField] private bool canonicalizeSymmetricParticipants = true;
        [SerializeField] private string defaultAccessPolicyId;
        [SerializeField] private string[] tags;
        [SerializeField] private int version = 1;

        public string Id => relationshipDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public RelationshipCategory Category => category;
        public RelationshipDirectionality Directionality => directionality;
        public RelationshipDuplicatePolicy DuplicatePolicy => duplicatePolicy;
        public IReadOnlyList<RelationshipRoleDefinitionData> Roles => roles ?? Array.Empty<RelationshipRoleDefinitionData>();
        public bool MayEnd => mayEnd;
        public bool AllowSelfRelationship => allowSelfRelationship;
        public bool CanonicalizeSymmetricParticipants => canonicalizeSymmetricParticipants;
        public string DefaultAccessPolicyId => defaultAccessPolicyId ?? string.Empty;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            relationshipDefinitionId = relationshipDefinitionId?.Trim();
            version = Math.Max(1, version);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            RelationshipCategory relationshipCategory,
            RelationshipDirectionality relationshipDirectionality,
            RelationshipDuplicatePolicy activeDuplicatePolicy,
            IEnumerable<RelationshipRoleDefinitionData> roleDefinitions,
            bool relationshipMayEnd = true,
            bool allowSelf = false,
            string accessPolicyId = "",
            IEnumerable<string> tagIds = null)
        {
            relationshipDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            description = string.Empty;
            category = relationshipCategory;
            directionality = relationshipDirectionality;
            duplicatePolicy = activeDuplicatePolicy;
            roles = CleanRoles(roleDefinitions);
            mayEnd = relationshipMayEnd;
            allowSelfRelationship = allowSelf;
            canonicalizeSymmetricParticipants = relationshipDirectionality == RelationshipDirectionality.Symmetric;
            defaultAccessPolicyId = accessPolicyId ?? string.Empty;
            tags = Clean(tagIds);
            version = 1;
        }

        public bool HasRole(string roleId)
        {
            return !string.IsNullOrWhiteSpace(roleId)
                && Roles.Any(role => string.Equals(role.roleId, roleId, StringComparison.Ordinal));
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError($"Relationship Definition '{name}' is missing a stable ID.");
            }
            else if (!Id.StartsWith("relationship.", StringComparison.Ordinal))
            {
                report.AddWarning($"Relationship Definition '{Id}' should use the 'relationship.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(RelationshipCategory), category))
            {
                report.AddError($"Relationship Definition '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(RelationshipDirectionality), directionality))
            {
                report.AddError($"Relationship Definition '{DisplayName}' has invalid directionality '{directionality}'.");
            }

            if (!Enum.IsDefined(typeof(RelationshipDuplicatePolicy), duplicatePolicy))
            {
                report.AddError($"Relationship Definition '{DisplayName}' has invalid duplicate policy '{duplicatePolicy}'.");
            }

            if (version < 1)
            {
                report.AddError($"Relationship Definition '{DisplayName}' has invalid version '{version}'.");
            }

            RelationshipRoleDefinitionData[] effectiveRoles = roles ?? Array.Empty<RelationshipRoleDefinitionData>();
            if (effectiveRoles.Length == 0)
            {
                report.AddError($"Relationship Definition '{DisplayName}' must declare at least one role.");
            }

            HashSet<string> roleIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RelationshipRoleDefinitionData role in effectiveRoles)
            {
                if (role == null || string.IsNullOrWhiteSpace(role.roleId))
                {
                    report.AddError($"Relationship Definition '{DisplayName}' contains a blank role ID.");
                    continue;
                }

                if (!roleIds.Add(role.roleId.Trim()))
                {
                    report.AddError($"Relationship Definition '{DisplayName}' contains duplicate role ID '{role.roleId}'.");
                }
            }

            if (directionality != RelationshipDirectionality.Symmetric && effectiveRoles.Count(role => role != null && role.required) < 2)
            {
                report.AddError($"Relationship Definition '{DisplayName}' is directed and must declare at least two required roles.");
            }

            if (!string.IsNullOrWhiteSpace(DefaultAccessPolicyId)
                && definitionsById != null
                && (!definitionsById.TryGetValue(DefaultAccessPolicyId, out IGameDefinition policyDefinition)
                    || policyDefinition is not InformationAccessPolicyDefinition))
            {
                report.AddError($"Relationship Definition '{DisplayName}' references missing Information Access policy '{DefaultAccessPolicyId}'.");
            }
        }

        private static RelationshipRoleDefinitionData[] CleanRoles(IEnumerable<RelationshipRoleDefinitionData> values)
        {
            return (values ?? Array.Empty<RelationshipRoleDefinitionData>())
                .Where(value => value != null && !string.IsNullOrWhiteSpace(value.roleId))
                .Select(value =>
                {
                    RelationshipRoleDefinitionData clone = value.Clone();
                    clone.roleId = clone.roleId.Trim();
                    clone.displayName = string.IsNullOrWhiteSpace(clone.displayName) ? clone.roleId : clone.displayName.Trim();
                    return clone;
                })
                .GroupBy(value => value.roleId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(value => value.roleId, StringComparer.Ordinal)
                .ToArray();
        }

        private static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
