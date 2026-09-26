using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Social.Family;

namespace UnityIsekaiGame.Governments
{
    public sealed partial class GovernmentRuntime : IDisposable
    {
        private readonly Dictionary<string, PolityRecordData> politiesById = new Dictionary<string, PolityRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoliticalNameRecordData> namesById = new Dictionary<string, PoliticalNameRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentRecordData> governmentsById = new Dictionary<string, GovernmentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentInstitutionRoleRecordData> institutionRolesById = new Dictionary<string, GovernmentInstitutionRoleRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoliticalTerritoryRecordData> territoriesById = new Dictionary<string, PoliticalTerritoryRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TerritoryPlaceMembershipRecordData> territoryPlaceMembershipsById = new Dictionary<string, TerritoryPlaceMembershipRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TerritorialClaimRecordData> claimsById = new Dictionary<string, TerritorialClaimRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TerritorialControlRecordData> controlsById = new Dictionary<string, TerritorialControlRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, TerritoryAdministrationRecordData> administrationsById = new Dictionary<string, TerritoryAdministrationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentSeatRecordData> seatsById = new Dictionary<string, GovernmentSeatRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, SovereigntyClaimRecordData> sovereigntyClaimsById = new Dictionary<string, SovereigntyClaimRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, JurisdictionRecordData> jurisdictionsById = new Dictionary<string, JurisdictionRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentOrganizationCharterRecordData> organizationChartersById = new Dictionary<string, GovernmentOrganizationCharterRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoliticalTransitionPlanRecordData> transitionsById = new Dictionary<string, PoliticalTransitionPlanRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, PoliticalTransactionRecordData> transactionsById = new Dictionary<string, PoliticalTransactionRecordData>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private OrganizationRuntime organizations;
        private OrganizationMembershipRuntime memberships;
        private OrganizationAuthorityRuntime authority;
        private OrganizationDecisionRuntime decisions;
        private OrganizationResourceRuntime resources;
        private FactionRuntime factions;
        private DiplomacyRuntime diplomacy;
        private PropertyRuntime properties;
        private FamilyRelationshipRuntime familyRelationships;
        private string worldId = string.Empty;
        private HashSet<string> knownPersonIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> knownPlaceIds = new HashSet<string>(StringComparer.Ordinal);
        private bool disposed;

        public long Revision { get; private set; }
        public int PolityCount => politiesById.Count;
        public int GovernmentCount => governmentsById.Count;
        public int TerritoryCount => territoriesById.Count;
        public int ClaimCount => claimsById.Count;
        public int JurisdictionCount => jurisdictionsById.Count;
        public int OrganizationCharterCount => organizationChartersById.Count;

        public event Action<PoliticalOperationResult> OperationCommitted;

        public IReadOnlyList<PolityRecordData> Polities => politiesById.Values.OrderBy(item => item.foundingWorldTime).ThenBy(item => item.polityId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<GovernmentRecordData> Governments => governmentsById.Values.OrderBy(item => item.establishedWorldTime).ThenBy(item => item.governmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PoliticalTerritoryRecordData> Territories => territoriesById.Values.OrderBy(item => item.createdWorldTime).ThenBy(item => item.territoryId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<JurisdictionRecordData> Jurisdictions => jurisdictionsById.Values.OrderByDescending(item => item.priority).ThenBy(item => item.effectiveWorldTime).ThenBy(item => item.jurisdictionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<GovernmentOrganizationCharterRecordData> OrganizationCharters => organizationChartersById.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.charterId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PoliticalNameRecordData> Names => namesById.Values.OrderBy(item => item.effectiveStartWorldTime).ThenBy(item => item.nameRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<GovernmentInstitutionRoleRecordData> InstitutionRoles => institutionRolesById.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.roleId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TerritoryPlaceMembershipRecordData> TerritoryPlaceMemberships => territoryPlaceMembershipsById.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.membershipId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TerritorialClaimRecordData> Claims => claimsById.Values.OrderBy(item => item.assertedWorldTime).ThenBy(item => item.claimId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TerritorialControlRecordData> Controls => controlsById.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.controlId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TerritoryAdministrationRecordData> Administrations => administrationsById.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.administrationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<GovernmentSeatRecordData> Seats => seatsById.Values.OrderBy(item => item.effectiveWorldTime).ThenBy(item => item.seatId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<SovereigntyClaimRecordData> SovereigntyClaims => sovereigntyClaimsById.Values.OrderBy(item => item.assertedWorldTime).ThenBy(item => item.sovereigntyClaimId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PoliticalTransitionPlanRecordData> Transitions => transitionsById.Values.OrderBy(item => item.plannedWorldTime).ThenBy(item => item.transitionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public IReadOnlyList<GovernmentRecordData> GetGovernmentsForPolity(string polityId) => governmentsById.Values.Where(item => string.Equals(item.polityId, PoliticalModelUtility.Normalize(polityId), StringComparison.Ordinal)).OrderBy(item => item.level).ThenBy(item => item.establishedWorldTime).ThenBy(item => item.governmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<PoliticalTerritoryRecordData> GetTerritoriesForPolity(string polityId) => territoriesById.Values.Where(item => string.Equals(item.polityId, PoliticalModelUtility.Normalize(polityId), StringComparison.Ordinal)).OrderBy(item => item.createdWorldTime).ThenBy(item => item.territoryId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<TerritorialClaimRecordData> GetClaimsForTerritory(string territoryId) => claimsById.Values.Where(item => string.Equals(item.territoryId, PoliticalModelUtility.Normalize(territoryId), StringComparison.Ordinal)).OrderBy(item => item.assertedWorldTime).ThenBy(item => item.claimId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<JurisdictionRecordData> GetJurisdictionsForGovernment(string governmentId) => jurisdictionsById.Values.Where(item => string.Equals(item.governmentId, PoliticalModelUtility.Normalize(governmentId), StringComparison.Ordinal)).OrderByDescending(item => item.priority).ThenBy(item => item.effectiveWorldTime).ThenBy(item => item.jurisdictionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<GovernmentOrganizationCharterRecordData> GetOrganizationChartersForGovernment(string governmentId) => organizationChartersById.Values.Where(item => string.Equals(item.governmentId, PoliticalModelUtility.Normalize(governmentId), StringComparison.Ordinal)).OrderBy(item => item.category).ThenBy(item => item.organizationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<GovernmentOrganizationCharterRecordData> GetAgenciesForGovernment(string governmentId) => organizationChartersById.Values.Where(item => string.Equals(item.governmentId, PoliticalModelUtility.Normalize(governmentId), StringComparison.Ordinal) && item.category == GovernmentOrganizationCharterCategory.GovernmentAgency && item.lifecycleState == GovernmentOrganizationCharterLifecycleState.Active).OrderBy(item => item.organizationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(
            DefinitionRegistry definitionRegistry,
            OrganizationRuntime organizationRuntime,
            OrganizationMembershipRuntime membershipRuntime,
            OrganizationAuthorityRuntime authorityRuntime,
            OrganizationDecisionRuntime decisionRuntime,
            OrganizationResourceRuntime resourceRuntime,
            FactionRuntime factionRuntime,
            DiplomacyRuntime diplomacyRuntime,
            PropertyRuntime propertyRuntime,
            string runtimeWorldId,
            IEnumerable<string> personIds,
            IEnumerable<string> placeIds,
            FamilyRelationshipRuntime familyRelationshipRuntime = null,
            EconomyRuntime economyRuntime = null,
            InstitutionalRevenueRuntime institutionalRevenueRuntime = null)
        {
            registry = definitionRegistry ?? registry;
            organizations = organizationRuntime;
            memberships = membershipRuntime;
            authority = authorityRuntime;
            decisions = decisionRuntime;
            resources = resourceRuntime;
            factions = factionRuntime;
            diplomacy = diplomacyRuntime;
            properties = propertyRuntime;
            familyRelationships = familyRelationshipRuntime ?? familyRelationships;
            ConfigureFiscalDependencies(economyRuntime, institutionalRevenueRuntime);
            worldId = PoliticalModelUtility.Normalize(runtimeWorldId);
            knownPersonIds = new HashSet<string>(PoliticalModelUtility.Clean(personIds), StringComparer.Ordinal);
            knownPlaceIds = new HashSet<string>(PoliticalModelUtility.Clean(placeIds), StringComparer.Ordinal);
        }

        public bool TryGetPolity(string polityId, out PolityRecordData polity)
        {
            if (politiesById.TryGetValue(PoliticalModelUtility.Normalize(polityId), out PolityRecordData found))
            {
                polity = found.Clone();
                return true;
            }

            polity = null;
            return false;
        }

        public bool TryGetGovernment(string governmentId, out GovernmentRecordData government)
        {
            if (governmentsById.TryGetValue(PoliticalModelUtility.Normalize(governmentId), out GovernmentRecordData found))
            {
                government = found.Clone();
                return true;
            }

            government = null;
            return false;
        }

        public bool TryGetTerritory(string territoryId, out PoliticalTerritoryRecordData territory)
        {
            if (territoriesById.TryGetValue(PoliticalModelUtility.Normalize(territoryId), out PoliticalTerritoryRecordData found))
            {
                territory = found.Clone();
                return true;
            }

            territory = null;
            return false;
        }

        public bool TryGetClaim(string claimId, out TerritorialClaimRecordData claim)
        {
            if (claimsById.TryGetValue(PoliticalModelUtility.Normalize(claimId), out TerritorialClaimRecordData found))
            {
                claim = found.Clone();
                return true;
            }

            claim = null;
            return false;
        }

        public bool TryGetJurisdiction(string jurisdictionId, out JurisdictionRecordData jurisdiction)
        {
            if (jurisdictionsById.TryGetValue(PoliticalModelUtility.Normalize(jurisdictionId), out JurisdictionRecordData found))
            {
                jurisdiction = found.Clone();
                return true;
            }

            jurisdiction = null;
            return false;
        }

        public bool TryGetOrganizationCharter(string charterId, out GovernmentOrganizationCharterRecordData charter)
        {
            if (organizationChartersById.TryGetValue(PoliticalModelUtility.Normalize(charterId), out GovernmentOrganizationCharterRecordData found))
            {
                charter = found.Clone();
                return true;
            }

            charter = null;
            return false;
        }

        public PoliticalOperationResult RenamePolity(PolityRenameRequest request)
        {
            request ??= new PolityRenameRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string polityId = PoliticalModelUtility.Normalize(request.polityId);
            if (TryDuplicate(request.transactionId, polityId, "rename-polity", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!politiesById.TryGetValue(polityId, out PolityRecordData current)) return Fail(PoliticalOperationCode.MissingPolity, $"Polity '{polityId}' is missing.", before);
            string name = PoliticalModelUtility.Normalize(request.name);
            if (string.IsNullOrEmpty(name)) return Fail(PoliticalOperationCode.InvalidRequest, "Polity name is required.", before);
            string nameRecordId = PoliticalModelUtility.Normalize(request.nameRecordId);
            if (string.IsNullOrEmpty(nameRecordId)) nameRecordId = $"{polityId}.name.{request.category.ToString().ToLowerInvariant()}.{namesById.Values.Count(item => string.Equals(item.ownerId, polityId, StringComparison.Ordinal)) + 1:000}";
            if (namesById.ContainsKey(nameRecordId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Political name record '{nameRecordId}' already exists.", before);

            PolityRecordData changed = current.Clone();
            if (request.makeOfficial) changed.officialName = name;
            changed.revision++;
            PoliticalNameRecordData record = new PoliticalNameRecordData
            {
                nameRecordId = nameRecordId,
                ownerId = polityId,
                category = request.category == PoliticalNameCategory.Unknown ? PoliticalNameCategory.Common : request.category,
                value = name,
                effectiveStartWorldTime = request.worldTime,
                visibility = request.visibility,
                sourceId = PoliticalModelUtility.Normalize(request.sourceId),
                recognitionContextId = PoliticalModelUtility.Normalize(request.recognitionContextId),
                provenanceId = PoliticalModelUtility.Normalize(request.provenanceId),
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Polity rename previewed.", before, before, preview: true, subjectId: polityId, polity: changed);
            politiesById[polityId] = changed;
            namesById[nameRecordId] = record;
            CompleteTransaction(request.transactionId, "rename-polity", polityId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Polity name recorded without replacing stable identity.", before, Revision, subjectId: polityId, polity: changed));
        }

        public PoliticalOperationResult TransitionPolity(PolityTransitionRequest request)
        {
            request ??= new PolityTransitionRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string polityId = PoliticalModelUtility.Normalize(request.polityId);
            if (TryDuplicate(request.transactionId, polityId, "transition-polity", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!politiesById.TryGetValue(polityId, out PolityRecordData current)) return Fail(PoliticalOperationCode.MissingPolity, $"Polity '{polityId}' is missing.", before);
            if (request.targetState == PolityLifecycleState.Unknown || request.targetState == PolityLifecycleState.Invalid) return Fail(PoliticalOperationCode.InvalidState, "Polity target state is invalid.", before);
            string[] successors = PoliticalModelUtility.Clean(request.successorPolityIds);
            foreach (string successorId in successors) if (!politiesById.ContainsKey(successorId)) return Fail(PoliticalOperationCode.MissingPolity, $"Successor polity '{successorId}' is missing.", before);
            PolityRecordData changed = current.Clone();
            changed.lifecycleState = request.targetState;
            changed.successorPolityIds = PoliticalModelUtility.Clean(changed.successorPolityIds.Concat(successors));
            if (request.targetState == PolityLifecycleState.Dissolved || request.targetState == PolityLifecycleState.Merged || request.targetState == PolityLifecycleState.Split || request.targetState == PolityLifecycleState.Historical || request.targetState == PolityLifecycleState.Archived) changed.dissolvedWorldTime = request.worldTime;
            changed.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? changed.sourceEventId : request.sourceEventId.Trim();
            changed.revision++;
            if (request.preview) return PoliticalOperationResult.Success("Polity lifecycle transition previewed.", before, before, preview: true, subjectId: polityId, polity: changed);
            politiesById[polityId] = changed;
            CompleteTransaction(request.transactionId, "transition-polity", polityId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Polity lifecycle transitioned.", before, Revision, subjectId: polityId, polity: changed));
        }

        public PoliticalOperationResult SetGovernmentInstitutionRole(GovernmentInstitutionRoleRequest request)
        {
            request ??= new GovernmentInstitutionRoleRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string roleId = PoliticalModelUtility.Normalize(request.roleId);
            if (TryDuplicate(request.transactionId, roleId, "set-government-institution-role", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(roleId)) return Fail(PoliticalOperationCode.InvalidRequest, "Government institution role ID is required.", before);
            if (request.endRole)
            {
                if (!institutionRolesById.TryGetValue(roleId, out GovernmentInstitutionRoleRecordData currentRole)) return Fail(PoliticalOperationCode.InvalidReference, $"Government institution role '{roleId}' is missing.", before);
                if (currentRole.endedWorldTime >= 0d) return Fail(PoliticalOperationCode.InvalidState, $"Government institution role '{roleId}' already ended.", before);
                GovernmentInstitutionRoleRecordData ended = currentRole.Clone();
                ended.endedWorldTime = request.worldTime;
                ended.sourceDecisionId = string.IsNullOrWhiteSpace(request.sourceDecisionId) ? ended.sourceDecisionId : request.sourceDecisionId.Trim();
                ended.revision++;
                if (request.preview) return PoliticalOperationResult.Success("Government institution role ending previewed.", before, before, preview: true, subjectId: roleId);
                institutionRolesById[roleId] = ended;
                CompleteTransaction(request.transactionId, "set-government-institution-role", roleId);
                Revision++;
                return PublishCommit(PoliticalOperationResult.Success("Government institution role ended.", before, Revision, subjectId: roleId));
            }
            if (!ValidateGovernment(request.governmentId, out string governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!ValidateOrganization(request.organizationId, out string organizationFailure)) return Fail(PoliticalOperationCode.InvalidReference, organizationFailure, before);
            if (institutionRolesById.ContainsKey(roleId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Government institution role '{roleId}' already exists.", before);
            GovernmentInstitutionRoleRecordData role = new GovernmentInstitutionRoleRecordData
            {
                roleId = roleId,
                governmentId = PoliticalModelUtility.Normalize(request.governmentId),
                organizationId = PoliticalModelUtility.Normalize(request.organizationId),
                roleCategory = request.roleCategory == GovernmentInstitutionRoleCategory.Unknown ? GovernmentInstitutionRoleCategory.Custom : request.roleCategory,
                primary = request.primary,
                effectiveWorldTime = request.worldTime,
                sourceAuthorityGrantId = PoliticalModelUtility.Normalize(request.sourceAuthorityGrantId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                visibility = request.visibility,
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Government institution role previewed.", before, before, preview: true, subjectId: roleId);
            institutionRolesById[roleId] = role;
            GovernmentRecordData government = governmentsById[role.governmentId];
            government.governingOrganizationIds = PoliticalModelUtility.Clean(government.governingOrganizationIds.Concat(new[] { role.organizationId }));
            if (request.primary) government.primaryGoverningOrganizationId = role.organizationId;
            government.revision++;
            CompleteTransaction(request.transactionId, "set-government-institution-role", roleId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government institution role recorded.", before, Revision, subjectId: roleId, government: government));
        }

        public PoliticalOperationResult ChangeTerritoryPlaceMembership(TerritoryPlaceMembershipRequest request)
        {
            request ??= new TerritoryPlaceMembershipRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string membershipId = PoliticalModelUtility.Normalize(request.membershipId);
            if (TryDuplicate(request.transactionId, membershipId, "change-territory-place-membership", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(membershipId)) return Fail(PoliticalOperationCode.InvalidRequest, "Territory-place membership ID is required.", before);
            if (!ValidateTerritory(request.territoryId, out string territoryFailure)) return Fail(PoliticalOperationCode.MissingTerritory, territoryFailure, before);
            if (!ValidatePlace(request.placeId, out string placeFailure)) return Fail(PoliticalOperationCode.InvalidReference, placeFailure, before);
            TerritoryPlaceMembershipRecordData membership;
            if (request.endMembership)
            {
                if (!territoryPlaceMembershipsById.TryGetValue(membershipId, out TerritoryPlaceMembershipRecordData existing)) return Fail(PoliticalOperationCode.InvalidReference, $"Territory-place membership '{membershipId}' is missing.", before);
                if (existing.endedWorldTime >= 0d) return Fail(PoliticalOperationCode.InvalidState, $"Territory-place membership '{membershipId}' already ended.", before);
                if (!string.Equals(existing.territoryId, PoliticalModelUtility.Normalize(request.territoryId), StringComparison.Ordinal) || !string.Equals(existing.placeId, PoliticalModelUtility.Normalize(request.placeId), StringComparison.Ordinal)) return Fail(PoliticalOperationCode.InvalidReference, $"Territory-place membership '{membershipId}' does not match the requested territory and place.", before);
                membership = existing.Clone();
                membership.endedWorldTime = request.worldTime;
                membership.revision++;
            }
            else
            {
                if (territoryPlaceMembershipsById.ContainsKey(membershipId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Territory-place membership '{membershipId}' already exists.", before);
                membership = new TerritoryPlaceMembershipRecordData { membershipId = membershipId, territoryId = PoliticalModelUtility.Normalize(request.territoryId), placeId = PoliticalModelUtility.Normalize(request.placeId), membershipKind = request.membershipKind == TerritoryMembershipKind.Unknown ? TerritoryMembershipKind.ContainsPlace : request.membershipKind, effectiveWorldTime = request.worldTime, sourceId = PoliticalModelUtility.Normalize(request.sourceId), revision = 1L };
            }
            PoliticalTerritoryRecordData territory = territoriesById[PoliticalModelUtility.Normalize(request.territoryId)].Clone();
            bool placeRemainsActive = request.endMembership && territoryPlaceMembershipsById.Values.Any(item => !string.Equals(item.membershipId, membershipId, StringComparison.Ordinal) && string.Equals(item.territoryId, territory.territoryId, StringComparison.Ordinal) && string.Equals(item.placeId, membership.placeId, StringComparison.Ordinal) && item.endedWorldTime < 0d);
            territory.placeIds = request.endMembership && !placeRemainsActive ? PoliticalModelUtility.Clean(territory.placeIds.Where(item => !string.Equals(item, membership.placeId, StringComparison.Ordinal))) : PoliticalModelUtility.Clean(territory.placeIds.Concat(new[] { membership.placeId }));
            territory.revision++;
            if (request.preview) return PoliticalOperationResult.Success("Territory-place membership change previewed.", before, before, preview: true, subjectId: membershipId, territory: territory);
            territoryPlaceMembershipsById[membershipId] = membership;
            territoriesById[territory.territoryId] = territory;
            CompleteTransaction(request.transactionId, "change-territory-place-membership", membershipId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Territory-place membership changed.", before, Revision, subjectId: membershipId, territory: territory));
        }

        public PoliticalOperationResult TransitionTerritorialClaim(TerritorialClaimTransitionRequest request)
        {
            request ??= new TerritorialClaimTransitionRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string claimId = PoliticalModelUtility.Normalize(request.claimId);
            if (TryDuplicate(request.transactionId, claimId, "transition-territorial-claim", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!claimsById.TryGetValue(claimId, out TerritorialClaimRecordData current)) return Fail(PoliticalOperationCode.MissingClaim, $"Territorial claim '{claimId}' is missing.", before);
            if (request.targetState == TerritorialClaimLifecycleState.Unknown) return Fail(PoliticalOperationCode.InvalidState, "Territorial claim target state is invalid.", before);
            foreach (string governmentId in PoliticalModelUtility.Clean(request.disputedByGovernmentIds)) if (!governmentsById.ContainsKey(governmentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Disputing government '{governmentId}' is missing.", before);
            TerritorialClaimRecordData changed = current.Clone();
            changed.lifecycleState = request.targetState;
            changed.disputedByGovernmentIds = PoliticalModelUtility.Clean(changed.disputedByGovernmentIds.Concat(request.disputedByGovernmentIds ?? Array.Empty<string>()));
            changed.recognitionRelationId = string.IsNullOrWhiteSpace(request.recognitionRelationId) ? changed.recognitionRelationId : request.recognitionRelationId.Trim();
            changed.sourceDecisionId = string.IsNullOrWhiteSpace(request.sourceDecisionId) ? changed.sourceDecisionId : request.sourceDecisionId.Trim();
            if (request.targetState == TerritorialClaimLifecycleState.Transferred || request.targetState == TerritorialClaimLifecycleState.Abandoned || request.targetState == TerritorialClaimLifecycleState.Superseded || request.targetState == TerritorialClaimLifecycleState.Historical) changed.endedWorldTime = request.worldTime;
            changed.revision++;
            if (request.preview) return PoliticalOperationResult.Success("Territorial claim transition previewed.", before, before, preview: true, subjectId: claimId, claim: changed);
            claimsById[claimId] = changed;
            CompleteTransaction(request.transactionId, "transition-territorial-claim", claimId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Territorial claim transitioned.", before, Revision, subjectId: claimId, claim: changed));
        }

        public PoliticalOperationResult TransitionTerritory(TerritoryTransitionRequest request)
        {
            request ??= new TerritoryTransitionRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string territoryId = PoliticalModelUtility.Normalize(request.territoryId);
            if (TryDuplicate(request.transactionId, territoryId, "transition-territory", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!territoriesById.TryGetValue(territoryId, out PoliticalTerritoryRecordData current)) return Fail(PoliticalOperationCode.MissingTerritory, $"Territory '{territoryId}' is missing.", before);
            if (request.targetState == TerritoryLifecycleState.Unknown) return Fail(PoliticalOperationCode.InvalidState, "Territory target state is invalid.", before);
            PoliticalTerritoryRecordData changed = current.Clone();
            changed.lifecycleState = request.targetState;
            changed.sourceEventId = string.IsNullOrWhiteSpace(request.sourceEventId) ? changed.sourceEventId : request.sourceEventId.Trim();
            if (request.targetState == TerritoryLifecycleState.Transferred || request.targetState == TerritoryLifecycleState.Dissolved || request.targetState == TerritoryLifecycleState.Historical || request.targetState == TerritoryLifecycleState.Archived) changed.endedWorldTime = request.worldTime;
            changed.revision++;
            if (request.preview) return PoliticalOperationResult.Success("Territory lifecycle transition previewed.", before, before, preview: true, subjectId: territoryId, territory: changed);
            territoriesById[territoryId] = changed;
            CompleteTransaction(request.transactionId, "transition-territory", territoryId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Territory lifecycle transitioned.", before, Revision, subjectId: territoryId, territory: changed));
        }

        public PoliticalOperationResult TransitionJurisdiction(JurisdictionTransitionRequest request)
        {
            request ??= new JurisdictionTransitionRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string jurisdictionId = PoliticalModelUtility.Normalize(request.jurisdictionId);
            if (TryDuplicate(request.transactionId, jurisdictionId, "transition-jurisdiction", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!jurisdictionsById.TryGetValue(jurisdictionId, out JurisdictionRecordData current)) return Fail(PoliticalOperationCode.MissingJurisdiction, $"Jurisdiction '{jurisdictionId}' is missing.", before);
            if (request.targetState == JurisdictionLifecycleState.Unknown) return Fail(PoliticalOperationCode.InvalidState, "Jurisdiction target state is invalid.", before);
            JurisdictionRecordData changed = current.Clone();
            changed.lifecycleState = request.targetState;
            changed.sourceDecisionId = string.IsNullOrWhiteSpace(request.sourceDecisionId) ? changed.sourceDecisionId : request.sourceDecisionId.Trim();
            if (request.targetState == JurisdictionLifecycleState.Ended || request.targetState == JurisdictionLifecycleState.Superseded || request.targetState == JurisdictionLifecycleState.Historical) changed.expirationWorldTime = request.worldTime;
            changed.revision++;
            if (request.preview) return PoliticalOperationResult.Success("Jurisdiction transition previewed.", before, before, preview: true, subjectId: jurisdictionId, jurisdiction: changed);
            jurisdictionsById[jurisdictionId] = changed;
            CompleteTransaction(request.transactionId, "transition-jurisdiction", jurisdictionId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Jurisdiction transitioned.", before, Revision, subjectId: jurisdictionId, jurisdiction: changed));
        }

        public PoliticalOperationResult RegisterOrganizationCharter(GovernmentOrganizationCharterRequest request)
        {
            request ??= new GovernmentOrganizationCharterRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string charterId = PoliticalModelUtility.Normalize(request.charterId);
            if (TryDuplicate(request.transactionId, charterId, "register-organization-charter", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(charterId)) return Fail(PoliticalOperationCode.InvalidRequest, "Government organization charter ID is required.", before);
            if (organizationChartersById.ContainsKey(charterId)) return Fail(PoliticalOperationCode.Conflict, $"Government organization charter '{charterId}' already exists.", before);
            if (!TryGetDefinition(request.charterDefinitionId, out GovernmentOrganizationCharterDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Government organization charter definition '{request.charterDefinitionId}' is missing.", before);

            string governmentId = string.IsNullOrWhiteSpace(request.governmentId) ? definition.GovernmentId : PoliticalModelUtility.Normalize(request.governmentId);
            string organizationId = string.IsNullOrWhiteSpace(request.organizationId) ? definition.OrganizationId : PoliticalModelUtility.Normalize(request.organizationId);
            string supervisorId = string.IsNullOrWhiteSpace(request.supervisingOrganizationId) ? definition.SupervisingOrganizationId : PoliticalModelUtility.Normalize(request.supervisingOrganizationId);
            string instrumentId = string.IsNullOrWhiteSpace(request.legalInstrumentId) ? definition.LegalInstrumentId : PoliticalModelUtility.Normalize(request.legalInstrumentId);
            string[] jurisdictionIds = PoliticalModelUtility.Clean(request.jurisdictionIds == null || request.jurisdictionIds.Length == 0 ? definition.JurisdictionIds : request.jurisdictionIds);

            if (!governmentsById.ContainsKey(governmentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (organizations == null || !organizations.TryGetSnapshot(organizationId, out _)) return Fail(PoliticalOperationCode.InvalidReference, $"Chartered organization '{organizationId}' is missing.", before);
            if (!string.IsNullOrEmpty(supervisorId) && !organizations.TryGetSnapshot(supervisorId, out _)) return Fail(PoliticalOperationCode.InvalidReference, $"Supervising organization '{supervisorId}' is missing.", before);
            if (string.IsNullOrEmpty(instrumentId)) return Fail(PoliticalOperationCode.InvalidReference, "A charter legal instrument ID is required.", before);
            foreach (string jurisdictionId in jurisdictionIds)
            {
                if (!jurisdictionsById.TryGetValue(jurisdictionId, out JurisdictionRecordData jurisdiction)) return Fail(PoliticalOperationCode.MissingJurisdiction, $"Charter jurisdiction '{jurisdictionId}' is missing.", before);
                if (!string.Equals(jurisdiction.governmentId, governmentId, StringComparison.Ordinal)) return Fail(PoliticalOperationCode.InvalidReference, $"Charter jurisdiction '{jurisdictionId}' does not belong to government '{governmentId}'.", before);
            }

            GovernmentOrganizationCharterRecordData record = new GovernmentOrganizationCharterRecordData
            {
                charterId = charterId,
                charterDefinitionId = definition.Id,
                governmentId = governmentId,
                organizationId = organizationId,
                supervisingOrganizationId = supervisorId,
                legalInstrumentId = instrumentId,
                jurisdictionIds = jurisdictionIds,
                grantedPowerIds = PoliticalModelUtility.Clean(definition.GrantedPowerIds),
                dutyIds = PoliticalModelUtility.Clean(definition.DutyIds),
                category = definition.Category,
                lifecycleState = GovernmentOrganizationCharterLifecycleState.Active,
                revocable = definition.Revocable,
                effectiveWorldTime = request.worldTime,
                sourceAuthorityGrantId = PoliticalModelUtility.Normalize(request.sourceAuthorityGrantId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                visibility = request.visibilityOverride ?? definition.DefaultVisibility,
                provenanceId = PoliticalModelUtility.Normalize(request.provenanceId),
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Government organization charter previewed.", before, before, preview: true, subjectId: charterId, organizationCharter: record);
            organizationChartersById[charterId] = record;
            CompleteTransaction(request.transactionId, "register-organization-charter", charterId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government organization charter registered.", before, Revision, subjectId: charterId, organizationCharter: record));
        }

        public PoliticalOperationResult TransitionOrganizationCharter(GovernmentOrganizationCharterTransitionRequest request)
        {
            request ??= new GovernmentOrganizationCharterTransitionRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string charterId = PoliticalModelUtility.Normalize(request.charterId);
            if (TryDuplicate(request.transactionId, charterId, "transition-organization-charter", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!organizationChartersById.TryGetValue(charterId, out GovernmentOrganizationCharterRecordData current)) return Fail(PoliticalOperationCode.InvalidReference, $"Government organization charter '{charterId}' is missing.", before);
            if (request.targetState == GovernmentOrganizationCharterLifecycleState.Unknown || request.targetState == GovernmentOrganizationCharterLifecycleState.Active && current.lifecycleState == GovernmentOrganizationCharterLifecycleState.Revoked) return Fail(PoliticalOperationCode.InvalidState, "Government organization charter transition is invalid.", before);
            if (request.targetState == GovernmentOrganizationCharterLifecycleState.Revoked && !current.revocable) return Fail(PoliticalOperationCode.InvalidState, $"Government organization charter '{charterId}' is irrevocable.", before);
            GovernmentOrganizationCharterRecordData changed = current.Clone();
            changed.lifecycleState = request.targetState;
            changed.sourceAuthorityGrantId = string.IsNullOrWhiteSpace(request.sourceAuthorityGrantId) ? changed.sourceAuthorityGrantId : request.sourceAuthorityGrantId.Trim();
            changed.sourceDecisionId = string.IsNullOrWhiteSpace(request.sourceDecisionId) ? changed.sourceDecisionId : request.sourceDecisionId.Trim();
            if (request.targetState == GovernmentOrganizationCharterLifecycleState.Revoked || request.targetState == GovernmentOrganizationCharterLifecycleState.Expired || request.targetState == GovernmentOrganizationCharterLifecycleState.Superseded || request.targetState == GovernmentOrganizationCharterLifecycleState.Historical) changed.endedWorldTime = request.worldTime;
            changed.revision++;
            if (request.preview) return PoliticalOperationResult.Success("Government organization charter transition previewed.", before, before, preview: true, subjectId: charterId, organizationCharter: changed);
            organizationChartersById[charterId] = changed;
            CompleteTransaction(request.transactionId, "transition-organization-charter", charterId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government organization charter transitioned.", before, Revision, subjectId: charterId, organizationCharter: changed));
        }

        public PoliticalOperationResult SynchronizeOrganizationCharter(GovernmentOrganizationCharterSynchronizationRequest request)
        {
            request ??= new GovernmentOrganizationCharterSynchronizationRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string charterId = PoliticalModelUtility.Normalize(request.charterId);
            if (TryDuplicate(request.transactionId, charterId, "synchronize-organization-charter", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!organizationChartersById.TryGetValue(charterId, out GovernmentOrganizationCharterRecordData current)) return Fail(PoliticalOperationCode.InvalidReference, $"Government organization charter '{charterId}' is missing.", before);
            string definitionId = string.IsNullOrWhiteSpace(request.charterDefinitionId) ? current.charterDefinitionId : request.charterDefinitionId;
            if (!TryGetDefinition(definitionId, out GovernmentOrganizationCharterDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Government organization charter definition '{definitionId}' is missing.", before);
            if (!governmentsById.ContainsKey(definition.GovernmentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{definition.GovernmentId}' is missing.", before);
            if (organizations == null || !organizations.TryGetSnapshot(definition.OrganizationId, out _)) return Fail(PoliticalOperationCode.InvalidReference, $"Chartered organization '{definition.OrganizationId}' is missing.", before);
            if (!string.IsNullOrEmpty(definition.SupervisingOrganizationId) && !organizations.TryGetSnapshot(definition.SupervisingOrganizationId, out _)) return Fail(PoliticalOperationCode.InvalidReference, $"Supervising organization '{definition.SupervisingOrganizationId}' is missing.", before);
            foreach (string jurisdictionId in definition.JurisdictionIds)
            {
                if (!jurisdictionsById.TryGetValue(jurisdictionId, out JurisdictionRecordData jurisdiction)) return Fail(PoliticalOperationCode.MissingJurisdiction, $"Charter jurisdiction '{jurisdictionId}' is missing.", before);
                if (!string.Equals(jurisdiction.governmentId, definition.GovernmentId, StringComparison.Ordinal)) return Fail(PoliticalOperationCode.InvalidReference, $"Charter jurisdiction '{jurisdictionId}' does not belong to government '{definition.GovernmentId}'.", before);
            }

            GovernmentOrganizationCharterRecordData changed = current.Clone();
            changed.charterDefinitionId = definition.Id;
            changed.governmentId = definition.GovernmentId;
            changed.organizationId = definition.OrganizationId;
            changed.supervisingOrganizationId = definition.SupervisingOrganizationId;
            changed.legalInstrumentId = definition.LegalInstrumentId;
            changed.jurisdictionIds = PoliticalModelUtility.Clean(definition.JurisdictionIds);
            changed.grantedPowerIds = PoliticalModelUtility.Clean(definition.GrantedPowerIds);
            changed.dutyIds = PoliticalModelUtility.Clean(definition.DutyIds);
            changed.category = definition.Category;
            changed.revocable = definition.Revocable;
            changed.visibility = definition.DefaultVisibility;
            changed.provenanceId = string.IsNullOrWhiteSpace(request.provenanceId) ? definition.Id : PoliticalModelUtility.Normalize(request.provenanceId);
            changed.revision++;
            if (request.preview) return PoliticalOperationResult.Success("Government organization charter synchronization previewed.", before, before, preview: true, subjectId: charterId, organizationCharter: changed);
            organizationChartersById[charterId] = changed;
            CompleteTransaction(request.transactionId, "synchronize-organization-charter", charterId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government organization charter synchronized with its authored definition.", before, Revision, subjectId: charterId, organizationCharter: changed));
        }

        public PoliticalOperationResult CreateTransitionPlan(PoliticalTransitionPlanRequest request)
        {
            request ??= new PoliticalTransitionPlanRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string transitionId = PoliticalModelUtility.Normalize(request.transitionId);
            if (TryDuplicate(request.transactionId, transitionId, "create-transition-plan", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrEmpty(transitionId) || request.transitionKind == PoliticalTransitionKind.Unknown) return Fail(PoliticalOperationCode.InvalidRequest, "Transition ID and kind are required.", before);
            if (transitionsById.ContainsKey(transitionId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Transition '{transitionId}' already exists.", before);
            if (!ValidateOptionalPoliticalReferences(request.sourcePolityId, request.targetPolityId, request.sourceGovernmentId, request.targetGovernmentId, request.territoryIds, before, out PoliticalOperationResult referenceFailure)) return referenceFailure;
            if (!ValidateDiplomacyReferences(request.sourceAgreementId, "", "", before, out PoliticalOperationResult diplomacyFailure)) return diplomacyFailure;
            PoliticalTransitionPlanRecordData plan = new PoliticalTransitionPlanRecordData { transitionId = transitionId, transitionKind = request.transitionKind, sourcePolityId = PoliticalModelUtility.Normalize(request.sourcePolityId), targetPolityId = PoliticalModelUtility.Normalize(request.targetPolityId), sourceGovernmentId = PoliticalModelUtility.Normalize(request.sourceGovernmentId), targetGovernmentId = PoliticalModelUtility.Normalize(request.targetGovernmentId), territoryIds = PoliticalModelUtility.Clean(request.territoryIds), sourceAgreementId = PoliticalModelUtility.Normalize(request.sourceAgreementId), sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId), plannedWorldTime = request.worldTime, diagnostics = PoliticalModelUtility.Normalize(request.diagnostics), revision = 1L };
            if (request.preview) return PoliticalOperationResult.Success("Political transition plan previewed.", before, before, preview: true, subjectId: transitionId, transition: plan);
            transitionsById[transitionId] = plan;
            CompleteTransaction(request.transactionId, "create-transition-plan", transitionId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Political transition plan recorded.", before, Revision, subjectId: transitionId, transition: plan));
        }

        public PoliticalOperationResult ProcessWorldTime(PoliticalTimeEvaluationRequest request)
        {
            request ??= new PoliticalTimeEvaluationRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string boundaryId = PoliticalModelUtility.Normalize(request.boundaryId);
            if (string.IsNullOrEmpty(boundaryId)) return Fail(PoliticalOperationCode.InvalidRequest, "Political time evaluation boundary ID is required.", before);
            if (TryDuplicate(request.transactionId, boundaryId, "process-political-world-time", before, out PoliticalOperationResult duplicate)) return duplicate;
            JurisdictionRecordData[] expiring = jurisdictionsById.Values
                .Where(item => IsJurisdictionActive(item, request.worldTime) == false
                    && (item.lifecycleState == JurisdictionLifecycleState.Active || item.lifecycleState == JurisdictionLifecycleState.Delegated || item.lifecycleState == JurisdictionLifecycleState.Contested)
                    && item.expirationWorldTime >= 0d
                    && request.worldTime > item.expirationWorldTime)
                .OrderBy(item => item.expirationWorldTime)
                .ThenBy(item => item.jurisdictionId, StringComparer.Ordinal)
                .ToArray();
            string administrationSummary = ProcessAdministrationWorldTime(request.worldTime, request.preview);
            string fiscalSummary = ProcessFiscalHierarchyWorldTime(request.worldTime, request.preview);
            if (request.preview) return PoliticalOperationResult.Success($"Political time evaluation previewed; {expiring.Length} jurisdiction(s) expire, {administrationSummary}, and {fiscalSummary}.", before, before, preview: true, subjectId: boundaryId);
            foreach (JurisdictionRecordData jurisdiction in expiring)
            {
                jurisdiction.lifecycleState = JurisdictionLifecycleState.Ended;
                jurisdiction.revision++;
            }
            CompleteTransaction(request.transactionId, "process-political-world-time", boundaryId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success($"Political time boundary processed deterministically; {expiring.Length} jurisdiction(s) expired, {administrationSummary}, and {fiscalSummary}.", before, Revision, subjectId: boundaryId));
        }

        public PoliticalOperationResult CreatePolity(PolityCreateRequest request)
        {
            request ??= new PolityCreateRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string polityId = PoliticalModelUtility.Normalize(request.polityId);
            if (TryDuplicate(request.transactionId, polityId, "create-polity", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(polityId)) return Fail(PoliticalOperationCode.InvalidRequest, "Polity ID is required.", before);
            if (politiesById.ContainsKey(polityId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Polity '{polityId}' already exists with different transaction identity.", before);
            if (!TryGetDefinition(request.polityDefinitionId, out PolityDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Polity definition '{request.polityDefinitionId}' is missing.", before);
            string name = PoliticalModelUtility.Normalize(request.officialName);
            if (string.IsNullOrWhiteSpace(name)) return Fail(PoliticalOperationCode.InvalidRequest, "Polity official name is required.", before);

            PolityRecordData record = new PolityRecordData
            {
                polityId = polityId,
                polityDefinitionId = definition.Id,
                officialName = name,
                lifecycleState = request.lifecycleState == PolityLifecycleState.Unknown ? PolityLifecycleState.Active : request.lifecycleState,
                foundingWorldTime = request.worldTime,
                visibility = request.visibility,
                capitalPlaceIds = ValidatePlacesOrFail(request.capitalPlaceIds, before, out PoliticalOperationResult placeFailure) ? PoliticalModelUtility.Clean(request.capitalPlaceIds) : Array.Empty<string>(),
                predecessorPolityIds = PoliticalModelUtility.Clean(request.predecessorPolityIds),
                tags = PoliticalModelUtility.Clean(request.tags).Concat(new[] { "government", "polity" }).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                sourceEventId = PoliticalModelUtility.Normalize(request.sourceEventId),
                sourceRecordId = PoliticalModelUtility.Normalize(request.sourceRecordId),
                provenanceId = PoliticalModelUtility.Normalize(request.provenanceId),
                revision = 1L
            };
            if (placeFailure != null) return placeFailure;

            PoliticalNameRecordData nameRecord = new PoliticalNameRecordData
            {
                nameRecordId = $"{polityId}.name.official.001",
                ownerId = polityId,
                category = PoliticalNameCategory.Official,
                value = name,
                effectiveStartWorldTime = request.worldTime,
                visibility = request.visibility,
                sourceId = record.sourceRecordId,
                provenanceId = record.provenanceId,
                revision = 1L
            };

            if (request.preview) return PoliticalOperationResult.Success("Polity previewed.", before, before, preview: true, subjectId: polityId, polity: record);
            politiesById[polityId] = record;
            namesById[nameRecord.nameRecordId] = nameRecord;
            CompleteTransaction(request.transactionId, "create-polity", polityId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Polity recorded.", before, Revision, subjectId: polityId, polity: record));
        }

        public PoliticalOperationResult RegisterGovernment(GovernmentRegisterRequest request)
        {
            request ??= new GovernmentRegisterRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            if (TryDuplicate(request.transactionId, governmentId, "register-government", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(governmentId)) return Fail(PoliticalOperationCode.InvalidRequest, "Government ID is required.", before);
            if (governmentsById.ContainsKey(governmentId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Government '{governmentId}' already exists with different transaction identity.", before);
            if (!TryGetDefinition(request.governmentDefinitionId, out GovernmentDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Government definition '{request.governmentDefinitionId}' is missing.", before);
            if (!ValidatePolity(request.polityId, out string polityFailure)) return Fail(PoliticalOperationCode.MissingPolity, polityFailure, before);
            string[] governingOrganizations = PoliticalModelUtility.Clean(request.governingOrganizationIds);
            if (!string.IsNullOrWhiteSpace(request.primaryGoverningOrganizationId))
            {
                governingOrganizations = governingOrganizations.Concat(new[] { PoliticalModelUtility.Normalize(request.primaryGoverningOrganizationId) }).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
            }
            if (governingOrganizations.Length == 0) return Fail(PoliticalOperationCode.InvalidReference, "Government requires at least one governing organization reference.", before);
            if (!definition.AllowsSeveralGoverningOrganizations && governingOrganizations.Length > 1) return Fail(PoliticalOperationCode.InvalidRequest, $"Government definition '{definition.Id}' permits only one governing organization.", before);
            foreach (string organizationId in governingOrganizations)
            {
                if (!ValidateOrganization(organizationId, out string organizationFailure)) return Fail(PoliticalOperationCode.InvalidReference, organizationFailure, before);
            }
            string parentId = PoliticalModelUtility.Normalize(request.parentGovernmentId);
            if (!string.IsNullOrEmpty(parentId))
            {
                if (!governmentsById.ContainsKey(parentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Parent government '{parentId}' is missing.", before);
                if (WouldCreateGovernmentCycle(governmentId, parentId)) return Fail(PoliticalOperationCode.CycleRejected, "Government hierarchy cycle rejected.", before);
            }

            GovernmentRecordData record = new GovernmentRecordData
            {
                governmentId = governmentId,
                governmentDefinitionId = definition.Id,
                polityId = PoliticalModelUtility.Normalize(request.polityId),
                officialName = string.IsNullOrWhiteSpace(request.officialName) ? definition.DisplayName : request.officialName.Trim(),
                primaryGoverningOrganizationId = string.IsNullOrWhiteSpace(request.primaryGoverningOrganizationId) ? governingOrganizations[0] : request.primaryGoverningOrganizationId.Trim(),
                governingOrganizationIds = governingOrganizations,
                parentGovernmentId = parentId,
                level = request.level == GovernmentLevel.Unknown ? definition.DefaultLevel : request.level,
                lifecycleState = request.lifecycleState == GovernmentLifecycleState.Unknown ? GovernmentLifecycleState.Active : request.lifecycleState,
                establishedWorldTime = request.worldTime,
                sourceAuthorityGrantId = PoliticalModelUtility.Normalize(request.sourceAuthorityGrantId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                sourceDiplomaticRecognitionId = PoliticalModelUtility.Normalize(request.sourceDiplomaticRecognitionId),
                sourceEventId = PoliticalModelUtility.Normalize(request.sourceEventId),
                sourceRecordId = PoliticalModelUtility.Normalize(request.sourceRecordId),
                visibility = request.visibility,
                tags = PoliticalModelUtility.Clean(request.tags).Concat(new[] { "government" }).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                revision = 1L
            };

            GovernmentInstitutionRoleRecordData[] roles = governingOrganizations.Select((organizationId, index) => new GovernmentInstitutionRoleRecordData
            {
                roleId = $"{governmentId}.institution.{index + 1:000}",
                governmentId = governmentId,
                organizationId = organizationId,
                roleCategory = index == 0 ? GovernmentInstitutionRoleCategory.Executive : GovernmentInstitutionRoleCategory.Custom,
                primary = string.Equals(organizationId, record.primaryGoverningOrganizationId, StringComparison.Ordinal),
                effectiveWorldTime = request.worldTime,
                sourceAuthorityGrantId = record.sourceAuthorityGrantId,
                sourceDecisionId = record.sourceDecisionId,
                visibility = request.visibility,
                revision = 1L
            }).ToArray();

            if (request.preview) return PoliticalOperationResult.Success("Government previewed.", before, before, preview: true, subjectId: governmentId, government: record);
            governmentsById[governmentId] = record;
            foreach (GovernmentInstitutionRoleRecordData role in roles) institutionRolesById[role.roleId] = role;
            if (!string.IsNullOrEmpty(parentId) && governmentsById.TryGetValue(parentId, out GovernmentRecordData parent))
            {
                parent.subordinateGovernmentIds = PoliticalModelUtility.Clean(parent.subordinateGovernmentIds.Concat(new[] { governmentId }));
                parent.revision++;
            }
            if (politiesById.TryGetValue(record.polityId, out PolityRecordData polity))
            {
                polity.claimantGovernmentIds = PoliticalModelUtility.Clean(polity.claimantGovernmentIds.Concat(new[] { governmentId }));
                if (string.IsNullOrEmpty(polity.currentGovernmentId) || request.markAsCurrentGovernment) polity.currentGovernmentId = governmentId;
                if (string.IsNullOrEmpty(polity.recognizedPrimaryGovernmentId) || request.markAsRecognizedPrimary) polity.recognizedPrimaryGovernmentId = governmentId;
                polity.revision++;
            }
            CompleteTransaction(request.transactionId, "register-government", governmentId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government registered.", before, Revision, subjectId: governmentId, government: record));
        }

        public PoliticalOperationResult CreateTerritory(TerritoryCreateRequest request)
        {
            request ??= new TerritoryCreateRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string territoryId = PoliticalModelUtility.Normalize(request.territoryId);
            if (TryDuplicate(request.transactionId, territoryId, "create-territory", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(territoryId)) return Fail(PoliticalOperationCode.InvalidRequest, "Territory ID is required.", before);
            if (territoriesById.ContainsKey(territoryId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Territory '{territoryId}' already exists with different transaction identity.", before);
            if (!TryGetDefinition(request.territoryDefinitionId, out PoliticalTerritoryDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Political Territory definition '{request.territoryDefinitionId}' is missing.", before);
            string[] placeIds = PoliticalModelUtility.Clean(request.placeIds);
            if (definition.RequiresAtLeastOnePlace && placeIds.Length == 0) return Fail(PoliticalOperationCode.InvalidReference, $"Territory definition '{definition.Id}' requires at least one place.", before);
            if (!ValidatePlacesOrFail(placeIds, before, out PoliticalOperationResult placeFailure)) return placeFailure;
            if (!string.IsNullOrWhiteSpace(request.polityId) && !ValidatePolity(request.polityId, out string polityFailure)) return Fail(PoliticalOperationCode.MissingPolity, polityFailure, before);
            if (!string.IsNullOrWhiteSpace(request.primaryGovernmentId) && !ValidateGovernment(request.primaryGovernmentId, out string governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            string parentId = PoliticalModelUtility.Normalize(request.parentTerritoryId);
            if (!string.IsNullOrEmpty(parentId))
            {
                if (!territoriesById.ContainsKey(parentId)) return Fail(PoliticalOperationCode.MissingTerritory, $"Parent territory '{parentId}' is missing.", before);
                if (WouldCreateTerritoryCycle(territoryId, parentId)) return Fail(PoliticalOperationCode.CycleRejected, "Territory hierarchy cycle rejected.", before);
            }

            PoliticalTerritoryRecordData record = new PoliticalTerritoryRecordData
            {
                territoryId = territoryId,
                territoryDefinitionId = definition.Id,
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? definition.DisplayName : request.displayName.Trim(),
                parentTerritoryId = parentId,
                polityId = PoliticalModelUtility.Normalize(request.polityId),
                primaryGovernmentId = PoliticalModelUtility.Normalize(request.primaryGovernmentId),
                lifecycleState = request.lifecycleState == TerritoryLifecycleState.Unknown ? TerritoryLifecycleState.Active : request.lifecycleState,
                placeIds = placeIds,
                createdWorldTime = request.worldTime,
                visibility = request.visibility,
                sourceEventId = PoliticalModelUtility.Normalize(request.sourceEventId),
                sourceRecordId = PoliticalModelUtility.Normalize(request.sourceRecordId),
                provenanceId = PoliticalModelUtility.Normalize(request.provenanceId),
                tags = PoliticalModelUtility.Clean(request.tags).Concat(new[] { "government", "territory" }).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                revision = 1L
            };

            TerritoryPlaceMembershipRecordData[] membershipsToAdd = placeIds.Select((placeId, index) => new TerritoryPlaceMembershipRecordData
            {
                membershipId = $"{territoryId}.place.{index + 1:000}",
                territoryId = territoryId,
                placeId = placeId,
                membershipKind = TerritoryMembershipKind.ContainsPlace,
                effectiveWorldTime = request.worldTime,
                sourceId = record.sourceRecordId,
                revision = 1L
            }).ToArray();

            if (request.preview) return PoliticalOperationResult.Success("Territory previewed.", before, before, preview: true, subjectId: territoryId, territory: record);
            territoriesById[territoryId] = record;
            foreach (TerritoryPlaceMembershipRecordData membership in membershipsToAdd) territoryPlaceMembershipsById[membership.membershipId] = membership;
            if (!string.IsNullOrEmpty(parentId) && territoriesById.TryGetValue(parentId, out PoliticalTerritoryRecordData parent))
            {
                parent.childTerritoryIds = PoliticalModelUtility.Clean(parent.childTerritoryIds.Concat(new[] { territoryId }));
                parent.revision++;
            }
            if (!string.IsNullOrEmpty(record.polityId) && politiesById.TryGetValue(record.polityId, out PolityRecordData polity))
            {
                polity.claimedTerritoryIds = PoliticalModelUtility.Clean(polity.claimedTerritoryIds.Concat(new[] { territoryId }));
                polity.revision++;
            }
            CompleteTransaction(request.transactionId, "create-territory", territoryId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Territory recorded.", before, Revision, subjectId: territoryId, territory: record));
        }

        public PoliticalOperationResult AssertTerritorialClaim(TerritorialClaimRequest request)
        {
            request ??= new TerritorialClaimRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string claimId = PoliticalModelUtility.Normalize(request.claimId);
            if (TryDuplicate(request.transactionId, claimId, "assert-claim", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(claimId)) return Fail(PoliticalOperationCode.InvalidRequest, "Territorial claim ID is required.", before);
            if (claimsById.ContainsKey(claimId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Territorial claim '{claimId}' already exists with different transaction identity.", before);
            if (!TryGetDefinition(request.claimDefinitionId, out TerritorialClaimDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Territorial Claim definition '{request.claimDefinitionId}' is missing.", before);
            if (!ValidateTerritory(request.territoryId, out string territoryFailure)) return Fail(PoliticalOperationCode.MissingTerritory, territoryFailure, before);
            if (definition.RequiresPolity && !ValidatePolity(request.claimantPolityId, out string polityFailure)) return Fail(PoliticalOperationCode.MissingPolity, polityFailure, before);
            if (definition.RequiresGovernment && !ValidateGovernment(request.claimantGovernmentId, out string governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!string.IsNullOrWhiteSpace(request.claimantGovernmentId) && !ValidateGovernment(request.claimantGovernmentId, out governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!ValidateDiplomacyReferences(request.basisAgreementId, request.basisDiplomaticRelationId, "", before, out PoliticalOperationResult diplomacyFailure)) return diplomacyFailure;

            TerritorialClaimRecordData record = new TerritorialClaimRecordData
            {
                claimId = claimId,
                claimDefinitionId = definition.Id,
                territoryId = PoliticalModelUtility.Normalize(request.territoryId),
                claimantPolityId = PoliticalModelUtility.Normalize(request.claimantPolityId),
                claimantGovernmentId = PoliticalModelUtility.Normalize(request.claimantGovernmentId),
                category = request.category == TerritorialClaimCategory.Unknown ? definition.Category : request.category,
                lifecycleState = request.lifecycleState == TerritorialClaimLifecycleState.Unknown ? TerritorialClaimLifecycleState.Asserted : request.lifecycleState,
                basisAgreementId = PoliticalModelUtility.Normalize(request.basisAgreementId),
                basisDiplomaticRelationId = PoliticalModelUtility.Normalize(request.basisDiplomaticRelationId),
                recognitionRelationId = PoliticalModelUtility.Normalize(request.recognitionRelationId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                assertedWorldTime = request.worldTime,
                visibility = request.visibility,
                disputedByGovernmentIds = PoliticalModelUtility.Clean(request.disputedByGovernmentIds),
                tags = PoliticalModelUtility.Clean(request.tags).Concat(new[] { "government", "claim" }).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                revision = 1L
            };

            if (request.preview) return PoliticalOperationResult.Success("Territorial claim previewed.", before, before, preview: true, subjectId: claimId, claim: record);
            claimsById[claimId] = record;
            CompleteTransaction(request.transactionId, "assert-claim", claimId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Territorial claim asserted.", before, Revision, subjectId: claimId, claim: record));
        }

        public PoliticalOperationResult RecordControl(TerritorialControlRequest request)
        {
            request ??= new TerritorialControlRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string controlId = PoliticalModelUtility.Normalize(request.controlId);
            if (TryDuplicate(request.transactionId, controlId, "record-control", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(controlId)) return Fail(PoliticalOperationCode.InvalidRequest, "Territorial control ID is required.", before);
            if (controlsById.ContainsKey(controlId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Territorial control '{controlId}' already exists with different transaction identity.", before);
            if (!ValidateTerritory(request.territoryId, out string territoryFailure)) return Fail(PoliticalOperationCode.MissingTerritory, territoryFailure, before);
            if (!ValidateGovernment(request.controllingGovernmentId, out string governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!ValidateDiplomacyReferences(request.sourceAgreementId, "", request.sourceWarId, before, out PoliticalOperationResult diplomacyFailure)) return diplomacyFailure;
            TerritorialControlRecordData record = new TerritorialControlRecordData
            {
                controlId = controlId,
                territoryId = PoliticalModelUtility.Normalize(request.territoryId),
                controllingGovernmentId = PoliticalModelUtility.Normalize(request.controllingGovernmentId),
                state = request.state == TerritorialControlState.Unknown ? TerritorialControlState.Controlled : request.state,
                sourceWarId = PoliticalModelUtility.Normalize(request.sourceWarId),
                sourceAgreementId = PoliticalModelUtility.Normalize(request.sourceAgreementId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                effectiveWorldTime = request.worldTime,
                visibility = request.visibility,
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Territorial control previewed.", before, before, preview: true, subjectId: controlId);
            controlsById[controlId] = record;
            CompleteTransaction(request.transactionId, "record-control", controlId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Territorial control recorded.", before, Revision, subjectId: controlId));
        }

        public PoliticalOperationResult RecordAdministration(TerritoryAdministrationRequest request)
        {
            request ??= new TerritoryAdministrationRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string administrationId = PoliticalModelUtility.Normalize(request.administrationId);
            if (TryDuplicate(request.transactionId, administrationId, "record-administration", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(administrationId)) return Fail(PoliticalOperationCode.InvalidRequest, "Territory administration ID is required.", before);
            if (administrationsById.ContainsKey(administrationId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Territory administration '{administrationId}' already exists with different transaction identity.", before);
            if (!ValidateTerritory(request.territoryId, out string territoryFailure)) return Fail(PoliticalOperationCode.MissingTerritory, territoryFailure, before);
            if (!ValidateGovernment(request.administeringGovernmentId, out string governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!string.IsNullOrWhiteSpace(request.delegatedByGovernmentId) && !ValidateGovernment(request.delegatedByGovernmentId, out governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!ValidateDiplomacyReferences(request.sourceAgreementId, "", "", before, out PoliticalOperationResult diplomacyFailure)) return diplomacyFailure;
            TerritoryAdministrationRecordData record = new TerritoryAdministrationRecordData
            {
                administrationId = administrationId,
                territoryId = PoliticalModelUtility.Normalize(request.territoryId),
                administeringGovernmentId = PoliticalModelUtility.Normalize(request.administeringGovernmentId),
                delegatedByGovernmentId = PoliticalModelUtility.Normalize(request.delegatedByGovernmentId),
                state = request.state == AdministrationState.Unknown ? AdministrationState.Administered : request.state,
                sourceAgreementId = PoliticalModelUtility.Normalize(request.sourceAgreementId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                effectiveWorldTime = request.worldTime,
                visibility = request.visibility,
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Territory administration previewed.", before, before, preview: true, subjectId: administrationId);
            administrationsById[administrationId] = record;
            CompleteTransaction(request.transactionId, "record-administration", administrationId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Territory administration recorded.", before, Revision, subjectId: administrationId));
        }

        public PoliticalOperationResult RegisterSeat(GovernmentSeatRequest request)
        {
            request ??= new GovernmentSeatRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string seatId = PoliticalModelUtility.Normalize(request.seatId);
            if (TryDuplicate(request.transactionId, seatId, "register-seat", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(seatId)) return Fail(PoliticalOperationCode.InvalidRequest, "Government seat ID is required.", before);
            if (seatsById.ContainsKey(seatId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Government seat '{seatId}' already exists with different transaction identity.", before);
            if (!ValidateGovernment(request.governmentId, out string governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!ValidatePlace(request.placeId, out string placeFailure)) return Fail(PoliticalOperationCode.InvalidReference, placeFailure, before);
            GovernmentSeatRecordData record = new GovernmentSeatRecordData
            {
                seatId = seatId,
                governmentId = PoliticalModelUtility.Normalize(request.governmentId),
                placeId = PoliticalModelUtility.Normalize(request.placeId),
                category = request.category == SeatCategory.Unknown ? SeatCategory.AdministrativeSeat : request.category,
                primary = request.primary,
                effectiveWorldTime = request.worldTime,
                visibility = request.visibility,
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Government seat previewed.", before, before, preview: true, subjectId: seatId);
            seatsById[seatId] = record;
            if (request.category == SeatCategory.Capital && governmentsById.TryGetValue(record.governmentId, out GovernmentRecordData government) && politiesById.TryGetValue(government.polityId, out PolityRecordData polity))
            {
                polity.capitalPlaceIds = PoliticalModelUtility.Clean(polity.capitalPlaceIds.Concat(new[] { record.placeId }));
                polity.revision++;
            }
            CompleteTransaction(request.transactionId, "register-seat", seatId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government seat registered.", before, Revision, subjectId: seatId));
        }

        public PoliticalOperationResult AssertSovereignty(SovereigntyClaimRequest request)
        {
            request ??= new SovereigntyClaimRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string claimId = PoliticalModelUtility.Normalize(request.sovereigntyClaimId);
            if (TryDuplicate(request.transactionId, claimId, "assert-sovereignty", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(claimId)) return Fail(PoliticalOperationCode.InvalidRequest, "Sovereignty claim ID is required.", before);
            if (sovereigntyClaimsById.ContainsKey(claimId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Sovereignty claim '{claimId}' already exists with different transaction identity.", before);
            if (!ValidatePolity(request.polityId, out string polityFailure)) return Fail(PoliticalOperationCode.MissingPolity, polityFailure, before);
            if (!ValidateGovernment(request.governmentId, out string governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!ValidateTerritory(request.territoryId, out string territoryFailure)) return Fail(PoliticalOperationCode.MissingTerritory, territoryFailure, before);
            if (!ValidateDiplomacyReferences("", request.recognitionRelationId, "", before, out PoliticalOperationResult diplomacyFailure)) return diplomacyFailure;
            SovereigntyClaimRecordData record = new SovereigntyClaimRecordData
            {
                sovereigntyClaimId = claimId,
                polityId = PoliticalModelUtility.Normalize(request.polityId),
                governmentId = PoliticalModelUtility.Normalize(request.governmentId),
                territoryId = PoliticalModelUtility.Normalize(request.territoryId),
                category = request.category == SovereigntyClaimCategory.Unknown ? SovereigntyClaimCategory.FullSovereignty : request.category,
                state = request.state == SovereigntyClaimState.Unknown ? SovereigntyClaimState.Claimed : request.state,
                recognitionRelationId = PoliticalModelUtility.Normalize(request.recognitionRelationId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                assertedWorldTime = request.worldTime,
                visibility = request.visibility,
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Sovereignty claim previewed.", before, before, preview: true, subjectId: claimId);
            sovereigntyClaimsById[claimId] = record;
            CompleteTransaction(request.transactionId, "assert-sovereignty", claimId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Sovereignty claim asserted.", before, Revision, subjectId: claimId));
        }

        public PoliticalOperationResult CreateJurisdiction(JurisdictionCreateRequest request)
        {
            request ??= new JurisdictionCreateRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string jurisdictionId = PoliticalModelUtility.Normalize(request.jurisdictionId);
            if (TryDuplicate(request.transactionId, jurisdictionId, "create-jurisdiction", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(jurisdictionId)) return Fail(PoliticalOperationCode.InvalidRequest, "Jurisdiction ID is required.", before);
            if (jurisdictionsById.ContainsKey(jurisdictionId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Jurisdiction '{jurisdictionId}' already exists with different transaction identity.", before);
            if (!TryGetDefinition(request.jurisdictionDefinitionId, out JurisdictionDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Jurisdiction definition '{request.jurisdictionDefinitionId}' is missing.", before);
            if (!ValidateGovernment(request.governmentId, out string governmentFailure)) return Fail(PoliticalOperationCode.MissingGovernment, governmentFailure, before);
            if (!ValidateScopeArrays(request, before, out PoliticalOperationResult scopeFailure)) return scopeFailure;
            string sourceJurisdictionId = PoliticalModelUtility.Normalize(request.sourceJurisdictionId);
            if (!string.IsNullOrEmpty(sourceJurisdictionId))
            {
                if (!definition.AllowsDelegation) return Fail(PoliticalOperationCode.InvalidRequest, $"Jurisdiction definition '{definition.Id}' does not allow delegation.", before);
                if (!jurisdictionsById.ContainsKey(sourceJurisdictionId)) return Fail(PoliticalOperationCode.MissingJurisdiction, $"Source jurisdiction '{sourceJurisdictionId}' is missing.", before);
                if (WouldCreateJurisdictionCycle(jurisdictionId, sourceJurisdictionId)) return Fail(PoliticalOperationCode.CycleRejected, "Jurisdiction delegation cycle rejected.", before);
            }
            JurisdictionScopeDimension dimensions = request.scopeDimensions == JurisdictionScopeDimension.None ? InferScopeDimensions(request) : request.scopeDimensions;
            if ((dimensions & ~definition.AllowedDimensions) != 0) return Fail(PoliticalOperationCode.InvalidRequest, $"Jurisdiction '{jurisdictionId}' uses dimensions not allowed by definition '{definition.Id}'.", before);
            JurisdictionSubjectMatter[] subjectMatters = (request.subjectMatters ?? Array.Empty<JurisdictionSubjectMatter>()).Where(item => item != JurisdictionSubjectMatter.Unknown).Distinct().OrderBy(item => item).ToArray();
            if (subjectMatters.Length == 0 && definition.AllowedSubjectMatters.Count > 0) subjectMatters = new[] { definition.AllowedSubjectMatters[0] };
            foreach (JurisdictionSubjectMatter subject in subjectMatters)
            {
                if (definition.AllowedSubjectMatters.Count > 0 && !definition.AllowedSubjectMatters.Contains(subject)) return Fail(PoliticalOperationCode.InvalidRequest, $"Jurisdiction definition '{definition.Id}' does not allow subject matter '{subject}'.", before);
            }

            JurisdictionRecordData record = new JurisdictionRecordData
            {
                jurisdictionId = jurisdictionId,
                jurisdictionDefinitionId = definition.Id,
                governmentId = PoliticalModelUtility.Normalize(request.governmentId),
                sourceJurisdictionId = sourceJurisdictionId,
                parentJurisdictionId = PoliticalModelUtility.Normalize(request.parentJurisdictionId),
                category = request.category == JurisdictionCategory.Unknown ? definition.Category : request.category,
                scopeDimensions = dimensions,
                subjectMatters = subjectMatters,
                territoryIds = PoliticalModelUtility.Clean(request.territoryIds),
                placeIds = PoliticalModelUtility.Clean(request.placeIds),
                personIds = PoliticalModelUtility.Clean(request.personIds),
                organizationIds = PoliticalModelUtility.Clean(request.organizationIds),
                propertyIds = PoliticalModelUtility.Clean(request.propertyIds),
                officeIds = PoliticalModelUtility.Clean(request.officeIds),
                statusIds = PoliticalModelUtility.Clean(request.statusIds),
                lifecycleState = request.lifecycleState == JurisdictionLifecycleState.Unknown ? JurisdictionLifecycleState.Active : request.lifecycleState,
                conflictPolicy = request.conflictPolicy == JurisdictionConflictPolicy.Unknown ? definition.DefaultConflictPolicy : request.conflictPolicy,
                priority = request.priority,
                exclusive = request.exclusive || definition.ExclusiveByDefault,
                sourceAuthorityGrantId = PoliticalModelUtility.Normalize(request.sourceAuthorityGrantId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                effectiveWorldTime = request.worldTime,
                expirationWorldTime = request.expirationWorldTime,
                visibility = request.visibility,
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Jurisdiction previewed.", before, before, preview: true, subjectId: jurisdictionId, jurisdiction: record);
            jurisdictionsById[jurisdictionId] = record;
            CompleteTransaction(request.transactionId, "create-jurisdiction", jurisdictionId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Jurisdiction created.", before, Revision, subjectId: jurisdictionId, jurisdiction: record));
        }

        public JurisdictionResolutionResult ResolveJurisdiction(JurisdictionResolutionRequest request)
        {
            request ??= new JurisdictionResolutionRequest();
            JurisdictionSubjectMatter subject = request.subjectMatter == JurisdictionSubjectMatter.Unknown ? JurisdictionSubjectMatter.GeneralAdministration : request.subjectMatter;
            double worldTime = request.worldTime;
            JurisdictionRecordData[] applicable = jurisdictionsById.Values
                .Where(item => IsJurisdictionActive(item, worldTime))
                .Where(item => string.IsNullOrWhiteSpace(request.requesterGovernmentId) || string.Equals(item.governmentId, PoliticalModelUtility.Normalize(request.requesterGovernmentId), StringComparison.Ordinal))
                .Where(item => AppliesToSubject(item, subject))
                .Where(item => AppliesToScope(item, request))
                .OrderByDescending(item => item.priority)
                .ThenByDescending(item => ScopeSpecificity(item))
                .ThenBy(item => item.jurisdictionId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();

            if (applicable.Length == 0) return JurisdictionResolutionResult.Create(JurisdictionResolutionStatus.NoApplicableJurisdiction, applicable, null, "No applicable jurisdiction.");
            bool contested = applicable.Any(item => item.lifecycleState == JurisdictionLifecycleState.Contested || item.conflictPolicy == JurisdictionConflictPolicy.Contested);
            if (contested) return JurisdictionResolutionResult.Create(JurisdictionResolutionStatus.Contested, applicable, applicable[0], "Applicable jurisdiction is contested.");
            bool exclusiveConflict = applicable.Count(item => item.exclusive || item.conflictPolicy == JurisdictionConflictPolicy.Exclusive) > 1;
            if (exclusiveConflict) return JurisdictionResolutionResult.Create(JurisdictionResolutionStatus.ExclusiveConflict, applicable, applicable[0], "Multiple exclusive jurisdictions apply.");
            bool shared = applicable.Length > 1 && applicable.Any(item => item.conflictPolicy == JurisdictionConflictPolicy.Shared);
            if (shared) return JurisdictionResolutionResult.Create(JurisdictionResolutionStatus.Shared, applicable, applicable[0], "Shared jurisdiction applies.");
            return JurisdictionResolutionResult.Create(applicable.Length > 1 ? JurisdictionResolutionStatus.Shared : JurisdictionResolutionStatus.Applicable, applicable, applicable[0], "Jurisdiction resolved deterministically.");
        }

        public PoliticalOperationResult TransferTerritory(TerritorialTransferRequest request)
        {
            request ??= new TerritorialTransferRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string transitionId = PoliticalModelUtility.Normalize(request.transitionId);
            if (TryDuplicate(request.transactionId, transitionId, "transfer-territory", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(transitionId)) return Fail(PoliticalOperationCode.InvalidRequest, "Territorial transfer transition ID is required.", before);
            if (transitionsById.ContainsKey(transitionId)) return Fail(PoliticalOperationCode.InvalidRequest, $"Transition '{transitionId}' already exists with different transaction identity.", before);
            if (!ValidateGovernment(request.sourceGovernmentId, out string sourceFailure)) return Fail(PoliticalOperationCode.MissingGovernment, sourceFailure, before);
            if (!ValidateGovernment(request.targetGovernmentId, out string targetFailure)) return Fail(PoliticalOperationCode.MissingGovernment, targetFailure, before);
            string[] territories = PoliticalModelUtility.Clean(request.territoryIds);
            if (territories.Length == 0) return Fail(PoliticalOperationCode.InvalidRequest, "Territorial transfer requires at least one territory.", before);
            foreach (string territoryId in territories)
            {
                if (!ValidateTerritory(territoryId, out string territoryFailure)) return Fail(PoliticalOperationCode.MissingTerritory, territoryFailure, before);
                if (controlsById.ContainsKey($"{transitionId}.control.{territoryId}")) return Fail(PoliticalOperationCode.Conflict, $"Transfer control record for territory '{territoryId}' already exists.", before);
                if (administrationsById.ContainsKey($"{transitionId}.administration.{territoryId}")) return Fail(PoliticalOperationCode.Conflict, $"Transfer administration record for territory '{territoryId}' already exists.", before);
            }
            if (!ValidateDiplomacyReferences(request.sourceAgreementId, "", "", before, out PoliticalOperationResult diplomacyFailure)) return diplomacyFailure;

            PoliticalTransitionPlanRecordData transition = new PoliticalTransitionPlanRecordData
            {
                transitionId = transitionId,
                transitionKind = PoliticalTransitionKind.TerritorialTransfer,
                sourceGovernmentId = PoliticalModelUtility.Normalize(request.sourceGovernmentId),
                targetGovernmentId = PoliticalModelUtility.Normalize(request.targetGovernmentId),
                territoryIds = territories,
                sourceAgreementId = PoliticalModelUtility.Normalize(request.sourceAgreementId),
                sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                plannedWorldTime = request.worldTime,
                executed = !request.preview,
                executedWorldTime = request.preview ? -1d : request.worldTime,
                diagnostics = "Transfer changes administration and control only through explicit records; property ownership is untouched.",
                revision = 1L
            };
            if (request.preview) return PoliticalOperationResult.Success("Territorial transfer previewed.", before, before, preview: true, subjectId: transitionId, transition: transition);

            // All references and generated identities are validated before this commit block.
            // The block contains no external calls, so observers can only see the complete transfer.
            foreach (string territoryId in territories)
            {
                foreach (TerritorialControlRecordData active in controlsById.Values.Where(item => string.Equals(item.territoryId, territoryId, StringComparison.Ordinal) && item.endedWorldTime < 0d).ToArray())
                {
                    active.endedWorldTime = request.worldTime;
                    active.state = TerritorialControlState.Historical;
                    active.revision++;
                }
                foreach (TerritoryAdministrationRecordData active in administrationsById.Values.Where(item => string.Equals(item.territoryId, territoryId, StringComparison.Ordinal) && item.endedWorldTime < 0d).ToArray())
                {
                    active.endedWorldTime = request.worldTime;
                    active.state = AdministrationState.Historical;
                    active.revision++;
                }
                controlsById[$"{transitionId}.control.{territoryId}"] = new TerritorialControlRecordData
                {
                    controlId = $"{transitionId}.control.{territoryId}",
                    territoryId = territoryId,
                    controllingGovernmentId = PoliticalModelUtility.Normalize(request.targetGovernmentId),
                    state = TerritorialControlState.Controlled,
                    sourceAgreementId = PoliticalModelUtility.Normalize(request.sourceAgreementId),
                    sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                    effectiveWorldTime = request.worldTime,
                    visibility = request.visibility,
                    revision = 1L
                };
                administrationsById[$"{transitionId}.administration.{territoryId}"] = new TerritoryAdministrationRecordData
                {
                    administrationId = $"{transitionId}.administration.{territoryId}",
                    territoryId = territoryId,
                    administeringGovernmentId = PoliticalModelUtility.Normalize(request.targetGovernmentId),
                    delegatedByGovernmentId = PoliticalModelUtility.Normalize(request.sourceGovernmentId),
                    state = AdministrationState.Delegated,
                    sourceAgreementId = PoliticalModelUtility.Normalize(request.sourceAgreementId),
                    sourceDecisionId = PoliticalModelUtility.Normalize(request.sourceDecisionId),
                    effectiveWorldTime = request.worldTime,
                    visibility = request.visibility,
                    revision = 1L
                };
                PoliticalTerritoryRecordData territory = territoriesById[territoryId];
                territory.primaryGovernmentId = PoliticalModelUtility.Normalize(request.targetGovernmentId);
                territory.lifecycleState = TerritoryLifecycleState.Transferred;
                territory.revision++;
            }
            transitionsById[transitionId] = transition;
            CompleteTransaction(request.transactionId, "transfer-territory", transitionId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Territorial transfer committed atomically without changing property ownership.", before, Revision, subjectId: transitionId, transition: transition));
        }

        public PoliticalOperationResult TransitionGovernment(GovernmentTransitionRequest request)
        {
            request ??= new GovernmentTransitionRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult readyFailure)) return readyFailure;
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            if (TryDuplicate(request.transactionId, governmentId, "transition-government", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!governmentsById.TryGetValue(governmentId, out GovernmentRecordData record)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (request.targetState == GovernmentLifecycleState.Unknown) return Fail(PoliticalOperationCode.InvalidState, "Government target state is invalid.", before);
            GovernmentRecordData changed = record.Clone();
            changed.lifecycleState = request.targetState;
            changed.endedWorldTime = request.targetState == GovernmentLifecycleState.Collapsed || request.targetState == GovernmentLifecycleState.Succeeded || request.targetState == GovernmentLifecycleState.Dissolved || request.targetState == GovernmentLifecycleState.Historical ? request.worldTime : changed.endedWorldTime;
            changed.sourceDecisionId = string.IsNullOrWhiteSpace(request.sourceDecisionId) ? changed.sourceDecisionId : request.sourceDecisionId.Trim();
            changed.revision++;
            if (request.preview) return PoliticalOperationResult.Success("Government lifecycle transition previewed.", before, before, preview: true, subjectId: governmentId, government: changed);
            governmentsById[governmentId] = changed;
            CompleteTransaction(request.transactionId, "transition-government", governmentId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government lifecycle transitioned.", before, Revision, subjectId: governmentId, government: changed));
        }

        public PoliticalProjectionResult<PolityRecordData> ProjectPolity(string polityId, bool privileged)
        {
            polityId = PoliticalModelUtility.Normalize(polityId);
            if (!politiesById.TryGetValue(polityId, out PolityRecordData record)) return PoliticalProjectionResult<PolityRecordData>.Denied(polityId, "Polity is missing.");
            PolityRecordData clone = record.Clone();
            if (privileged || !PoliticalModelUtility.IsHidden(record.visibility)) return PoliticalProjectionResult<PolityRecordData>.Full(polityId, clone);
            clone.claimantGovernmentIds = Array.Empty<string>();
            clone.claimedTerritoryIds = Array.Empty<string>();
            clone.diplomaticActorId = string.Empty;
            clone.sourceEventId = string.Empty;
            clone.sourceRecordId = string.Empty;
            return PoliticalProjectionResult<PolityRecordData>.RedactedProjection(polityId, clone);
        }

        public PoliticalProjectionResult<GovernmentRecordData> ProjectGovernment(string governmentId, bool privileged)
        {
            governmentId = PoliticalModelUtility.Normalize(governmentId);
            if (!governmentsById.TryGetValue(governmentId, out GovernmentRecordData record)) return PoliticalProjectionResult<GovernmentRecordData>.Denied(governmentId, "Government is missing.");
            GovernmentRecordData clone = record.Clone();
            if (privileged || !PoliticalModelUtility.IsHidden(record.visibility)) return PoliticalProjectionResult<GovernmentRecordData>.Full(governmentId, clone);
            clone.governingOrganizationIds = Array.Empty<string>();
            clone.primaryGoverningOrganizationId = string.Empty;
            clone.sourceAuthorityGrantId = string.Empty;
            clone.sourceDecisionId = string.Empty;
            clone.sourceDiplomaticRecognitionId = string.Empty;
            return PoliticalProjectionResult<GovernmentRecordData>.RedactedProjection(governmentId, clone);
        }

        public GovernmentRuntimeSaveData CreateSaveData()
        {
            return new GovernmentRuntimeSaveData
            {
                schemaVersion = 1,
                revision = Revision,
                worldId = worldId ?? string.Empty,
                polities = politiesById.Values.OrderBy(item => item.polityId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                names = namesById.Values.OrderBy(item => item.nameRecordId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                governments = governmentsById.Values.OrderBy(item => item.governmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                institutionRoles = institutionRolesById.Values.OrderBy(item => item.roleId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                territories = territoriesById.Values.OrderBy(item => item.territoryId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                territoryPlaceMemberships = territoryPlaceMembershipsById.Values.OrderBy(item => item.membershipId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                claims = claimsById.Values.OrderBy(item => item.claimId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                controls = controlsById.Values.OrderBy(item => item.controlId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                administrations = administrationsById.Values.OrderBy(item => item.administrationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                seats = seatsById.Values.OrderBy(item => item.seatId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                sovereigntyClaims = sovereigntyClaimsById.Values.OrderBy(item => item.sovereigntyClaimId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                jurisdictions = jurisdictionsById.Values.OrderBy(item => item.jurisdictionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                organizationCharters = organizationChartersById.Values.OrderBy(item => item.charterId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                officeTenures = officeTenuresById.Values.OrderBy(item => item.tenureId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                elections = electionsById.Values.OrderBy(item => item.electionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                permits = permitsById.Values.OrderBy(item => item.permitId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                fiscalMandates = fiscalMandatesById.Values.OrderBy(item => item.mandateId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                legitimacyAssessments = legitimacyAssessmentsById.Values.OrderBy(item => item.assessmentId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                remittancePolicies = remittancePoliciesById.Values.OrderBy(item => item.policyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                remittanceSettlements = remittanceSettlementsById.Values.OrderBy(item => item.settlementId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                fiscalReliefs = fiscalReliefsById.Values.OrderBy(item => item.reliefId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                actionRegulations = actionRegulationsById.Values.OrderBy(item => item.regulationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                officeVacancies = officeVacanciesById.Values.OrderBy(item => item.vacancyId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                legitimacyEvents = legitimacyEventsById.Values.OrderBy(item => item.eventId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                budgetCycles = budgetCyclesById.Values.OrderBy(item => item.cycleId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                transitions = transitionsById.Values.OrderBy(item => item.transitionId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray(),
                transactions = transactionsById.Values.OrderBy(item => item.revision).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToArray()
            };
        }

        public PoliticalOperationResult RestoreFromSaveData(GovernmentRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, OrganizationMembershipRuntime membershipRuntime, OrganizationAuthorityRuntime authorityRuntime, OrganizationDecisionRuntime decisionRuntime, OrganizationResourceRuntime resourceRuntime, FactionRuntime factionRuntime, DiplomacyRuntime diplomacyRuntime, PropertyRuntime propertyRuntime, string runtimeWorldId, IEnumerable<string> personIds, IEnumerable<string> placeIds, bool restoring = false)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, organizationRuntime, factionRuntime, diplomacyRuntime, propertyRuntime, runtimeWorldId, personIds, placeIds, out string failure)) return Fail(PoliticalOperationCode.ValidationFailed, failure, before);
            Configure(definitionRegistry, organizationRuntime, membershipRuntime, authorityRuntime, decisionRuntime, resourceRuntime, factionRuntime, diplomacyRuntime, propertyRuntime, runtimeWorldId, personIds, placeIds);
            RestoreInternal(saveData.Clone());
            return PoliticalOperationResult.Success(restoring ? "Government runtime restored." : "Government runtime loaded.", before, Revision);
        }

        public static bool ValidateSaveData(GovernmentRuntimeSaveData saveData, DefinitionRegistry registry, OrganizationRuntime organizations, FactionRuntime factions, DiplomacyRuntime diplomacy, PropertyRuntime properties, string expectedWorldId, IEnumerable<string> persons, IEnumerable<string> places, out string failure)
        {
            failure = string.Empty;
            if (saveData == null) { failure = "Government save data is missing."; return false; }
            if (saveData.schemaVersion != 1) { failure = $"Unsupported government save schema version {saveData.schemaVersion}."; return false; }
            string expected = PoliticalModelUtility.Normalize(expectedWorldId);
            if (!string.IsNullOrEmpty(expected) && !string.IsNullOrWhiteSpace(saveData.worldId) && !string.Equals(saveData.worldId.Trim(), expected, StringComparison.Ordinal)) { failure = $"Government save world '{saveData.worldId}' does not match expected world '{expected}'."; return false; }

            HashSet<string> personSet = new HashSet<string>(PoliticalModelUtility.Clean(persons), StringComparer.Ordinal);
            HashSet<string> placeSet = new HashSet<string>(PoliticalModelUtility.Clean(places), StringComparer.Ordinal);
            HashSet<string> polityIds = IdSet(saveData.polities?.Select(item => item?.polityId), "polity", out failure); if (failure.Length > 0) return false;
            HashSet<string> governmentIds = IdSet(saveData.governments?.Select(item => item?.governmentId), "government", out failure); if (failure.Length > 0) return false;
            HashSet<string> territoryIds = IdSet(saveData.territories?.Select(item => item?.territoryId), "territory", out failure); if (failure.Length > 0) return false;
            HashSet<string> jurisdictionIds = IdSet(saveData.jurisdictions?.Select(item => item?.jurisdictionId), "jurisdiction", out failure); if (failure.Length > 0) return false;

            foreach (PolityRecordData polity in saveData.polities ?? Array.Empty<PolityRecordData>())
            {
                if (registry != null && !registry.TryGet(polity.polityDefinitionId, out PolityDefinition _)) { failure = $"Polity '{polity.polityId}' references missing Polity definition '{polity.polityDefinitionId}'."; return false; }
                if (!ValidateRefs(polity.claimantGovernmentIds, governmentIds, true, out failure, "polity claimant government")) return false;
                if (!ValidateRefs(polity.claimedTerritoryIds, territoryIds, true, out failure, "polity claimed territory")) return false;
                if (!ValidatePlaces(polity.capitalPlaceIds, placeSet, true, out failure)) return false;
                if (!ValidateRefs(polity.predecessorPolityIds, polityIds, true, out failure, "polity predecessor")) return false;
                if (!ValidateRefs(polity.successorPolityIds, polityIds, true, out failure, "polity successor")) return false;
            }

            foreach (GovernmentRecordData government in saveData.governments ?? Array.Empty<GovernmentRecordData>())
            {
                if (registry != null && !registry.TryGet(government.governmentDefinitionId, out GovernmentDefinition _)) { failure = $"Government '{government.governmentId}' references missing Government definition '{government.governmentDefinitionId}'."; return false; }
                if (!polityIds.Contains(PoliticalModelUtility.Normalize(government.polityId))) { failure = $"Government '{government.governmentId}' references missing polity '{government.polityId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(government.parentGovernmentId) && !governmentIds.Contains(government.parentGovernmentId.Trim())) { failure = $"Government '{government.governmentId}' references missing parent government '{government.parentGovernmentId}'."; return false; }
                if (!ValidateRefs(government.subordinateGovernmentIds, governmentIds, true, out failure, "subordinate government")) return false;
                foreach (string organizationId in PoliticalModelUtility.Clean(government.governingOrganizationIds))
                {
                    if (organizations != null && !organizations.TryGetSnapshot(organizationId, out _)) { failure = $"Government '{government.governmentId}' references missing Organization '{organizationId}'."; return false; }
                }
            }

            foreach (PoliticalTerritoryRecordData territory in saveData.territories ?? Array.Empty<PoliticalTerritoryRecordData>())
            {
                if (registry != null && !registry.TryGet(territory.territoryDefinitionId, out PoliticalTerritoryDefinition _)) { failure = $"Territory '{territory.territoryId}' references missing Political Territory definition '{territory.territoryDefinitionId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(territory.polityId) && !polityIds.Contains(territory.polityId.Trim())) { failure = $"Territory '{territory.territoryId}' references missing polity '{territory.polityId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(territory.primaryGovernmentId) && !governmentIds.Contains(territory.primaryGovernmentId.Trim())) { failure = $"Territory '{territory.territoryId}' references missing government '{territory.primaryGovernmentId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(territory.parentTerritoryId) && !territoryIds.Contains(territory.parentTerritoryId.Trim())) { failure = $"Territory '{territory.territoryId}' references missing parent territory '{territory.parentTerritoryId}'."; return false; }
                if (!ValidateRefs(territory.childTerritoryIds, territoryIds, true, out failure, "child territory")) return false;
                if (!ValidatePlaces(territory.placeIds, placeSet, true, out failure)) return false;
            }

            foreach (TerritoryPlaceMembershipRecordData membership in saveData.territoryPlaceMemberships ?? Array.Empty<TerritoryPlaceMembershipRecordData>())
            {
                if (!territoryIds.Contains(PoliticalModelUtility.Normalize(membership.territoryId))) { failure = $"Territory-place membership '{membership.membershipId}' references missing territory '{membership.territoryId}'."; return false; }
                if (!ValidatePlaces(new[] { membership.placeId }, placeSet, true, out failure)) return false;
            }

            foreach (TerritorialClaimRecordData claim in saveData.claims ?? Array.Empty<TerritorialClaimRecordData>())
            {
                if (registry != null && !registry.TryGet(claim.claimDefinitionId, out TerritorialClaimDefinition _)) { failure = $"Claim '{claim.claimId}' references missing Territorial Claim definition '{claim.claimDefinitionId}'."; return false; }
                if (!territoryIds.Contains(PoliticalModelUtility.Normalize(claim.territoryId))) { failure = $"Claim '{claim.claimId}' references missing territory '{claim.territoryId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(claim.claimantPolityId) && !polityIds.Contains(claim.claimantPolityId.Trim())) { failure = $"Claim '{claim.claimId}' references missing polity '{claim.claimantPolityId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(claim.claimantGovernmentId) && !governmentIds.Contains(claim.claimantGovernmentId.Trim())) { failure = $"Claim '{claim.claimId}' references missing government '{claim.claimantGovernmentId}'."; return false; }
                if (!ValidateDiplomacyReference(diplomacy, claim.basisAgreementId, "agreement", out failure)) return false;
                if (!ValidateDiplomacyReference(diplomacy, claim.basisDiplomaticRelationId, "relation", out failure)) return false;
            }

            foreach (TerritorialControlRecordData control in saveData.controls ?? Array.Empty<TerritorialControlRecordData>())
            {
                if (!territoryIds.Contains(PoliticalModelUtility.Normalize(control.territoryId))) { failure = $"Control '{control.controlId}' references missing territory '{control.territoryId}'."; return false; }
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(control.controllingGovernmentId))) { failure = $"Control '{control.controlId}' references missing government '{control.controllingGovernmentId}'."; return false; }
            }

            foreach (TerritoryAdministrationRecordData administration in saveData.administrations ?? Array.Empty<TerritoryAdministrationRecordData>())
            {
                if (!territoryIds.Contains(PoliticalModelUtility.Normalize(administration.territoryId))) { failure = $"Administration '{administration.administrationId}' references missing territory '{administration.territoryId}'."; return false; }
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(administration.administeringGovernmentId))) { failure = $"Administration '{administration.administrationId}' references missing government '{administration.administeringGovernmentId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(administration.delegatedByGovernmentId) && !governmentIds.Contains(administration.delegatedByGovernmentId.Trim())) { failure = $"Administration '{administration.administrationId}' references missing delegating government '{administration.delegatedByGovernmentId}'."; return false; }
            }

            foreach (GovernmentSeatRecordData seat in saveData.seats ?? Array.Empty<GovernmentSeatRecordData>())
            {
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(seat.governmentId))) { failure = $"Seat '{seat.seatId}' references missing government '{seat.governmentId}'."; return false; }
                if (!ValidatePlaces(new[] { seat.placeId }, placeSet, true, out failure)) return false;
            }

            foreach (SovereigntyClaimRecordData claim in saveData.sovereigntyClaims ?? Array.Empty<SovereigntyClaimRecordData>())
            {
                if (!polityIds.Contains(PoliticalModelUtility.Normalize(claim.polityId))) { failure = $"Sovereignty claim '{claim.sovereigntyClaimId}' references missing polity '{claim.polityId}'."; return false; }
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(claim.governmentId))) { failure = $"Sovereignty claim '{claim.sovereigntyClaimId}' references missing government '{claim.governmentId}'."; return false; }
                if (!territoryIds.Contains(PoliticalModelUtility.Normalize(claim.territoryId))) { failure = $"Sovereignty claim '{claim.sovereigntyClaimId}' references missing territory '{claim.territoryId}'."; return false; }
            }

            foreach (JurisdictionRecordData jurisdiction in saveData.jurisdictions ?? Array.Empty<JurisdictionRecordData>())
            {
                if (registry != null && !registry.TryGet(jurisdiction.jurisdictionDefinitionId, out JurisdictionDefinition _)) { failure = $"Jurisdiction '{jurisdiction.jurisdictionId}' references missing Jurisdiction definition '{jurisdiction.jurisdictionDefinitionId}'."; return false; }
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(jurisdiction.governmentId))) { failure = $"Jurisdiction '{jurisdiction.jurisdictionId}' references missing government '{jurisdiction.governmentId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(jurisdiction.sourceJurisdictionId) && !jurisdictionIds.Contains(jurisdiction.sourceJurisdictionId.Trim())) { failure = $"Jurisdiction '{jurisdiction.jurisdictionId}' references missing source jurisdiction '{jurisdiction.sourceJurisdictionId}'."; return false; }
                if (!ValidateRefs(jurisdiction.territoryIds, territoryIds, true, out failure, "jurisdiction territory")) return false;
                if (!ValidatePlaces(jurisdiction.placeIds, placeSet, true, out failure)) return false;
                if (!ValidateRefs(jurisdiction.personIds, personSet, false, out failure, "jurisdiction person")) return false;
                foreach (string organizationId in PoliticalModelUtility.Clean(jurisdiction.organizationIds)) if (organizations != null && !organizations.TryGetSnapshot(organizationId, out _)) { failure = $"Jurisdiction '{jurisdiction.jurisdictionId}' references missing Organization '{organizationId}'."; return false; }
                foreach (string propertyId in PoliticalModelUtility.Clean(jurisdiction.propertyIds)) if (properties != null && !properties.TryGetProperty(propertyId, out _)) { failure = $"Jurisdiction '{jurisdiction.jurisdictionId}' references missing Property '{propertyId}'."; return false; }
            }

            HashSet<string> charterIds = IdSet(saveData.organizationCharters?.Select(item => item?.charterId), "government organization charter", out failure); if (failure.Length > 0) return false;
            foreach (GovernmentOrganizationCharterRecordData charter in saveData.organizationCharters ?? Array.Empty<GovernmentOrganizationCharterRecordData>())
            {
                if (registry != null && !registry.TryGet(charter.charterDefinitionId, out GovernmentOrganizationCharterDefinition _)) { failure = $"Government organization charter '{charter.charterId}' references missing definition '{charter.charterDefinitionId}'."; return false; }
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(charter.governmentId))) { failure = $"Government organization charter '{charter.charterId}' references missing government '{charter.governmentId}'."; return false; }
                if (organizations != null && !organizations.TryGetSnapshot(charter.organizationId, out _)) { failure = $"Government organization charter '{charter.charterId}' references missing organization '{charter.organizationId}'."; return false; }
                if (!string.IsNullOrWhiteSpace(charter.supervisingOrganizationId) && organizations != null && !organizations.TryGetSnapshot(charter.supervisingOrganizationId, out _)) { failure = $"Government organization charter '{charter.charterId}' references missing supervisor '{charter.supervisingOrganizationId}'."; return false; }
                if (!ValidateRefs(charter.jurisdictionIds, jurisdictionIds, true, out failure, "government organization charter jurisdiction")) return false;
                foreach (string jurisdictionId in PoliticalModelUtility.Clean(charter.jurisdictionIds)) if (saveData.jurisdictions.First(item => item.jurisdictionId == jurisdictionId).governmentId != charter.governmentId) { failure = $"Government organization charter '{charter.charterId}' uses a jurisdiction owned by another government."; return false; }
                if (string.IsNullOrWhiteSpace(charter.legalInstrumentId)) { failure = $"Government organization charter '{charter.charterId}' has no legal instrument ID."; return false; }
                if (!Enum.IsDefined(typeof(GovernmentOrganizationCharterCategory), charter.category) || charter.category == GovernmentOrganizationCharterCategory.Unknown) { failure = $"Government organization charter '{charter.charterId}' has an invalid category."; return false; }
                if (!Enum.IsDefined(typeof(GovernmentOrganizationCharterLifecycleState), charter.lifecycleState) || charter.lifecycleState == GovernmentOrganizationCharterLifecycleState.Unknown) { failure = $"Government organization charter '{charter.charterId}' has an invalid lifecycle state."; return false; }
            }

            if (!ValidateAdministrationSaveData(saveData, registry, organizations, governmentIds, jurisdictionIds, personSet, out failure)) return false;
            if (!ValidateFiscalHierarchySaveData(saveData, registry, governmentIds, jurisdictionIds, out failure)) return false;

            if (HasCycle(saveData.governments?.ToDictionary(item => item.governmentId ?? string.Empty, item => item.parentGovernmentId ?? string.Empty, StringComparer.Ordinal), out failure, "government")) return false;
            if (HasCycle(saveData.territories?.ToDictionary(item => item.territoryId ?? string.Empty, item => item.parentTerritoryId ?? string.Empty, StringComparer.Ordinal), out failure, "territory")) return false;
            if (HasCycle(saveData.jurisdictions?.ToDictionary(item => item.jurisdictionId ?? string.Empty, item => item.sourceJurisdictionId ?? string.Empty, StringComparer.Ordinal), out failure, "jurisdiction delegation")) return false;
            return true;
        }

        public void Reset()
        {
            politiesById.Clear();
            namesById.Clear();
            governmentsById.Clear();
            institutionRolesById.Clear();
            territoriesById.Clear();
            territoryPlaceMembershipsById.Clear();
            claimsById.Clear();
            controlsById.Clear();
            administrationsById.Clear();
            seatsById.Clear();
            sovereigntyClaimsById.Clear();
            jurisdictionsById.Clear();
            organizationChartersById.Clear();
            officeTenuresById.Clear();
            electionsById.Clear();
            permitsById.Clear();
            fiscalMandatesById.Clear();
            legitimacyAssessmentsById.Clear();
            remittancePoliciesById.Clear();
            remittanceSettlementsById.Clear();
            fiscalReliefsById.Clear();
            actionRegulationsById.Clear();
            officeVacanciesById.Clear();
            legitimacyEventsById.Clear();
            budgetCyclesById.Clear();
            transitionsById.Clear();
            transactionsById.Clear();
            Revision = 0L;
        }

        public void Dispose()
        {
            Reset();
            OperationCommitted = null;
            disposed = true;
        }

        private void RestoreInternal(GovernmentRuntimeSaveData saveData)
        {
            Reset();
            GovernmentRuntimeSaveData clone = saveData.Clone();
            worldId = clone.worldId ?? string.Empty;
            Revision = clone.revision;
            foreach (PolityRecordData item in clone.polities) politiesById[item.polityId] = item;
            foreach (PoliticalNameRecordData item in clone.names) namesById[item.nameRecordId] = item;
            foreach (GovernmentRecordData item in clone.governments) governmentsById[item.governmentId] = item;
            foreach (GovernmentInstitutionRoleRecordData item in clone.institutionRoles) institutionRolesById[item.roleId] = item;
            foreach (PoliticalTerritoryRecordData item in clone.territories) territoriesById[item.territoryId] = item;
            foreach (TerritoryPlaceMembershipRecordData item in clone.territoryPlaceMemberships) territoryPlaceMembershipsById[item.membershipId] = item;
            foreach (TerritorialClaimRecordData item in clone.claims) claimsById[item.claimId] = item;
            foreach (TerritorialControlRecordData item in clone.controls) controlsById[item.controlId] = item;
            foreach (TerritoryAdministrationRecordData item in clone.administrations) administrationsById[item.administrationId] = item;
            foreach (GovernmentSeatRecordData item in clone.seats) seatsById[item.seatId] = item;
            foreach (SovereigntyClaimRecordData item in clone.sovereigntyClaims) sovereigntyClaimsById[item.sovereigntyClaimId] = item;
            foreach (JurisdictionRecordData item in clone.jurisdictions) jurisdictionsById[item.jurisdictionId] = item;
            foreach (GovernmentOrganizationCharterRecordData item in clone.organizationCharters) organizationChartersById[item.charterId] = item;
            foreach (GovernmentOfficeTenureRecordData item in clone.officeTenures) officeTenuresById[item.tenureId] = item;
            foreach (GovernmentElectionRecordData item in clone.elections) electionsById[item.electionId] = item;
            foreach (GovernmentPermitRecordData item in clone.permits) permitsById[item.permitId] = item;
            foreach (GovernmentFiscalMandateRecordData item in clone.fiscalMandates) fiscalMandatesById[item.mandateId] = item;
            foreach (GovernmentLegitimacyRecordData item in clone.legitimacyAssessments) legitimacyAssessmentsById[item.assessmentId] = item;
            foreach (GovernmentRemittancePolicyRecordData item in clone.remittancePolicies) remittancePoliciesById[item.policyId] = item;
            foreach (GovernmentRemittanceSettlementRecordData item in clone.remittanceSettlements) remittanceSettlementsById[item.settlementId] = item;
            foreach (GovernmentFiscalReliefRecordData item in clone.fiscalReliefs) fiscalReliefsById[item.reliefId] = item;
            foreach (GovernmentActionRegulationRecordData item in clone.actionRegulations) actionRegulationsById[item.regulationId] = item;
            foreach (GovernmentOfficeVacancyRecordData item in clone.officeVacancies) officeVacanciesById[item.vacancyId] = item;
            foreach (GovernmentLegitimacyEventRecordData item in clone.legitimacyEvents) legitimacyEventsById[item.eventId] = item;
            foreach (GovernmentBudgetCycleRecordData item in clone.budgetCycles) budgetCyclesById[item.cycleId] = item;
            foreach (PoliticalTransitionPlanRecordData item in clone.transitions) transitionsById[item.transitionId] = item;
            foreach (PoliticalTransactionRecordData item in clone.transactions) transactionsById[item.transactionId] = item;
        }

        private PoliticalOperationResult PublishCommit(PoliticalOperationResult result)
        {
            if (result != null && result.Succeeded && !result.Preview && !result.Duplicate)
            {
                Action<PoliticalOperationResult> handlers = OperationCommitted;
                if (handlers != null)
                {
                    foreach (Action<PoliticalOperationResult> handler in handlers.GetInvocationList())
                    {
                        try { handler(result); }
                        catch (Exception exception) { Debug.LogException(exception); }
                    }
                }
            }
            return result;
        }

        private bool ValidateOptionalPoliticalReferences(string sourcePolityId, string targetPolityId, string sourceGovernmentId, string targetGovernmentId, IEnumerable<string> territoryIds, long before, out PoliticalOperationResult failure)
        {
            foreach (string polityId in PoliticalModelUtility.Clean(new[] { sourcePolityId, targetPolityId }))
            {
                if (!politiesById.ContainsKey(polityId)) { failure = Fail(PoliticalOperationCode.MissingPolity, $"Polity '{polityId}' is missing.", before); return false; }
            }
            foreach (string governmentId in PoliticalModelUtility.Clean(new[] { sourceGovernmentId, targetGovernmentId }))
            {
                if (!governmentsById.ContainsKey(governmentId)) { failure = Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before); return false; }
            }
            foreach (string territoryId in PoliticalModelUtility.Clean(territoryIds))
            {
                if (!territoriesById.ContainsKey(territoryId)) { failure = Fail(PoliticalOperationCode.MissingTerritory, $"Territory '{territoryId}' is missing.", before); return false; }
            }
            failure = null;
            return true;
        }

        private bool Ready(out PoliticalOperationResult failure)
        {
            if (disposed) { failure = Fail(PoliticalOperationCode.Disposed, "Government runtime is disposed.", Revision); return false; }
            if (registry == null) { failure = Fail(PoliticalOperationCode.InvalidRequest, "Definition registry is missing.", Revision); return false; }
            failure = null;
            return true;
        }

        private bool TryDuplicate(string transactionId, string subjectId, string operationKind, long before, out PoliticalOperationResult duplicate)
        {
            transactionId = PoliticalModelUtility.Normalize(transactionId);
            subjectId = PoliticalModelUtility.Normalize(subjectId);
            duplicate = null;
            if (string.IsNullOrWhiteSpace(transactionId)) return false;
            if (!transactionsById.TryGetValue(transactionId, out PoliticalTransactionRecordData transaction)) return false;
            if (string.Equals(transaction.subjectId, subjectId, StringComparison.Ordinal) && string.Equals(transaction.operationKind, operationKind, StringComparison.Ordinal))
            {
                duplicate = PoliticalOperationResult.Success("Duplicate political transaction ignored.", before, before, duplicate: true, subjectId: subjectId);
                return true;
            }

            duplicate = Fail(PoliticalOperationCode.InvalidRequest, $"Transaction '{transactionId}' already exists with different identity.", before);
            return true;
        }

        private void CompleteTransaction(string transactionId, string operationKind, string subjectId)
        {
            transactionId = PoliticalModelUtility.Normalize(transactionId);
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new PoliticalTransactionRecordData
            {
                transactionId = transactionId,
                operationKind = operationKind ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                revision = Revision + 1L
            };
        }

        private PoliticalOperationResult Fail(PoliticalOperationCode code, string message, long before) => PoliticalOperationResult.Failure(code, message, before);

        private bool TryGetDefinition<TDefinition>(string id, out TDefinition definition) where TDefinition : class, IGameDefinition
        {
            definition = null;
            return registry != null && registry.TryGet(PoliticalModelUtility.Normalize(id), out definition);
        }

        private bool ValidatePolity(string polityId, out string failure)
        {
            polityId = PoliticalModelUtility.Normalize(polityId);
            if (politiesById.ContainsKey(polityId)) { failure = string.Empty; return true; }
            failure = $"Polity '{polityId}' is missing.";
            return false;
        }

        private bool ValidateGovernment(string governmentId, out string failure)
        {
            governmentId = PoliticalModelUtility.Normalize(governmentId);
            if (governmentsById.ContainsKey(governmentId)) { failure = string.Empty; return true; }
            failure = $"Government '{governmentId}' is missing.";
            return false;
        }

        private bool ValidateTerritory(string territoryId, out string failure)
        {
            territoryId = PoliticalModelUtility.Normalize(territoryId);
            if (territoriesById.ContainsKey(territoryId)) { failure = string.Empty; return true; }
            failure = $"Territory '{territoryId}' is missing.";
            return false;
        }

        private bool ValidateOrganization(string organizationId, out string failure)
        {
            organizationId = PoliticalModelUtility.Normalize(organizationId);
            if (organizations == null || organizations.TryGetSnapshot(organizationId, out _)) { failure = string.Empty; return true; }
            failure = $"Organization '{organizationId}' is missing.";
            return false;
        }

        private bool ValidatePlace(string placeId, out string failure)
        {
            placeId = PoliticalModelUtility.Normalize(placeId);
            if (!string.IsNullOrEmpty(placeId) && (knownPlaceIds.Count == 0 || knownPlaceIds.Contains(placeId))) { failure = string.Empty; return true; }
            failure = $"Place '{placeId}' is missing.";
            return false;
        }

        private bool ValidatePlacesOrFail(IEnumerable<string> placeIds, long before, out PoliticalOperationResult failure)
        {
            foreach (string placeId in PoliticalModelUtility.Clean(placeIds))
            {
                if (!ValidatePlace(placeId, out string placeFailure))
                {
                    failure = Fail(PoliticalOperationCode.InvalidReference, placeFailure, before);
                    return false;
                }
            }

            failure = null;
            return true;
        }

        private bool ValidateDiplomacyReferences(string agreementId, string relationId, string warId, long before, out PoliticalOperationResult failure)
        {
            if (!string.IsNullOrWhiteSpace(agreementId) && diplomacy != null && !diplomacy.TryGetAgreement(agreementId.Trim(), out _)) { failure = Fail(PoliticalOperationCode.InvalidReference, $"Diplomatic agreement '{agreementId}' is missing.", before); return false; }
            if (!string.IsNullOrWhiteSpace(relationId) && diplomacy != null && !diplomacy.TryGetRelation(relationId.Trim(), out _)) { failure = Fail(PoliticalOperationCode.InvalidReference, $"Diplomatic relation '{relationId}' is missing.", before); return false; }
            if (!string.IsNullOrWhiteSpace(warId) && diplomacy != null && !diplomacy.TryGetWar(warId.Trim(), out _)) { failure = Fail(PoliticalOperationCode.InvalidReference, $"Diplomatic war '{warId}' is missing.", before); return false; }
            failure = null;
            return true;
        }

        private bool ValidateScopeArrays(JurisdictionCreateRequest request, long before, out PoliticalOperationResult failure)
        {
            foreach (string territoryId in PoliticalModelUtility.Clean(request.territoryIds)) if (!ValidateTerritory(territoryId, out string territoryFailure)) { failure = Fail(PoliticalOperationCode.MissingTerritory, territoryFailure, before); return false; }
            foreach (string placeId in PoliticalModelUtility.Clean(request.placeIds)) if (!ValidatePlace(placeId, out string placeFailure)) { failure = Fail(PoliticalOperationCode.InvalidReference, placeFailure, before); return false; }
            foreach (string personId in PoliticalModelUtility.Clean(request.personIds)) if (knownPersonIds.Count > 0 && !knownPersonIds.Contains(personId)) { failure = Fail(PoliticalOperationCode.InvalidReference, $"Person '{personId}' is missing.", before); return false; }
            foreach (string organizationId in PoliticalModelUtility.Clean(request.organizationIds)) if (!ValidateOrganization(organizationId, out string organizationFailure)) { failure = Fail(PoliticalOperationCode.InvalidReference, organizationFailure, before); return false; }
            foreach (string propertyId in PoliticalModelUtility.Clean(request.propertyIds)) if (properties != null && !properties.TryGetProperty(propertyId, out _)) { failure = Fail(PoliticalOperationCode.InvalidReference, $"Property '{propertyId}' is missing.", before); return false; }
            failure = null;
            return true;
        }

        private static JurisdictionScopeDimension InferScopeDimensions(JurisdictionCreateRequest request)
        {
            JurisdictionScopeDimension dimensions = JurisdictionScopeDimension.None;
            if (PoliticalModelUtility.Clean(request.territoryIds).Length > 0) dimensions |= JurisdictionScopeDimension.Territory;
            if (PoliticalModelUtility.Clean(request.placeIds).Length > 0) dimensions |= JurisdictionScopeDimension.Place;
            if (PoliticalModelUtility.Clean(request.personIds).Length > 0) dimensions |= JurisdictionScopeDimension.Person;
            if (PoliticalModelUtility.Clean(request.organizationIds).Length > 0) dimensions |= JurisdictionScopeDimension.Organization;
            if (PoliticalModelUtility.Clean(request.propertyIds).Length > 0) dimensions |= JurisdictionScopeDimension.Property;
            if ((request.subjectMatters ?? Array.Empty<JurisdictionSubjectMatter>()).Any(item => item != JurisdictionSubjectMatter.Unknown)) dimensions |= JurisdictionScopeDimension.SubjectMatter;
            if (PoliticalModelUtility.Clean(request.officeIds).Length > 0) dimensions |= JurisdictionScopeDimension.Office;
            if (PoliticalModelUtility.Clean(request.statusIds).Length > 0) dimensions |= JurisdictionScopeDimension.Status;
            return dimensions == JurisdictionScopeDimension.None ? JurisdictionScopeDimension.SubjectMatter : dimensions;
        }

        private bool WouldCreateGovernmentCycle(string newGovernmentId, string parentGovernmentId)
        {
            string cursor = parentGovernmentId;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(cursor))
            {
                if (!visited.Add(cursor)) return true;
                if (string.Equals(cursor, newGovernmentId, StringComparison.Ordinal)) return true;
                cursor = governmentsById.TryGetValue(cursor, out GovernmentRecordData parent) ? parent.parentGovernmentId : string.Empty;
            }
            return false;
        }

        private bool WouldCreateTerritoryCycle(string newTerritoryId, string parentTerritoryId)
        {
            string cursor = parentTerritoryId;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(cursor))
            {
                if (!visited.Add(cursor)) return true;
                if (string.Equals(cursor, newTerritoryId, StringComparison.Ordinal)) return true;
                cursor = territoriesById.TryGetValue(cursor, out PoliticalTerritoryRecordData parent) ? parent.parentTerritoryId : string.Empty;
            }
            return false;
        }

        private bool WouldCreateJurisdictionCycle(string newJurisdictionId, string sourceJurisdictionId)
        {
            string cursor = sourceJurisdictionId;
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            while (!string.IsNullOrEmpty(cursor))
            {
                if (!visited.Add(cursor)) return true;
                if (string.Equals(cursor, newJurisdictionId, StringComparison.Ordinal)) return true;
                cursor = jurisdictionsById.TryGetValue(cursor, out JurisdictionRecordData parent) ? parent.sourceJurisdictionId : string.Empty;
            }
            return false;
        }

        private static bool IsJurisdictionActive(JurisdictionRecordData jurisdiction, double worldTime)
        {
            bool activeState = jurisdiction.lifecycleState == JurisdictionLifecycleState.Active
                || jurisdiction.lifecycleState == JurisdictionLifecycleState.Delegated
                || jurisdiction.lifecycleState == JurisdictionLifecycleState.Contested;
            return activeState
                && worldTime >= jurisdiction.effectiveWorldTime
                && (jurisdiction.expirationWorldTime < 0d || worldTime <= jurisdiction.expirationWorldTime);
        }

        private static bool AppliesToSubject(JurisdictionRecordData jurisdiction, JurisdictionSubjectMatter subject)
        {
            return jurisdiction.subjectMatters == null || jurisdiction.subjectMatters.Length == 0 || jurisdiction.subjectMatters.Contains(subject);
        }

        private static bool AppliesToScope(JurisdictionRecordData jurisdiction, JurisdictionResolutionRequest request)
        {
            return Matches(jurisdiction.territoryIds, request.territoryId)
                && Matches(jurisdiction.placeIds, request.placeId)
                && Matches(jurisdiction.personIds, request.personId)
                && Matches(jurisdiction.organizationIds, request.organizationId)
                && Matches(jurisdiction.propertyIds, request.propertyId)
                && Matches(jurisdiction.officeIds, request.officeId)
                && Matches(jurisdiction.statusIds, request.statusId);
        }

        private static bool Matches(IEnumerable<string> expectedIds, string actualId)
        {
            string[] expected = PoliticalModelUtility.Clean(expectedIds);
            return expected.Length == 0 || expected.Contains(PoliticalModelUtility.Normalize(actualId));
        }

        private static int ScopeSpecificity(JurisdictionRecordData jurisdiction)
        {
            int score = 0;
            if (jurisdiction.territoryIds != null) score += jurisdiction.territoryIds.Length;
            if (jurisdiction.placeIds != null) score += jurisdiction.placeIds.Length;
            if (jurisdiction.personIds != null) score += jurisdiction.personIds.Length * 2;
            if (jurisdiction.organizationIds != null) score += jurisdiction.organizationIds.Length * 2;
            if (jurisdiction.propertyIds != null) score += jurisdiction.propertyIds.Length * 2;
            if (jurisdiction.subjectMatters != null) score += jurisdiction.subjectMatters.Length;
            return score;
        }

        private static HashSet<string> IdSet(IEnumerable<string> ids, string label, out string failure)
        {
            failure = string.Empty;
            string[] clean = PoliticalModelUtility.Clean(ids);
            if (clean.Contains(string.Empty))
            {
                failure = $"Government save has an empty {label} ID.";
                return new HashSet<string>(StringComparer.Ordinal);
            }
            if (clean.Length != (ids ?? Array.Empty<string>()).Where(item => item != null).Select(PoliticalModelUtility.Normalize).Where(item => item.Length > 0).Count())
            {
                failure = $"Government save has duplicate {label} IDs.";
                return new HashSet<string>(StringComparer.Ordinal);
            }
            return new HashSet<string>(clean, StringComparer.Ordinal);
        }

        private static bool ValidateRefs(IEnumerable<string> ids, ISet<string> valid, bool allowEmpty, out string failure, string label)
        {
            failure = string.Empty;
            foreach (string id in PoliticalModelUtility.Clean(ids))
            {
                if (string.IsNullOrWhiteSpace(id) && allowEmpty) continue;
                if (!valid.Contains(id)) { failure = $"{label} references missing ID '{id}'."; return false; }
            }
            return true;
        }

        private static bool ValidatePlaces(IEnumerable<string> ids, ISet<string> knownPlaces, bool allowUnknownWhenNoCatalog, out string failure)
        {
            failure = string.Empty;
            foreach (string id in PoliticalModelUtility.Clean(ids))
            {
                if (knownPlaces.Count == 0 && allowUnknownWhenNoCatalog) continue;
                if (!knownPlaces.Contains(id)) { failure = $"Political record references missing Place '{id}'."; return false; }
            }
            return true;
        }

        private static bool ValidateDiplomacyReference(DiplomacyRuntime runtime, string id, string kind, out string failure)
        {
            failure = string.Empty;
            id = PoliticalModelUtility.Normalize(id);
            if (string.IsNullOrEmpty(id) || runtime == null) return true;
            bool exists = kind == "agreement" ? runtime.TryGetAgreement(id, out _) : runtime.TryGetRelation(id, out _);
            if (exists) return true;
            failure = $"Political record references missing diplomatic {kind} '{id}'.";
            return false;
        }

        private static bool HasCycle(Dictionary<string, string> parentById, out string failure, string label)
        {
            failure = string.Empty;
            if (parentById == null) return false;
            foreach (string id in parentById.Keys.Where(item => !string.IsNullOrEmpty(item)))
            {
                HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
                string cursor = id;
                while (!string.IsNullOrEmpty(cursor) && parentById.TryGetValue(cursor, out string parent))
                {
                    if (string.IsNullOrWhiteSpace(parent)) break;
                    if (!visited.Add(parent))
                    {
                        failure = $"Government save has a {label} cycle at '{id}'.";
                        return true;
                    }
                    cursor = parent;
                }
            }
            return false;
        }
    }

    public sealed class PolityCreateRequest
    {
        public string transactionId;
        public string polityId;
        public string polityDefinitionId;
        public string officialName;
        public PolityLifecycleState lifecycleState = PolityLifecycleState.Active;
        public string[] capitalPlaceIds = Array.Empty<string>();
        public string[] predecessorPolityIds = Array.Empty<string>();
        public double worldTime;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public bool preview;
    }

    public sealed class PolityRenameRequest
    {
        public string transactionId;
        public string polityId;
        public string nameRecordId;
        public string name;
        public PoliticalNameCategory category = PoliticalNameCategory.Common;
        public bool makeOfficial;
        public double worldTime;
        public string sourceId;
        public string recognitionContextId;
        public string provenanceId;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public bool preview;
    }

    public sealed class PolityTransitionRequest
    {
        public string transactionId;
        public string polityId;
        public PolityLifecycleState targetState = PolityLifecycleState.Active;
        public string[] successorPolityIds = Array.Empty<string>();
        public string sourceEventId;
        public double worldTime;
        public bool preview;
    }

    public sealed class GovernmentInstitutionRoleRequest
    {
        public string transactionId;
        public string roleId;
        public string governmentId;
        public string organizationId;
        public GovernmentInstitutionRoleCategory roleCategory = GovernmentInstitutionRoleCategory.Custom;
        public bool primary;
        public bool endRole;
        public double worldTime;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public bool preview;
    }

    public sealed class GovernmentRegisterRequest
    {
        public string transactionId;
        public string governmentId;
        public string governmentDefinitionId;
        public string polityId;
        public string officialName;
        public string primaryGoverningOrganizationId;
        public string[] governingOrganizationIds = Array.Empty<string>();
        public string parentGovernmentId;
        public GovernmentLevel level = GovernmentLevel.Unknown;
        public GovernmentLifecycleState lifecycleState = GovernmentLifecycleState.Active;
        public double worldTime;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public string sourceDiplomaticRecognitionId;
        public string sourceEventId;
        public string sourceRecordId;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string[] tags = Array.Empty<string>();
        public bool markAsCurrentGovernment = true;
        public bool markAsRecognizedPrimary = true;
        public bool preview;
    }

    public sealed class TerritoryCreateRequest
    {
        public string transactionId;
        public string territoryId;
        public string territoryDefinitionId;
        public string displayName;
        public string parentTerritoryId;
        public string polityId;
        public string primaryGovernmentId;
        public string[] placeIds = Array.Empty<string>();
        public TerritoryLifecycleState lifecycleState = TerritoryLifecycleState.Active;
        public double worldTime;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public bool preview;
    }

    public sealed class TerritoryTransitionRequest
    {
        public string transactionId;
        public string territoryId;
        public TerritoryLifecycleState targetState = TerritoryLifecycleState.Active;
        public string sourceEventId;
        public double worldTime;
        public bool preview;
    }

    public sealed class TerritoryPlaceMembershipRequest
    {
        public string transactionId;
        public string membershipId;
        public string territoryId;
        public string placeId;
        public TerritoryMembershipKind membershipKind = TerritoryMembershipKind.ContainsPlace;
        public bool endMembership;
        public double worldTime;
        public string sourceId;
        public bool preview;
    }

    public sealed class TerritorialClaimRequest
    {
        public string transactionId;
        public string claimId;
        public string claimDefinitionId;
        public string territoryId;
        public string claimantPolityId;
        public string claimantGovernmentId;
        public TerritorialClaimCategory category = TerritorialClaimCategory.Unknown;
        public TerritorialClaimLifecycleState lifecycleState = TerritorialClaimLifecycleState.Asserted;
        public string basisAgreementId;
        public string basisDiplomaticRelationId;
        public string recognitionRelationId;
        public string sourceDecisionId;
        public string[] disputedByGovernmentIds = Array.Empty<string>();
        public double worldTime;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string[] tags = Array.Empty<string>();
        public bool preview;
    }

    public sealed class TerritorialClaimTransitionRequest
    {
        public string transactionId;
        public string claimId;
        public TerritorialClaimLifecycleState targetState = TerritorialClaimLifecycleState.Asserted;
        public string[] disputedByGovernmentIds = Array.Empty<string>();
        public string recognitionRelationId;
        public string sourceDecisionId;
        public double worldTime;
        public bool preview;
    }

    public sealed class TerritorialControlRequest
    {
        public string transactionId;
        public string controlId;
        public string territoryId;
        public string controllingGovernmentId;
        public TerritorialControlState state = TerritorialControlState.Controlled;
        public string sourceWarId;
        public string sourceAgreementId;
        public string sourceDecisionId;
        public double worldTime;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public bool preview;
    }

    public sealed class TerritoryAdministrationRequest
    {
        public string transactionId;
        public string administrationId;
        public string territoryId;
        public string administeringGovernmentId;
        public string delegatedByGovernmentId;
        public AdministrationState state = AdministrationState.Administered;
        public string sourceAgreementId;
        public string sourceDecisionId;
        public double worldTime;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public bool preview;
    }

    public sealed class GovernmentSeatRequest
    {
        public string transactionId;
        public string seatId;
        public string governmentId;
        public string placeId;
        public SeatCategory category = SeatCategory.AdministrativeSeat;
        public bool primary;
        public double worldTime;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public bool preview;
    }

    public sealed class SovereigntyClaimRequest
    {
        public string transactionId;
        public string sovereigntyClaimId;
        public string polityId;
        public string governmentId;
        public string territoryId;
        public SovereigntyClaimCategory category = SovereigntyClaimCategory.FullSovereignty;
        public SovereigntyClaimState state = SovereigntyClaimState.Claimed;
        public string recognitionRelationId;
        public string sourceDecisionId;
        public double worldTime;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public bool preview;
    }

    public sealed class JurisdictionCreateRequest
    {
        public string transactionId;
        public string jurisdictionId;
        public string jurisdictionDefinitionId;
        public string governmentId;
        public string sourceJurisdictionId;
        public string parentJurisdictionId;
        public JurisdictionCategory category = JurisdictionCategory.Unknown;
        public JurisdictionScopeDimension scopeDimensions = JurisdictionScopeDimension.None;
        public JurisdictionSubjectMatter[] subjectMatters = Array.Empty<JurisdictionSubjectMatter>();
        public string[] territoryIds = Array.Empty<string>();
        public string[] placeIds = Array.Empty<string>();
        public string[] personIds = Array.Empty<string>();
        public string[] organizationIds = Array.Empty<string>();
        public string[] propertyIds = Array.Empty<string>();
        public string[] officeIds = Array.Empty<string>();
        public string[] statusIds = Array.Empty<string>();
        public JurisdictionLifecycleState lifecycleState = JurisdictionLifecycleState.Active;
        public JurisdictionConflictPolicy conflictPolicy = JurisdictionConflictPolicy.Unknown;
        public int priority;
        public bool exclusive;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public double worldTime;
        public double expirationWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public bool preview;
    }

    public sealed class JurisdictionTransitionRequest
    {
        public string transactionId;
        public string jurisdictionId;
        public JurisdictionLifecycleState targetState = JurisdictionLifecycleState.Active;
        public string sourceDecisionId;
        public double worldTime;
        public bool preview;
    }

    public sealed class GovernmentOrganizationCharterRequest
    {
        public string transactionId;
        public string charterId;
        public string charterDefinitionId;
        public string governmentId;
        public string organizationId;
        public string supervisingOrganizationId;
        public string legalInstrumentId;
        public string[] jurisdictionIds = Array.Empty<string>();
        public double worldTime;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public PoliticalVisibility? visibilityOverride;
        public string provenanceId;
        public bool preview;
    }

    public sealed class GovernmentOrganizationCharterTransitionRequest
    {
        public string transactionId;
        public string charterId;
        public GovernmentOrganizationCharterLifecycleState targetState = GovernmentOrganizationCharterLifecycleState.Active;
        public double worldTime;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public bool preview;
    }

    public sealed class GovernmentOrganizationCharterSynchronizationRequest
    {
        public string transactionId;
        public string charterId;
        public string charterDefinitionId;
        public string provenanceId;
        public bool preview;
    }

    public sealed class PoliticalTransitionPlanRequest
    {
        public string transactionId;
        public string transitionId;
        public PoliticalTransitionKind transitionKind = PoliticalTransitionKind.BoundaryChange;
        public string sourcePolityId;
        public string targetPolityId;
        public string sourceGovernmentId;
        public string targetGovernmentId;
        public string[] territoryIds = Array.Empty<string>();
        public string sourceAgreementId;
        public string sourceDecisionId;
        public string diagnostics;
        public double worldTime;
        public bool preview;
    }

    public sealed class PoliticalTimeEvaluationRequest
    {
        public string transactionId;
        public string boundaryId;
        public double worldTime;
        public bool preview;
    }

    public sealed class TerritorialTransferRequest
    {
        public string transactionId;
        public string transitionId;
        public string sourceGovernmentId;
        public string targetGovernmentId;
        public string[] territoryIds = Array.Empty<string>();
        public string sourceAgreementId;
        public string sourceDecisionId;
        public double worldTime;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public bool preview;
    }

    public sealed class GovernmentTransitionRequest
    {
        public string transactionId;
        public string governmentId;
        public GovernmentLifecycleState targetState = GovernmentLifecycleState.Active;
        public string sourceDecisionId;
        public double worldTime;
        public bool preview;
    }
}
