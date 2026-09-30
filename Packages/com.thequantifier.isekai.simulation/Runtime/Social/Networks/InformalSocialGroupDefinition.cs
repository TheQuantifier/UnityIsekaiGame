using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Social.Norms;
using UnityIsekaiGame.Social.Reputation;

namespace UnityIsekaiGame.Social.Networks
{
    [CreateAssetMenu(fileName = "InformalSocialGroupDefinition", menuName = "Unity Isekai Game/Social/Informal Social Group Definition")]
    public sealed class InformalSocialGroupDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string groupDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private InformalSocialGroupCategory category = InformalSocialGroupCategory.FriendCircle;
        [SerializeField] private int minimumMembers = 2;
        [SerializeField] private int maximumMembers = 12;
        [SerializeField] private bool requiresLeader;
        [SerializeField] private bool allowsMultipleLeaders;
        [SerializeField] private bool preserveMembershipHistory = true;
        [SerializeField] private bool mayDissolve = true;
        [SerializeField] private InformalSocialGroupVisibility visibility = InformalSocialGroupVisibility.Public;
        [SerializeField] private string associatedAudienceId;
        [SerializeField] private string[] associatedNormDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] associatedProjectionDefinitionIds = Array.Empty<string>();
        [SerializeField] private SocialNetworkGroupRoleDefinitionData[] roles = Array.Empty<SocialNetworkGroupRoleDefinitionData>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => groupDefinitionId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description ?? string.Empty;
        public InformalSocialGroupCategory Category => category;
        public int MinimumMembers => minimumMembers;
        public int MaximumMembers => maximumMembers;
        public bool RequiresLeader => requiresLeader;
        public bool AllowsMultipleLeaders => allowsMultipleLeaders;
        public bool PreserveMembershipHistory => preserveMembershipHistory;
        public bool MayDissolve => mayDissolve;
        public InformalSocialGroupVisibility Visibility => visibility;
        public string AssociatedAudienceId => associatedAudienceId ?? string.Empty;
        public IReadOnlyList<string> AssociatedNormDefinitionIds => associatedNormDefinitionIds ?? Array.Empty<string>();
        public IReadOnlyList<string> AssociatedProjectionDefinitionIds => associatedProjectionDefinitionIds ?? Array.Empty<string>();
        public IReadOnlyList<SocialNetworkGroupRoleDefinitionData> Roles => roles ?? Array.Empty<SocialNetworkGroupRoleDefinitionData>();
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public int Version => version;

        private void OnValidate()
        {
            groupDefinitionId = groupDefinitionId?.Trim();
            minimumMembers = Math.Max(1, minimumMembers);
            maximumMembers = Math.Max(minimumMembers, maximumMembers);
            version = Math.Max(1, version);
            associatedAudienceId = associatedAudienceId?.Trim();
            associatedNormDefinitionIds = Clean(associatedNormDefinitionIds);
            associatedProjectionDefinitionIds = Clean(associatedProjectionDefinitionIds);
            tags = Clean(tags);
        }

        public void DevelopmentConfigure(
            string id,
            string name,
            InformalSocialGroupCategory groupCategory,
            int minMembers,
            int maxMembers,
            bool leaderRequired,
            bool multipleLeaders,
            InformalSocialGroupVisibility groupVisibility,
            IEnumerable<SocialNetworkGroupRoleDefinitionData> supportedRoles,
            string audienceId = "",
            IEnumerable<string> normIds = null,
            IEnumerable<string> projectionIds = null,
            bool preserveHistory = true,
            bool dissolvable = true,
            string text = "",
            IEnumerable<string> tagIds = null)
        {
            groupDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name;
            description = text ?? string.Empty;
            category = groupCategory;
            minimumMembers = Math.Max(1, minMembers);
            maximumMembers = Math.Max(minimumMembers, maxMembers);
            requiresLeader = leaderRequired;
            allowsMultipleLeaders = multipleLeaders;
            visibility = groupVisibility;
            roles = (supportedRoles ?? Array.Empty<SocialNetworkGroupRoleDefinitionData>()).Select(item => item?.Clone()).Where(item => item != null).OrderBy(item => item.roleId, StringComparer.Ordinal).ToArray();
            associatedAudienceId = audienceId?.Trim() ?? string.Empty;
            associatedNormDefinitionIds = Clean(normIds);
            associatedProjectionDefinitionIds = Clean(projectionIds);
            preserveMembershipHistory = preserveHistory;
            mayDissolve = dissolvable;
            tags = Clean(tagIds);
            version = 1;
        }

        public bool SupportsRole(string roleId) => Roles.Any(item => string.Equals(item.roleId, roleId, StringComparison.Ordinal));
        public bool IsLeaderRole(string roleId) => Roles.Any(item => string.Equals(item.roleId, roleId, StringComparison.Ordinal) && item.leaderRole);

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null) return;
            if (string.IsNullOrWhiteSpace(Id)) report.AddError($"Informal Social Group Definition '{name}' is missing a stable ID.");
            else if (!Id.StartsWith("social-group.", StringComparison.Ordinal)) report.AddWarning($"Informal Social Group Definition '{Id}' should use the 'social-group.' namespace prefix.");
            if (!Enum.IsDefined(typeof(InformalSocialGroupCategory), category)) report.AddError($"Informal Social Group Definition '{DisplayName}' has invalid category '{category}'.");
            if (!Enum.IsDefined(typeof(InformalSocialGroupVisibility), visibility)) report.AddError($"Informal Social Group Definition '{DisplayName}' has invalid visibility '{visibility}'.");
            if (minimumMembers < 1 || maximumMembers < minimumMembers) report.AddError($"Informal Social Group Definition '{DisplayName}' has invalid membership bounds.");
            if (Roles.Count == 0) report.AddError($"Informal Social Group Definition '{DisplayName}' must declare at least one member role.");
            if (requiresLeader && !Roles.Any(item => item.leaderRole)) report.AddError($"Informal Social Group Definition '{DisplayName}' requires a leader role but none is declared.");
            if (!allowsMultipleLeaders && Roles.Count(item => item.leaderRole) > 1) report.AddError($"Informal Social Group Definition '{DisplayName}' declares multiple leader roles while multi-leader support is disabled.");
            foreach (SocialNetworkGroupRoleDefinitionData role in Roles)
            {
                if (string.IsNullOrWhiteSpace(role?.roleId)) report.AddError($"Informal Social Group Definition '{DisplayName}' contains a blank role ID.");
            }
            ValidateReferences(definitionsById, report);
        }

        private void ValidateReferences(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (!string.IsNullOrWhiteSpace(AssociatedAudienceId) && (definitionsById == null || !definitionsById.TryGetValue(AssociatedAudienceId, out IGameDefinition audience) || audience is not ReputationAudienceDefinition))
            {
                report.AddError($"Informal Social Group Definition '{DisplayName}' references missing reputation audience '{AssociatedAudienceId}'.");
            }
            foreach (string normId in AssociatedNormDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(normId, out IGameDefinition norm) || norm is not SocialNormDefinition)
                {
                    report.AddError($"Informal Social Group Definition '{DisplayName}' references missing social norm '{normId}'.");
                }
            }
            foreach (string projectionId in AssociatedProjectionDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(projectionId, out IGameDefinition projection) || projection is not SocialGraphProjectionDefinition)
                {
                    report.AddError($"Informal Social Group Definition '{DisplayName}' references missing graph projection '{projectionId}'.");
                }
            }
        }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }
}
