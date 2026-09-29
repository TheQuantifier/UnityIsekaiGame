using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Professions;

namespace UnityIsekaiGame.Organizations
{
    [CreateAssetMenu(fileName = "OrganizationMembershipDefinition", menuName = "Unity Isekai Game/Organizations/Membership Definition")]
    public sealed class OrganizationMembershipDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string membershipDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField] private OrganizationMembershipCategory category = OrganizationMembershipCategory.FullMember;
        [SerializeField] private string[] applicableOrganizationDefinitionIds = Array.Empty<string>();
        [SerializeField] private OrganizationCategory[] applicableOrganizationCategories = Array.Empty<OrganizationCategory>();
        [SerializeField] private OrganizationMembershipStatus initialStatus = OrganizationMembershipStatus.Active;
        [SerializeField] private OrganizationMembershipMultiplicityPolicy multiplicityPolicy = OrganizationMembershipMultiplicityPolicy.OneActivePerPersonOrganizationDefinition;
        [SerializeField] private bool supportsRanks = true;
        [SerializeField] private bool requiresRank;
        [SerializeField] private bool supportsOffices = true;
        [SerializeField] private bool allowApplication = true;
        [SerializeField] private bool allowInvitation = true;
        [SerializeField] private bool requiresExplicitAcceptance = true;
        [SerializeField] private bool allowSuspension = true;
        [SerializeField] private bool allowReinstatement = true;
        [SerializeField] private bool allowResignation = true;
        [SerializeField] private bool allowRemoval = true;
        [SerializeField] private bool retainHistory = true;
        [SerializeField] private bool allowBranchMembership = true;
        [SerializeField] private bool requireParentMembershipForBranch;
        [SerializeField] private bool inheritParentMembership;
        [SerializeField] private OrganizationVisibility visibility = OrganizationVisibility.Public;
        [SerializeField] private string accessPolicyId;
        [SerializeField] private string[] requiredCredentialDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredProfessionalRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredCapabilityIds = Array.Empty<string>();
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private int version = 1;

        public string Id => membershipDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string Description => description ?? string.Empty;
        public OrganizationMembershipCategory Category => category;
        public IReadOnlyList<string> ApplicableOrganizationDefinitionIds => Clean(applicableOrganizationDefinitionIds);
        public IReadOnlyList<OrganizationCategory> ApplicableOrganizationCategories => CleanCategories(applicableOrganizationCategories);
        public OrganizationMembershipStatus InitialStatus => initialStatus;
        public OrganizationMembershipMultiplicityPolicy MultiplicityPolicy => multiplicityPolicy;
        public bool SupportsRanks => supportsRanks;
        public bool RequiresRank => requiresRank;
        public bool SupportsOffices => supportsOffices;
        public bool AllowApplication => allowApplication;
        public bool AllowInvitation => allowInvitation;
        public bool RequiresExplicitAcceptance => requiresExplicitAcceptance;
        public bool AllowSuspension => allowSuspension;
        public bool AllowReinstatement => allowReinstatement;
        public bool AllowResignation => allowResignation;
        public bool AllowRemoval => allowRemoval;
        public bool RetainHistory => retainHistory;
        public bool AllowBranchMembership => allowBranchMembership;
        public bool RequireParentMembershipForBranch => requireParentMembershipForBranch;
        public bool InheritParentMembership => inheritParentMembership;
        public OrganizationVisibility Visibility => visibility;
        public string AccessPolicyId => accessPolicyId ?? string.Empty;
        public IReadOnlyList<string> RequiredCredentialDefinitionIds => Clean(requiredCredentialDefinitionIds);
        public IReadOnlyList<string> RequiredProfessionalRankDefinitionIds => Clean(requiredProfessionalRankDefinitionIds);
        public IReadOnlyList<string> RequiredCapabilityIds => Clean(requiredCapabilityIds);
        public IReadOnlyList<string> TagIds => Clean(tags);
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string id,
            string name,
            OrganizationMembershipCategory membershipCategory,
            IEnumerable<string> organizationDefinitions = null,
            IEnumerable<OrganizationCategory> organizationCategories = null,
            OrganizationMembershipStatus defaultStatus = OrganizationMembershipStatus.Active,
            OrganizationMembershipMultiplicityPolicy multiplicity = OrganizationMembershipMultiplicityPolicy.OneActivePerPersonOrganizationDefinition,
            bool rankSupport = true,
            bool rankRequired = false,
            bool officeSupport = true,
            bool application = true,
            bool invitation = true,
            bool acceptance = true,
            bool suspension = true,
            bool reinstatement = true,
            bool resignation = true,
            bool removal = true,
            bool history = true,
            bool branchMembership = true,
            bool requireParent = false,
            bool inheritParent = false,
            OrganizationVisibility membershipVisibility = OrganizationVisibility.Public,
            string policyId = "",
            IEnumerable<string> credentials = null,
            IEnumerable<string> professionalRanks = null,
            IEnumerable<string> capabilities = null,
            IEnumerable<string> tagIds = null)
        {
            membershipDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            description = string.Empty;
            category = membershipCategory;
            applicableOrganizationDefinitionIds = Clean(organizationDefinitions).ToArray();
            applicableOrganizationCategories = CleanCategories(organizationCategories).ToArray();
            initialStatus = defaultStatus;
            multiplicityPolicy = multiplicity;
            supportsRanks = rankSupport;
            requiresRank = rankRequired;
            supportsOffices = officeSupport;
            allowApplication = application;
            allowInvitation = invitation;
            requiresExplicitAcceptance = acceptance;
            allowSuspension = suspension;
            allowReinstatement = reinstatement;
            allowResignation = resignation;
            allowRemoval = removal;
            retainHistory = history;
            allowBranchMembership = branchMembership;
            requireParentMembershipForBranch = requireParent;
            inheritParentMembership = inheritParent;
            visibility = membershipVisibility;
            accessPolicyId = policyId ?? string.Empty;
            requiredCredentialDefinitionIds = Clean(credentials).ToArray();
            requiredProfessionalRankDefinitionIds = Clean(professionalRanks).ToArray();
            requiredCapabilityIds = Clean(capabilities).ToArray();
            tags = Clean(tagIds).ToArray();
            version = 1;
        }

        public bool AppliesTo(OrganizationDefinition organizationDefinition)
        {
            if (organizationDefinition == null)
            {
                return false;
            }

            return ApplicableOrganizationDefinitionIds.Count == 0 && ApplicableOrganizationCategories.Count == 0
                || ApplicableOrganizationDefinitionIds.Contains(organizationDefinition.Id)
                || ApplicableOrganizationCategories.Contains(organizationDefinition.Category);
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Organization Membership definition has no stable ID.");
            }
            else if (!Id.StartsWith("organization-membership.", StringComparison.Ordinal))
            {
                report.AddWarning($"Organization Membership definition '{DisplayName}' should use the 'organization-membership.' namespace prefix.");
            }

            if (!Enum.IsDefined(typeof(OrganizationMembershipCategory), category) || category == OrganizationMembershipCategory.Unknown)
            {
                report.AddError($"Organization Membership definition '{DisplayName}' has invalid category '{category}'.");
            }

            if (!Enum.IsDefined(typeof(OrganizationMembershipStatus), initialStatus) || !IsLiveOrPending(initialStatus))
            {
                report.AddError($"Organization Membership definition '{DisplayName}' has invalid initial status '{initialStatus}'.");
            }

            foreach (string organizationDefinitionId in ApplicableOrganizationDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(organizationDefinitionId, out IGameDefinition organizationDefinition) || organizationDefinition is not OrganizationDefinition)
                {
                    report.AddError($"Organization Membership definition '{DisplayName}' references missing Organization Definition '{organizationDefinitionId}'.");
                }
            }

            foreach (OrganizationCategory organizationCategory in ApplicableOrganizationCategories)
            {
                if (!Enum.IsDefined(typeof(OrganizationCategory), organizationCategory) || organizationCategory == OrganizationCategory.Unknown)
                {
                    report.AddError($"Organization Membership definition '{DisplayName}' references invalid Organization category '{organizationCategory}'.");
                }
            }

            foreach (string credentialId in RequiredCredentialDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(credentialId, out IGameDefinition credential) || credential is not CredentialDefinition)
                {
                    report.AddError($"Organization Membership definition '{DisplayName}' references missing Credential '{credentialId}'.");
                }
            }

            foreach (string rankId in RequiredProfessionalRankDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(rankId, out IGameDefinition rank) || rank is not ProfessionalRankDefinition)
                {
                    report.AddError($"Organization Membership definition '{DisplayName}' references missing Professional Rank '{rankId}'.");
                }
            }

            if (requiresRank && !supportsRanks)
            {
                report.AddError($"Organization Membership definition '{DisplayName}' requires ranks but does not support ranks.");
            }

            if (version <= 0)
            {
                report.AddError($"Organization Membership definition '{DisplayName}' has invalid version.");
            }
        }

        private static bool IsLiveOrPending(OrganizationMembershipStatus status)
        {
            return status == OrganizationMembershipStatus.Applied
                || status == OrganizationMembershipStatus.Invited
                || status == OrganizationMembershipStatus.PendingAcceptance
                || status == OrganizationMembershipStatus.Provisional
                || status == OrganizationMembershipStatus.Active;
        }

        internal static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static OrganizationCategory[] CleanCategories(IEnumerable<OrganizationCategory> values)
        {
            return (values ?? Array.Empty<OrganizationCategory>())
                .Where(value => value != OrganizationCategory.Unknown)
                .Distinct()
                .OrderBy(value => value.ToString(), StringComparer.Ordinal)
                .ToArray();
        }
    }

    [CreateAssetMenu(fileName = "OrganizationRankTrackDefinition", menuName = "Unity Isekai Game/Organizations/Rank Track Definition")]
    public class OrganizationRankTrackDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string rankTrackDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private string organizationDefinitionId;
        [SerializeField] private string[] supportedMembershipDefinitionIds = Array.Empty<string>();
        [SerializeField] private bool allowMultipleActiveRanks;
        [SerializeField] private bool allowSkipping;
        [SerializeField] private bool secret;
        [SerializeField] private int version = 1;

        public string Id => rankTrackDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string OrganizationDefinitionId => organizationDefinitionId ?? string.Empty;
        public IReadOnlyList<string> SupportedMembershipDefinitionIds => OrganizationMembershipDefinition.Clean(supportedMembershipDefinitionIds);
        public bool AllowMultipleActiveRanks => allowMultipleActiveRanks;
        public bool AllowSkipping => allowSkipping;
        public bool Secret => secret;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, string organizationDefinition, IEnumerable<string> memberships = null, bool multipleActive = false, bool skipping = false, bool isSecret = false)
        {
            rankTrackDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            organizationDefinitionId = organizationDefinition ?? string.Empty;
            supportedMembershipDefinitionIds = OrganizationMembershipDefinition.Clean(memberships).ToArray();
            allowMultipleActiveRanks = multipleActive;
            allowSkipping = skipping;
            secret = isSecret;
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Organization Rank Track definition has no stable ID.");
            }
            else if (!Id.StartsWith("organization-rank-track.", StringComparison.Ordinal))
            {
                report.AddWarning($"Organization Rank Track definition '{DisplayName}' should use the 'organization-rank-track.' namespace prefix.");
            }

            if (!string.IsNullOrWhiteSpace(OrganizationDefinitionId) && (definitionsById == null || !definitionsById.TryGetValue(OrganizationDefinitionId, out IGameDefinition organization) || organization is not OrganizationDefinition))
            {
                report.AddError($"Organization Rank Track definition '{DisplayName}' references missing Organization Definition '{OrganizationDefinitionId}'.");
            }

            foreach (string membershipId in SupportedMembershipDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(membershipId, out IGameDefinition membership) || membership is not OrganizationMembershipDefinition)
                {
                    report.AddError($"Organization Rank Track definition '{DisplayName}' references missing Membership Definition '{membershipId}'.");
                }
            }
        }
    }

    [CreateAssetMenu(fileName = "OrganizationRankDefinition", menuName = "Unity Isekai Game/Organizations/Rank Definition")]
    public class OrganizationRankDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string rankDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private string rankTrackDefinitionId;
        [SerializeField] private int rankOrder;
        [SerializeField] private string[] priorRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] applicableOrganizationIds = Array.Empty<string>();
        [SerializeField] private string[] equivalentProfessionalRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private bool terminalRank;
        [SerializeField] private bool secret;
        [SerializeField] private int version = 1;

        public string Id => rankDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string RankTrackDefinitionId => rankTrackDefinitionId ?? string.Empty;
        public int RankOrder => Math.Max(0, rankOrder);
        public IReadOnlyList<string> PriorRankDefinitionIds => OrganizationMembershipDefinition.Clean(priorRankDefinitionIds);
        public IReadOnlyList<string> ApplicableOrganizationIds => OrganizationMembershipDefinition.Clean(applicableOrganizationIds);
        public IReadOnlyList<string> EquivalentProfessionalRankDefinitionIds => OrganizationMembershipDefinition.Clean(equivalentProfessionalRankDefinitionIds);
        public bool TerminalRank => terminalRank;
        public bool Secret => secret;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(string id, string name, string trackId, int order, IEnumerable<string> priorRanks = null, IEnumerable<string> professionalRanks = null, bool terminal = false, bool isSecret = false, IEnumerable<string> organizationIds = null)
        {
            rankDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            rankTrackDefinitionId = trackId ?? string.Empty;
            rankOrder = Math.Max(0, order);
            priorRankDefinitionIds = OrganizationMembershipDefinition.Clean(priorRanks).ToArray();
            applicableOrganizationIds = OrganizationMembershipDefinition.Clean(organizationIds).ToArray();
            equivalentProfessionalRankDefinitionIds = OrganizationMembershipDefinition.Clean(professionalRanks).ToArray();
            terminalRank = terminal;
            secret = isSecret;
            version = 1;
        }

        public bool AppliesToOrganization(string organizationId)
        {
            return ApplicableOrganizationIds.Count == 0
                || ApplicableOrganizationIds.Contains(string.IsNullOrWhiteSpace(organizationId) ? string.Empty : organizationId.Trim());
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Organization Rank definition has no stable ID.");
            }
            else if (!Id.StartsWith("organization-rank.", StringComparison.Ordinal))
            {
                report.AddWarning($"Organization Rank definition '{DisplayName}' should use the 'organization-rank.' namespace prefix.");
            }

            if (definitionsById == null || !definitionsById.TryGetValue(RankTrackDefinitionId, out IGameDefinition track) || track is not OrganizationRankTrackDefinition)
            {
                report.AddError($"Organization Rank definition '{DisplayName}' references missing Rank Track '{RankTrackDefinitionId}'.");
            }

            foreach (string priorRankId in PriorRankDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(priorRankId, out IGameDefinition rank) || rank is not OrganizationRankDefinition)
                {
                    report.AddError($"Organization Rank definition '{DisplayName}' references missing prior Organization Rank '{priorRankId}'.");
                }
            }

            foreach (string professionalRankId in EquivalentProfessionalRankDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(professionalRankId, out IGameDefinition professionalRank) || professionalRank is not ProfessionalRankDefinition)
                {
                    report.AddError($"Organization Rank definition '{DisplayName}' references missing Professional Rank '{professionalRankId}'.");
                }
            }
        }
    }

    [CreateAssetMenu(fileName = "OrganizationOfficeDefinition", menuName = "Unity Isekai Game/Organizations/Office Definition")]
    public class OrganizationOfficeDefinition : ScriptableObject, IGameDefinition, IDefinitionCatalogValidationParticipant
    {
        [SerializeField] private string officeDefinitionId;
        [SerializeField] private string displayName;
        [SerializeField] private string organizationDefinitionId;
        [SerializeField] private string linkedPositionDefinitionId;
        [SerializeField] private string[] requiredMembershipDefinitionIds = Array.Empty<string>();
        [SerializeField] private string[] requiredRankDefinitionIds = Array.Empty<string>();
        [SerializeField] private int maximumActiveHolders = 1;
        [SerializeField] private bool allowVacancy = true;
        [SerializeField] private bool allowActingHolders = true;
        [SerializeField] private bool allowJointHolders;
        [SerializeField] private bool allowTermEnd = true;
        [SerializeField] private bool secret;
        [SerializeField] private int version = 1;

        public string Id => officeDefinitionId ?? string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? Id : displayName;
        public string OrganizationDefinitionId => organizationDefinitionId ?? string.Empty;
        public string LinkedPositionDefinitionId => linkedPositionDefinitionId ?? string.Empty;
        public IReadOnlyList<string> RequiredMembershipDefinitionIds => OrganizationMembershipDefinition.Clean(requiredMembershipDefinitionIds);
        public IReadOnlyList<string> RequiredRankDefinitionIds => OrganizationMembershipDefinition.Clean(requiredRankDefinitionIds);
        public int MaximumActiveHolders => Math.Max(1, maximumActiveHolders);
        public bool AllowVacancy => allowVacancy;
        public bool AllowActingHolders => allowActingHolders;
        public bool AllowJointHolders => allowJointHolders;
        public bool AllowTermEnd => allowTermEnd;
        public bool Secret => secret;
        public int Version => Math.Max(1, version);

        public void DevelopmentConfigure(
            string id,
            string name,
            string organizationDefinition,
            string positionDefinition = "",
            IEnumerable<string> memberships = null,
            IEnumerable<string> ranks = null,
            int maximumHolders = 1,
            bool vacancy = true,
            bool acting = true,
            bool joint = false,
            bool termEnd = true,
            bool isSecret = false)
        {
            officeDefinitionId = id?.Trim();
            displayName = string.IsNullOrWhiteSpace(name) ? id : name.Trim();
            organizationDefinitionId = organizationDefinition ?? string.Empty;
            linkedPositionDefinitionId = positionDefinition ?? string.Empty;
            requiredMembershipDefinitionIds = OrganizationMembershipDefinition.Clean(memberships).ToArray();
            requiredRankDefinitionIds = OrganizationMembershipDefinition.Clean(ranks).ToArray();
            maximumActiveHolders = Math.Max(1, maximumHolders);
            allowVacancy = vacancy;
            allowActingHolders = acting;
            allowJointHolders = joint || maximumHolders > 1;
            allowTermEnd = termEnd;
            secret = isSecret;
            version = 1;
        }

        public void ValidateCatalogDefinition(IReadOnlyDictionary<string, IGameDefinition> definitionsById, DefinitionValidationReport report)
        {
            if (report == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(Id))
            {
                report.AddError("Organization Office definition has no stable ID.");
            }
            else if (!Id.StartsWith("organization-office.", StringComparison.Ordinal))
            {
                report.AddWarning($"Organization Office definition '{DisplayName}' should use the 'organization-office.' namespace prefix.");
            }

            if (!string.IsNullOrWhiteSpace(OrganizationDefinitionId) && (definitionsById == null || !definitionsById.TryGetValue(OrganizationDefinitionId, out IGameDefinition organization) || organization is not OrganizationDefinition))
            {
                report.AddError($"Organization Office definition '{DisplayName}' references missing Organization Definition '{OrganizationDefinitionId}'.");
            }

            if (!string.IsNullOrWhiteSpace(LinkedPositionDefinitionId) && (definitionsById == null || !definitionsById.TryGetValue(LinkedPositionDefinitionId, out IGameDefinition position) || position is not PositionDefinition))
            {
                report.AddError($"Organization Office definition '{DisplayName}' references missing Position Definition '{LinkedPositionDefinitionId}'.");
            }

            foreach (string membershipId in RequiredMembershipDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(membershipId, out IGameDefinition membership) || membership is not OrganizationMembershipDefinition)
                {
                    report.AddError($"Organization Office definition '{DisplayName}' references missing Membership Definition '{membershipId}'.");
                }
            }

            foreach (string rankId in RequiredRankDefinitionIds)
            {
                if (definitionsById == null || !definitionsById.TryGetValue(rankId, out IGameDefinition rank) || rank is not OrganizationRankDefinition)
                {
                    report.AddError($"Organization Office definition '{DisplayName}' references missing Organization Rank '{rankId}'.");
                }
            }

            if (!AllowJointHolders && MaximumActiveHolders > 1)
            {
                report.AddError($"Organization Office definition '{DisplayName}' has multiple holders but disallows joint holders.");
            }
        }
    }
}
