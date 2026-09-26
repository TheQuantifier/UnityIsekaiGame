using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityIsekaiGame.Crimes;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Justice;
using UnityIsekaiGame.Laws;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Organizations.Integration
{
    public sealed class Step13InstitutionalIntegrationFacade
    {
        private readonly DefinitionRegistry registry;
        private readonly string worldId;
        private readonly string[] knownPersonIds;
        private readonly string[] knownPlaceIds;
        private readonly OrganizationRuntime organizations;
        private readonly OrganizationMembershipRuntime memberships;
        private readonly OrganizationAuthorityRuntime authority;
        private readonly OrganizationResourceRuntime resources;
        private readonly OrganizationDecisionRuntime decisions;
        private readonly FactionRuntime factions;
        private readonly DiplomacyRuntime diplomacy;
        private readonly GovernmentRuntime governments;
        private readonly LegalRuntime laws;
        private readonly CrimeRuntime crimes;
        private readonly JusticeRuntime justice;
        private readonly EconomyRuntime economy;
        private readonly PropertyRuntime properties;
        private readonly BusinessRuntime businesses;
        private readonly ItemInstanceIdentityRuntime itemInstances;

        public Step13InstitutionalIntegrationFacade(
            DefinitionRegistry registry,
            string worldId,
            IEnumerable<string> knownPersonIds,
            IEnumerable<string> knownPlaceIds,
            OrganizationRuntime organizations,
            OrganizationMembershipRuntime memberships,
            OrganizationAuthorityRuntime authority,
            OrganizationResourceRuntime resources,
            OrganizationDecisionRuntime decisions,
            FactionRuntime factions,
            DiplomacyRuntime diplomacy,
            GovernmentRuntime governments,
            LegalRuntime laws,
            CrimeRuntime crimes,
            JusticeRuntime justice,
            EconomyRuntime economy = null,
            PropertyRuntime properties = null,
            BusinessRuntime businesses = null,
            ItemInstanceIdentityRuntime itemInstances = null)
        {
            this.registry = registry;
            this.worldId = Clean(worldId);
            this.knownPersonIds = Clean(knownPersonIds);
            this.knownPlaceIds = Clean(knownPlaceIds);
            this.organizations = organizations;
            this.memberships = memberships;
            this.authority = authority;
            this.resources = resources;
            this.decisions = decisions;
            this.factions = factions;
            this.diplomacy = diplomacy;
            this.governments = governments;
            this.laws = laws;
            this.crimes = crimes;
            this.justice = justice;
            this.economy = economy;
            this.properties = properties;
            this.businesses = businesses;
            this.itemInstances = itemInstances;
        }

        public IReadOnlyList<Step13OwnershipEntry> OwnershipMap => CreateOwnershipMap();
        public IReadOnlyList<Step13PersistenceDependencyEntry> PersistenceDependencies => CreatePersistenceDependencyGraph();

        public IReadOnlyList<Step13RuntimeSummary> CreateRuntimeSummaries()
        {
            LegalRuntimeSaveData legalSave = laws?.CreateSaveData();
            CrimeRuntimeSaveData crimeSave = crimes?.CreateSaveData();
            JusticeRuntimeSaveData justiceSave = justice?.CreateSaveData();

            return new[]
            {
                new Step13RuntimeSummary(nameof(OrganizationRuntime), OrganizationPersistenceParticipant.Key, organizations != null, organizations != null && organizations.CreateSaveData() != null, organizations?.Revision ?? 0L, organizations?.Count ?? 0),
                new Step13RuntimeSummary(nameof(OrganizationMembershipRuntime), OrganizationMembershipPersistenceParticipant.Key, memberships != null, memberships != null && memberships.CreateSaveData() != null, memberships?.Revision ?? 0L, memberships?.MembershipCount ?? 0, memberships?.OfficeCount ?? 0),
                new Step13RuntimeSummary(nameof(OrganizationAuthorityRuntime), OrganizationAuthorityPersistenceParticipant.Key, authority != null, authority != null && authority.CreateSaveData() != null, authority?.Revision ?? 0L, authority?.GrantCount ?? 0, authority?.ApprovalCount ?? 0),
                new Step13RuntimeSummary(nameof(OrganizationResourceRuntime), OrganizationResourcePersistenceParticipant.Key, resources != null, resources != null && resources.CreateSaveData() != null, resources?.Revision ?? 0L, resources?.TreasuryCount ?? 0, resources?.AccountCount ?? 0, resources?.BudgetCount ?? 0),
                new Step13RuntimeSummary(nameof(OrganizationDecisionRuntime), OrganizationDecisionPersistenceParticipant.Key, decisions != null, decisions != null && decisions.CreateSaveData() != null, decisions?.Revision ?? 0L, decisions?.GoalCount ?? 0, decisions?.ProposalCount ?? 0, decisions?.ResolutionCount ?? 0),
                new Step13RuntimeSummary(nameof(FactionRuntime), FactionPersistenceParticipant.Key, factions != null, factions != null && factions.CreateSaveData() != null, factions?.Revision ?? 0L, factions?.FactionCount ?? 0, factions?.AffiliationCount ?? 0, factions?.RoleAssignmentCount ?? 0),
                new Step13RuntimeSummary(nameof(DiplomacyRuntime), DiplomacyPersistenceParticipant.Key, diplomacy != null, diplomacy != null && diplomacy.CreateSaveData() != null, diplomacy?.Revision ?? 0L, diplomacy?.RelationCount ?? 0, diplomacy?.AgreementCount ?? 0, diplomacy?.WarCount ?? 0),
                new Step13RuntimeSummary(nameof(GovernmentRuntime), GovernmentPersistenceParticipant.Key, governments != null, governments != null && governments.CreateSaveData() != null, governments?.Revision ?? 0L, governments?.GovernmentCount ?? 0, governments?.TerritoryCount ?? 0, governments?.JurisdictionCount ?? 0),
                new Step13RuntimeSummary(nameof(LegalRuntime), LegalPersistenceParticipant.Key, laws != null, laws != null && legalSave != null, laws?.Revision ?? 0L, legalSave?.instruments?.Length ?? 0, legalSave?.provisions?.Length ?? 0, legalSave?.statuses?.Length ?? 0),
                new Step13RuntimeSummary(nameof(CrimeRuntime), CrimePersistenceParticipant.Key, crimes != null, crimes != null && crimeSave != null, crimes?.Revision ?? 0L, crimeSave?.incidents?.Length ?? 0, crimeSave?.warrants?.Length ?? 0, crimeSave?.wantedStatuses?.Length ?? 0),
                new Step13RuntimeSummary(nameof(JusticeRuntime), JusticePersistenceParticipant.Key, justice != null, justice != null && justiceSave != null, justice?.Revision ?? 0L, justiceSave?.courts?.Length ?? 0, justiceSave?.cases?.Length ?? 0, justiceSave?.judgments?.Length ?? 0)
            };
        }

        public Step13IntegrationValidationReport ValidateComplete()
        {
            Step13IntegrationValidationReport report = new Step13IntegrationValidationReport();
            Step13InstitutionalIntegrationValidator.ValidateOwnershipMap(OwnershipMap, report);
            Step13InstitutionalIntegrationValidator.ValidatePersistenceDependencies(PersistenceDependencies, report);
            Step13InstitutionalIntegrationValidator.ValidateSchedulerBudget(new Step13SchedulerBudget(), report);
            ValidateRuntimeReadiness(report);
            ValidateRuntimeSaveGraphs(report);
            return report;
        }

        public Step13ReadinessSnapshot CreateReadinessSnapshot()
        {
            Step13IntegrationValidationReport report = ValidateComplete();
            IReadOnlyList<Step13RuntimeSummary> runtimes = CreateRuntimeSummaries();
            Step13IntegrationHealthStatus status = string.IsNullOrWhiteSpace(worldId) || registry == null
                ? Step13IntegrationHealthStatus.Uninitialized
                : report.ErrorCount > 0
                    ? Step13IntegrationHealthStatus.Failed
                    : report.WarningCount > 0 || runtimes.Any(item => !item.Ready)
                        ? Step13IntegrationHealthStatus.Degraded
                        : Step13IntegrationHealthStatus.Ready;
            long revision = runtimes.Sum(item => item.Revision);
            string fingerprint = Fingerprint(runtimes.Select(item => $"{item.RuntimeName}:{item.Revision}:{item.PrimaryCount}:{item.SecondaryCount}:{item.TertiaryCount}")
                .Concat(OwnershipMap.Select(item => $"{item.DomainId}:{item.FeatureId}:{item.AuthoritativeRuntime}:{item.Derived}"))
                .Concat(PersistenceDependencies.Select(item => $"{item.ParticipantKey}:{string.Join(",", item.DependsOn)}"))
                .Concat(report.Diagnostics.Select(item => item.ToString())));
            return new Step13ReadinessSnapshot(status, runtimes, OwnershipMap, PersistenceDependencies, report.Diagnostics, worldId, revision, fingerprint);
        }

        public Step13ActionEvaluationResult EvaluateProtectedAction(Step13InstitutionalActionContext context)
        {
            List<Step13ActionGateResult> gates = new List<Step13ActionGateResult>();
            Step13InstitutionalActionContext request = context ?? new Step13InstitutionalActionContext(
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                null,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0d);

            bool identities = !string.IsNullOrWhiteSpace(request.ActingPersonId)
                && request.Target.IsValid
                && (string.IsNullOrWhiteSpace(request.Target.WorldId) || string.IsNullOrWhiteSpace(worldId) || string.Equals(request.Target.WorldId, worldId, StringComparison.Ordinal));
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Identity, identities, identities ? "identity-valid" : "identity-invalid", identities ? "Actor, target, and world scope are valid." : "Actor, target, or world scope is missing or incompatible.", request.Target.SourceRuntime, request.Target.StableId));

            bool authorityOk = false;
            string authorityCode = "authority-missing";
            string authorityMessage = "Protected Step 13 actions require verified institutional authority.";
            if (authority != null && !string.IsNullOrWhiteSpace(request.RepresentedOrganizationId) && !string.IsNullOrWhiteSpace(request.ActionDefinitionId))
            {
                OrganizationAuthorityScopeData scope = OrganizationAuthorityScopeData.ForOrganization(request.RepresentedOrganizationId);
                scope.placeId = request.PlaceId;
                scope.recordId = request.Target.StableId;
                scope.actionDefinitionId = request.ActionDefinitionId;
                OrganizationAuthorizationResult authorization = authority.EvaluateAuthorization(new OrganizationAuthorizationRequest
                {
                    actorPersonId = request.ActingPersonId,
                    organizationId = request.RepresentedOrganizationId,
                    actionDefinitionId = request.ActionDefinitionId,
                    scope = scope,
                    targetPersonId = request.Target.SubjectType == Step13InstitutionalSubjectType.Person ? request.Target.StableId : string.Empty,
                    targetRecordId = request.Target.StableId,
                    allowDelegatedAuthority = true,
                    worldTime = request.WorldTime
                });
                authorityOk = authorization.Succeeded;
                authorityCode = authorization.Status.ToString();
                authorityMessage = authorization.Message;
            }
            else if (authority != null && !string.IsNullOrWhiteSpace(request.AuthorityGrantId) && authority.TryGetGrant(request.AuthorityGrantId, out OrganizationAuthoritySnapshot grant))
            {
                authorityOk = grant.LifecycleState == OrganizationAuthorityGrantLifecycleState.Active
                    && string.Equals(grant.GranteePersonId, request.ActingPersonId, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(request.RepresentedOrganizationId) || string.Equals(grant.OrganizationId, request.RepresentedOrganizationId, StringComparison.Ordinal));
                authorityCode = authorityOk ? "active-authority-grant" : "authority-grant-mismatch";
                authorityMessage = authorityOk ? "The supplied active authority grant belongs to the actor and represented organization." : "The supplied authority grant is inactive or belongs to another actor or organization.";
            }
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Authority, authorityOk, authorityCode, authorityMessage, nameof(OrganizationAuthorityRuntime), request.AuthorityGrantId, authority?.Revision ?? 0L));

            bool jurisdictionRequired = !string.IsNullOrWhiteSpace(request.JurisdictionId)
                || !string.IsNullOrWhiteSpace(request.TerritoryId)
                || !string.IsNullOrWhiteSpace(request.PlaceId)
                || !string.IsNullOrWhiteSpace(request.RepresentedGovernmentId);
            bool jurisdictionOk = !jurisdictionRequired;
            string jurisdictionCode = jurisdictionOk ? "jurisdiction-not-required" : "jurisdiction-runtime-missing";
            string jurisdictionMessage = jurisdictionOk ? "This request has no jurisdictional scope." : "Government and jurisdiction runtime is required for this request.";
            if (jurisdictionRequired && governments != null)
            {
                if (!string.IsNullOrWhiteSpace(request.JurisdictionId))
                {
                    jurisdictionOk = governments.TryGetJurisdiction(request.JurisdictionId, out JurisdictionRecordData jurisdiction)
                        && (string.IsNullOrWhiteSpace(request.RepresentedGovernmentId) || string.Equals(jurisdiction.governmentId, request.RepresentedGovernmentId, StringComparison.Ordinal))
                        && (jurisdiction.territoryIds.Length == 0 || jurisdiction.territoryIds.Contains(request.TerritoryId, StringComparer.Ordinal))
                        && (jurisdiction.placeIds.Length == 0 || jurisdiction.placeIds.Contains(request.PlaceId, StringComparer.Ordinal));
                }
                else
                {
                    JurisdictionResolutionResult resolution = governments.ResolveJurisdiction(new JurisdictionResolutionRequest { requesterGovernmentId = request.RepresentedGovernmentId, territoryId = request.TerritoryId, placeId = request.PlaceId, organizationId = request.RepresentedOrganizationId, subjectMatter = JurisdictionSubjectMatter.GeneralAdministration, worldTime = request.WorldTime });
                    jurisdictionOk = resolution.Status == JurisdictionResolutionStatus.Applicable || resolution.Status == JurisdictionResolutionStatus.Shared;
                }
                jurisdictionCode = jurisdictionOk ? "jurisdiction-applicable" : "jurisdiction-mismatch";
                jurisdictionMessage = jurisdictionOk ? "An active jurisdiction applies to the requested government and location." : "No active jurisdiction applies to the requested government and location.";
            }
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Jurisdiction, jurisdictionOk, jurisdictionCode, jurisdictionMessage, nameof(GovernmentRuntime), FirstNonEmpty(request.JurisdictionId, request.TerritoryId, request.PlaceId), governments?.Revision ?? 0L));

            bool legalRequired = !string.IsNullOrWhiteSpace(request.LegalSubjectMatterId)
                || !string.IsNullOrWhiteSpace(request.SourceWarrantId)
                || request.Target.SubjectType == Step13InstitutionalSubjectType.Case
                || request.Target.SubjectType == Step13InstitutionalSubjectType.Warrant
                || request.Target.SubjectType == Step13InstitutionalSubjectType.Judgment
                || request.Target.SubjectType == Step13InstitutionalSubjectType.Sentence;
            bool legalOk = !legalRequired || laws != null;
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Legality, legalOk, legalOk ? "legal-available" : "legal-runtime-missing", legalOk ? "Legal permission can be evaluated or is not required by this request." : "Legal runtime is required for this request.", nameof(LegalRuntime), request.LegalSubjectMatterId, laws?.Revision ?? 0L));

            string domainRuntime = ResolveRuntimeForSubject(request.Target.SubjectType);
            bool domainOk = !string.IsNullOrWhiteSpace(domainRuntime);
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Domain, domainOk, domainOk ? "domain-owner-present" : "domain-owner-missing", domainOk ? "The target subject has an available authoritative runtime." : "No authoritative runtime is available for the target subject.", domainRuntime, request.Target.StableId));

            bool consentRequired = request.Target.SubjectType == Step13InstitutionalSubjectType.Person || request.Target.SubjectType == Step13InstitutionalSubjectType.Membership;
            bool consentOk = !consentRequired || !string.IsNullOrWhiteSpace(request.ProvenanceId) || request.Visibility == Step13ProjectionVisibility.Privileged;
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Consent, consentOk, consentOk ? "consent-satisfied" : "consent-context-missing", consentOk ? "Consent is satisfied or not required." : "Person or membership changes require explicit provenance or privileged context.", "InstitutionalActionContext", request.ProvenanceId));

            bool resourceRequired = request.Target.SubjectType == Step13InstitutionalSubjectType.Property
                || request.Target.SubjectType == Step13InstitutionalSubjectType.Business
                || request.Target.SubjectType == Step13InstitutionalSubjectType.Item
                || request.Target.SubjectType == Step13InstitutionalSubjectType.Inventory;
            bool resourceOk = !resourceRequired || resources != null;
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Resource, resourceOk, resourceOk ? "resource-available" : "resource-runtime-missing", resourceOk ? "Resource runtime is available or not required." : "Resource mutations require OrganizationResourceRuntime.", nameof(OrganizationResourceRuntime), request.Target.StableId, resources?.Revision ?? 0L));

            bool timingOk = request.WorldTime >= 0d;
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Timing, timingOk, timingOk ? "time-valid" : "time-invalid", timingOk ? "Action uses explicit authoritative world time." : "Action world time must be non-negative."));

            bool prepared = gates.All(item => item.Succeeded);
            gates.Add(new Step13ActionGateResult(Step13ActionGate.Prepared, prepared, prepared ? "prepared" : "blocked", prepared ? "All protected-action gates passed." : "One or more protected-action gates failed."));

            string fingerprint = Fingerprint(gates.Select(item => $"{item.Gate}:{item.Succeeded}:{item.Code}:{item.SourceRuntime}:{item.SourceRecordId}:{item.Revision}"));
            return new Step13ActionEvaluationResult(gates, fingerprint);
        }

        public Step13InstitutionalContextSnapshot CreateInstitutionalContextSnapshot(string requesterPersonId, string actorPersonId, string organizationId, string governmentId, string placeId, double worldTime, Step13InstitutionalContextOptions options = null)
        {
            Step13InstitutionalContextOptions resolved = (options ?? new Step13InstitutionalContextOptions()).Clone();
            List<Step13ContextRecordReference> records = new List<Step13ContextRecordReference>();
            List<string> diagnostics = new List<string>();
            bool truncated = false;
            HashSet<string> contextJurisdictionIds = new HashSet<string>((governments?.Jurisdictions ?? Array.Empty<JurisdictionRecordData>())
                .Where(item => (string.IsNullOrWhiteSpace(governmentId) || Matches(item.governmentId, governmentId))
                    && (item.placeIds.Length == 0 || Includes(item.placeIds, placeId))
                    && (item.territoryIds.Length == 0 || governments.Territories.Any(territory => item.territoryIds.Contains(territory.territoryId, StringComparer.Ordinal) && territory.placeIds.Contains(placeId, StringComparer.Ordinal))))
                .Select(item => item.jurisdictionId), StringComparer.Ordinal);

            OrganizationRuntimeSaveData organizationSave = organizations?.CreateSaveData();
            AddLimited(records, diagnostics, ref truncated, "organizations", resolved.MaxOrganizations,
                organizationSave?.records
                    .Where(item => Matches(item.organizationId, organizationId) || Includes(item.parentOrganizationIds, organizationId) || Includes(item.operatingAreaPlaceIds, placeId))
                    .Select(item => Reference(nameof(OrganizationRuntime), item.organizationId, Step13InstitutionalProjectionState.Authoritative, MapVisibility(item.visibility), item.organizationDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "memberships", resolved.MaxMemberships,
                memberships?.CreateSaveData()?.memberships
                    .Where(item => Matches(item.organizationId, organizationId) || Matches(item.personId, actorPersonId))
                    .Select(item => Reference(nameof(OrganizationMembershipRuntime), item.membershipId, Step13InstitutionalProjectionState.Authoritative, Step13ProjectionVisibility.Participant, item.membershipDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "authority", resolved.MaxAuthority,
                authority?.CreateSaveData()?.grants
                    .Where(item => Matches(item.organizationId, organizationId) || Matches(item.granteePersonId, actorPersonId))
                    .Select(item => Reference(nameof(OrganizationAuthorityRuntime), item.grantId, Step13InstitutionalProjectionState.Authoritative, Step13ProjectionVisibility.Official, item.authorityRoleDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "resources", resolved.MaxResources,
                resources?.CreateSaveData()?.treasuries
                    .Where(item => Matches(item.organizationId, organizationId))
                    .Select(item => Reference(nameof(OrganizationResourceRuntime), item.treasuryId, Step13InstitutionalProjectionState.Authoritative, Step13ProjectionVisibility.Official, item.resourceTypeDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "decisions", resolved.MaxDecisions,
                decisions?.CreateSaveData()?.proposals
                    .Where(item => Matches(item.organizationId, organizationId))
                    .Select(item => Reference(nameof(OrganizationDecisionRuntime), item.proposalId, Step13InstitutionalProjectionState.Authoritative, MapVisibility(item.visibility), item.proposalDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "factions", resolved.MaxFactions,
                factions?.CreateSaveData()?.factions
                    .Where(item => Matches(item.hostContext?.primaryOrganizationId, organizationId) || Includes(item.hostContext?.organizationIds, organizationId))
                    .Select(item => Reference(nameof(FactionRuntime), item.factionId, Step13InstitutionalProjectionState.Authoritative, MapVisibility(item.visibility), item.factionDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "diplomacy", resolved.MaxDiplomacy,
                diplomacy?.CreateSaveData()?.relations
                    .Where(item => IncludesDiplomaticActor(item.sourceActor, organizationId) || IncludesDiplomaticActor(item.targetActor, organizationId))
                    .Select(item => Reference(nameof(DiplomacyRuntime), item.relationId, Step13InstitutionalProjectionState.Authoritative, MapVisibility(item.visibility), item.relationDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "governments", resolved.MaxGovernments,
                governments?.CreateSaveData()?.governments
                    .Where(item => Matches(item.governmentId, governmentId) || Includes(item.governingOrganizationIds, organizationId))
                    .Select(item => Reference(nameof(GovernmentRuntime), item.governmentId, Step13InstitutionalProjectionState.Authoritative, MapVisibility(item.visibility), item.governmentDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "laws", resolved.MaxLaws,
                laws?.CreateSaveData()?.instruments
                    .Where(item => Matches(item.governmentId, governmentId) || Matches(item.organizationId, organizationId) || item.jurisdictionIds.Any(contextJurisdictionIds.Contains))
                    .Select(item => Reference(nameof(LegalRuntime), item.instrumentId, Step13InstitutionalProjectionState.Authoritative, MapVisibility(item.visibility), item.instrumentDefinitionId)));

            AddLimited(records, diagnostics, ref truncated, "crimes", resolved.MaxCrimes,
                crimes?.CreateSaveData()?.incidents
                    .Where(item => Matches(item.primaryPlaceId, placeId) || item.jurisdictionIds.Any(contextJurisdictionIds.Contains))
                    .Select(item => Reference(nameof(CrimeRuntime), item.incidentId, Step13InstitutionalProjectionState.Authoritative, MapVisibility(item.visibility), item.category.ToString())));

            AddLimited(records, diagnostics, ref truncated, "justice", resolved.MaxJustice,
                justice?.CreateSaveData()?.cases
                    .Where(item => !string.IsNullOrWhiteSpace(item.caseId))
                    .Select(item => Reference(nameof(JusticeRuntime), item.caseId, Step13InstitutionalProjectionState.Authoritative, MapVisibility(item.visibility), item.category.ToString())));

            bool participant = resolved.Privileged || string.Equals(requesterPersonId, actorPersonId, StringComparison.Ordinal) || memberships != null && memberships.QueryMemberships(requesterPersonId, organizationId, activeOnly: true).Any();
            int hidden = records.RemoveAll(item => !CanProject(item.Visibility, resolved.Privileged, participant));
            if (hidden > 0) diagnostics.Add($"privacy filtered {hidden} institutional record reference(s)");

            string fingerprint = Fingerprint(records.Select(item => $"{item.RuntimeName}:{item.RecordId}:{item.ProjectionState}:{item.Visibility}:{item.Summary}")
                .Concat(CreateRuntimeSummaries().Select(item => $"{item.RuntimeName}:{item.Revision}:{item.PrimaryCount}:{item.SecondaryCount}:{item.TertiaryCount}")));
            return new Step13InstitutionalContextSnapshot(requesterPersonId, actorPersonId, organizationId, governmentId, placeId, worldTime, records, CreateRuntimeSummaries(), diagnostics, truncated, fingerprint);
        }

        private void ValidateRuntimeReadiness(Step13IntegrationValidationReport report)
        {
            if (registry == null)
            {
                report.AddError(Step13IntegrationDiagnosticDomain.DefinitionCatalog, "missing-registry", "Step 13 integration requires a DefinitionRegistry.");
            }

            if (string.IsNullOrWhiteSpace(worldId))
            {
                report.AddError(Step13IntegrationDiagnosticDomain.RuntimeReadiness, "missing-world", "Step 13 integration requires an explicit world ID.");
            }

            foreach (Step13RuntimeSummary summary in CreateRuntimeSummaries())
            {
                if (!summary.Present)
                {
                    report.AddError(Step13IntegrationDiagnosticDomain.RuntimeReadiness, "missing-runtime", $"{summary.RuntimeName} is not present.", summary.RuntimeName);
                }
            }

            if (economy == null)
            {
                report.AddWarning(Step13IntegrationDiagnosticDomain.RuntimeGraph, "missing-economy", "Financial Step 13 resource workflows are unavailable without EconomyRuntime.", nameof(EconomyRuntime));
            }

            if (properties == null)
            {
                report.AddWarning(Step13IntegrationDiagnosticDomain.RuntimeGraph, "missing-property", "Property, territory, government, and legal workflows are degraded without PropertyRuntime.", nameof(PropertyRuntime));
            }
        }

        private void ValidateRuntimeSaveGraphs(Step13IntegrationValidationReport report)
        {
            if (registry == null)
            {
                return;
            }

            if (organizations != null && !OrganizationRuntime.ValidateSaveData(organizations.CreateSaveData(), registry, worldId, knownPersonIds, knownPlaceIds, out string organizationFailure))
            {
                ValidateSave(report, OrganizationPersistenceParticipant.Key, organizationFailure);
            }

            if (memberships != null && !OrganizationMembershipRuntime.ValidateSaveData(memberships.CreateSaveData(), registry, organizations, worldId, knownPersonIds, OrganizationIds(), out string membershipFailure))
            {
                ValidateSave(report, OrganizationMembershipPersistenceParticipant.Key, membershipFailure);
            }

            if (authority != null && !OrganizationAuthorityRuntime.ValidateSaveData(authority.CreateSaveData(), registry, organizations, memberships, worldId, knownPersonIds, OrganizationIds(), out string authorityFailure))
            {
                ValidateSave(report, OrganizationAuthorityPersistenceParticipant.Key, authorityFailure);
            }

            if (resources != null && economy != null && !OrganizationResourceRuntime.ValidateSaveData(resources.CreateSaveData(), registry, organizations, economy, worldId, properties, businesses, itemInstances, out string resourceFailure))
            {
                ValidateSave(report, OrganizationResourcePersistenceParticipant.Key, resourceFailure);
            }

            if (decisions != null && !OrganizationDecisionRuntime.ValidateSaveData(decisions.CreateSaveData(), registry, organizations, memberships, authority, resources, worldId, knownPersonIds, out string decisionFailure))
            {
                ValidateSave(report, OrganizationDecisionPersistenceParticipant.Key, decisionFailure);
            }

            if (factions != null && !FactionRuntime.ValidateSaveData(factions.CreateSaveData(), registry, organizations, memberships, worldId, knownPersonIds, out string factionFailure))
            {
                ValidateSave(report, FactionPersistenceParticipant.Key, factionFailure);
            }

            if (diplomacy != null && !DiplomacyRuntime.ValidateSaveData(diplomacy.CreateSaveData(), registry, organizations, factions, worldId, knownPersonIds, out string diplomacyFailure))
            {
                ValidateSave(report, DiplomacyPersistenceParticipant.Key, diplomacyFailure);
            }

            if (governments != null && properties != null && !GovernmentRuntime.ValidateSaveData(governments.CreateSaveData(), registry, organizations, factions, diplomacy, properties, worldId, knownPersonIds, knownPlaceIds, out string governmentFailure))
            {
                ValidateSave(report, GovernmentPersistenceParticipant.Key, governmentFailure);
            }

            if (laws != null && properties != null && !LegalRuntime.ValidateSaveData(laws.CreateSaveData(), registry, governments, organizations, authority, decisions, diplomacy, properties, worldId, knownPersonIds, knownPlaceIds, out string legalFailure))
            {
                ValidateSave(report, LegalPersistenceParticipant.Key, legalFailure);
            }

            if (crimes != null && !CrimeRuntime.ValidateSaveData(crimes.CreateSaveData(), registry, governments, laws, authority, diplomacy, worldId, knownPersonIds, knownPlaceIds, out string crimeFailure))
            {
                ValidateSave(report, CrimePersistenceParticipant.Key, crimeFailure);
            }

            if (justice != null && !JusticeRuntime.ValidateSaveData(justice.CreateSaveData(), registry, governments, laws, organizations, authority, crimes, worldId, knownPersonIds, knownPlaceIds, out string justiceFailure))
            {
                ValidateSave(report, JusticePersistenceParticipant.Key, justiceFailure);
            }
        }

        private string[] OrganizationIds()
        {
            return organizations?.CreateSaveData()?.records?.Select(item => item.organizationId).ToArray() ?? Array.Empty<string>();
        }

        private static void ValidateSave(Step13IntegrationValidationReport report, string participantKey, string failure)
        {
            report.AddError(Step13IntegrationDiagnosticDomain.Persistence, "invalid-save-graph", failure, participantKey);
        }

        public static IReadOnlyList<Step13OwnershipEntry> CreateOwnershipMap()
        {
            return new[]
            {
                new Step13OwnershipEntry("organization-identity", "13.1", "Organization Identity", nameof(OrganizationRuntime), false, nameof(OrganizationMembershipRuntime), nameof(OrganizationAuthorityRuntime), nameof(FactionRuntime), nameof(GovernmentRuntime)),
                new Step13OwnershipEntry("organization-hierarchy", "13.1", "Organization Hierarchy", nameof(OrganizationRuntime), false, nameof(OrganizationMembershipRuntime), nameof(DiplomacyRuntime)),
                new Step13OwnershipEntry("membership", "13.2", "Membership", nameof(OrganizationMembershipRuntime), false, nameof(OrganizationAuthorityRuntime), nameof(FactionRuntime)),
                new Step13OwnershipEntry("rank-assignment", "13.2", "Rank Assignment", nameof(OrganizationMembershipRuntime), false, nameof(OrganizationAuthorityRuntime)),
                new Step13OwnershipEntry("office-identity", "13.2", "Office Identity", nameof(OrganizationMembershipRuntime), false, nameof(OrganizationAuthorityRuntime), nameof(JusticeRuntime)),
                new Step13OwnershipEntry("office-assignment", "13.2", "Office Assignment", nameof(OrganizationMembershipRuntime), false, nameof(OrganizationAuthorityRuntime)),
                new Step13OwnershipEntry("institutional-authority", "13.3", "Institutional Authority", nameof(OrganizationAuthorityRuntime), false, nameof(OrganizationDecisionRuntime), nameof(LegalRuntime), nameof(CrimeRuntime), nameof(JusticeRuntime)),
                new Step13OwnershipEntry("direct-grant", "13.3", "Direct Grant", nameof(OrganizationAuthorityRuntime), false, nameof(OrganizationResourceRuntime)),
                new Step13OwnershipEntry("delegation", "13.3", "Delegation", nameof(OrganizationAuthorityRuntime), false),
                new Step13OwnershipEntry("treasury", "13.4", "Treasury Metadata", nameof(OrganizationResourceRuntime), false, nameof(EconomyRuntime)),
                new Step13OwnershipEntry("currency-balance", "11.1", "Currency Balance", nameof(EconomyRuntime), true, nameof(OrganizationResourceRuntime)),
                new Step13OwnershipEntry("inventory-association", "13.4", "Inventory Association", nameof(OrganizationResourceRuntime), false, "InventoryRuntime", nameof(ItemInstanceIdentityRuntime)),
                new Step13OwnershipEntry("property-ownership", "11.5", "Property Ownership", nameof(PropertyRuntime), true, nameof(OrganizationResourceRuntime), nameof(GovernmentRuntime), nameof(LegalRuntime)),
                new Step13OwnershipEntry("organization-property-association", "13.4", "Organization Property Association", nameof(OrganizationResourceRuntime), false, nameof(PropertyRuntime)),
                new Step13OwnershipEntry("budget", "13.4", "Budget", nameof(OrganizationResourceRuntime), false, nameof(OrganizationDecisionRuntime)),
                new Step13OwnershipEntry("policy", "13.5", "Organizational Policy", nameof(OrganizationDecisionRuntime), false, nameof(FactionRuntime), nameof(LegalRuntime)),
                new Step13OwnershipEntry("proposal-vote", "13.5", "Proposals and Votes", nameof(OrganizationDecisionRuntime), false, nameof(FactionRuntime)),
                new Step13OwnershipEntry("faction-identity", "13.6", "Faction Identity", nameof(FactionRuntime), false, nameof(DiplomacyRuntime)),
                new Step13OwnershipEntry("faction-affiliation", "13.6", "Faction Affiliation", nameof(FactionRuntime), false, nameof(OrganizationMembershipRuntime)),
                new Step13OwnershipEntry("diplomacy", "13.7", "Diplomacy", nameof(DiplomacyRuntime), false, nameof(GovernmentRuntime), nameof(LegalRuntime)),
                new Step13OwnershipEntry("treaty", "13.7", "Treaty and Agreement", nameof(DiplomacyRuntime), false, nameof(LegalRuntime)),
                new Step13OwnershipEntry("war-status", "13.7", "War Status", nameof(DiplomacyRuntime), false, nameof(GovernmentRuntime)),
                new Step13OwnershipEntry("polity-identity", "13.8", "Polity Identity", nameof(GovernmentRuntime), false, nameof(LegalRuntime)),
                new Step13OwnershipEntry("government-identity", "13.8", "Government Identity", nameof(GovernmentRuntime), false, nameof(LegalRuntime), nameof(CrimeRuntime), nameof(JusticeRuntime)),
                new Step13OwnershipEntry("territory", "13.8", "Territory", nameof(GovernmentRuntime), false, nameof(LegalRuntime), nameof(CrimeRuntime)),
                new Step13OwnershipEntry("claim", "13.8", "Territorial Claim", nameof(GovernmentRuntime), false),
                new Step13OwnershipEntry("territorial-control", "13.8", "Territorial Control", nameof(GovernmentRuntime), false, nameof(DiplomacyRuntime)),
                new Step13OwnershipEntry("administration", "13.8", "Territory Administration", nameof(GovernmentRuntime), false, nameof(LegalRuntime)),
                new Step13OwnershipEntry("jurisdiction", "13.8", "Jurisdiction", nameof(GovernmentRuntime), false, nameof(LegalRuntime), nameof(CrimeRuntime), nameof(JusticeRuntime)),
                new Step13OwnershipEntry("law", "13.9", "Legal Instrument", nameof(LegalRuntime), false, nameof(CrimeRuntime), nameof(JusticeRuntime)),
                new Step13OwnershipEntry("legal-provision", "13.9", "Legal Provision", nameof(LegalRuntime), false, nameof(CrimeRuntime), nameof(JusticeRuntime)),
                new Step13OwnershipEntry("citizenship", "13.9", "Citizenship", nameof(LegalRuntime), false, nameof(GovernmentRuntime)),
                new Step13OwnershipEntry("legal-status", "13.9", "Legal Status", nameof(LegalRuntime), false, nameof(JusticeRuntime)),
                new Step13OwnershipEntry("incident", "13.10", "Crime Incident", nameof(CrimeRuntime), false, nameof(JusticeRuntime)),
                new Step13OwnershipEntry("report", "13.10", "Crime Report", nameof(CrimeRuntime), false),
                new Step13OwnershipEntry("suspect", "13.10", "Suspect", nameof(CrimeRuntime), false, nameof(JusticeRuntime)),
                new Step13OwnershipEntry("evidence-link", "13.10", "Evidence Link", nameof(CrimeRuntime), false, "KnowledgeRecordRuntime", nameof(JusticeRuntime)),
                new Step13OwnershipEntry("warrant", "13.10", "Warrant", nameof(CrimeRuntime), false, nameof(JusticeRuntime)),
                new Step13OwnershipEntry("wanted-status", "13.10", "Wanted Status", nameof(CrimeRuntime), false),
                new Step13OwnershipEntry("court", "13.11", "Court", nameof(JusticeRuntime), false, nameof(GovernmentRuntime)),
                new Step13OwnershipEntry("arrest", "13.11", "Arrest", nameof(JusticeRuntime), false, nameof(CrimeRuntime)),
                new Step13OwnershipEntry("custody", "13.11", "Custody", nameof(JusticeRuntime), false),
                new Step13OwnershipEntry("charge", "13.11", "Charge", nameof(JusticeRuntime), false, nameof(CrimeRuntime), nameof(LegalRuntime)),
                new Step13OwnershipEntry("case", "13.11", "Court Case", nameof(JusticeRuntime), false),
                new Step13OwnershipEntry("judgment", "13.11", "Judgment", nameof(JusticeRuntime), false),
                new Step13OwnershipEntry("sentence", "13.11", "Sentence", nameof(JusticeRuntime), false),
                new Step13OwnershipEntry("remedy", "13.11", "Remedy", nameof(JusticeRuntime), false, nameof(EconomyRuntime), nameof(PropertyRuntime), nameof(OrganizationMembershipRuntime)),
                new Step13OwnershipEntry("appeal", "13.11", "Appeal", nameof(JusticeRuntime), false),
                new Step13OwnershipEntry("clemency", "13.11", "Clemency", nameof(JusticeRuntime), false),
                new Step13OwnershipEntry("historical-event", "8.3", "Historical Event", "AuthoritativeHistoryRuntime", true),
                new Step13OwnershipEntry("social-reputation", "12.3", "Social Reputation", "ReputationRuntime", true),
                new Step13OwnershipEntry("knowledge-visibility", "8.8", "Knowledge and Visibility", "InformationAccessRuntime", true),
                new Step13OwnershipEntry("financial-transaction", "11.1", "Financial Transaction", nameof(EconomyRuntime), true),
                new Step13OwnershipEntry("item-state", "9.1", "Item Identity and State", nameof(ItemInstanceIdentityRuntime), true)
            };
        }

        public static IReadOnlyList<Step13PersistenceDependencyEntry> CreatePersistenceDependencyGraph()
        {
            return new[]
            {
                new Step13PersistenceDependencyEntry(OrganizationPersistenceParticipant.Key),
                new Step13PersistenceDependencyEntry(OrganizationMembershipPersistenceParticipant.Key, OrganizationPersistenceParticipant.Key),
                new Step13PersistenceDependencyEntry(OrganizationAuthorityPersistenceParticipant.Key, OrganizationPersistenceParticipant.Key, OrganizationMembershipPersistenceParticipant.Key),
                new Step13PersistenceDependencyEntry(OrganizationResourcePersistenceParticipant.Key, OrganizationPersistenceParticipant.Key, OrganizationAuthorityPersistenceParticipant.Key, "world.economy", "world.properties", "world.businesses", "world.item-instances"),
                new Step13PersistenceDependencyEntry(OrganizationDecisionPersistenceParticipant.Key, OrganizationPersistenceParticipant.Key, OrganizationMembershipPersistenceParticipant.Key, OrganizationAuthorityPersistenceParticipant.Key, OrganizationResourcePersistenceParticipant.Key),
                new Step13PersistenceDependencyEntry(FactionPersistenceParticipant.Key, OrganizationPersistenceParticipant.Key, OrganizationMembershipPersistenceParticipant.Key, OrganizationDecisionPersistenceParticipant.Key),
                new Step13PersistenceDependencyEntry(DiplomacyPersistenceParticipant.Key, OrganizationPersistenceParticipant.Key, FactionPersistenceParticipant.Key, OrganizationAuthorityPersistenceParticipant.Key, OrganizationDecisionPersistenceParticipant.Key),
                new Step13PersistenceDependencyEntry(GovernmentPersistenceParticipant.Key, OrganizationPersistenceParticipant.Key, FactionPersistenceParticipant.Key, DiplomacyPersistenceParticipant.Key, "world.properties"),
                new Step13PersistenceDependencyEntry(LegalPersistenceParticipant.Key, GovernmentPersistenceParticipant.Key, OrganizationPersistenceParticipant.Key, OrganizationAuthorityPersistenceParticipant.Key, OrganizationDecisionPersistenceParticipant.Key, DiplomacyPersistenceParticipant.Key, "world.properties"),
                new Step13PersistenceDependencyEntry(CrimePersistenceParticipant.Key, GovernmentPersistenceParticipant.Key, LegalPersistenceParticipant.Key, OrganizationAuthorityPersistenceParticipant.Key, DiplomacyPersistenceParticipant.Key),
                new Step13PersistenceDependencyEntry(JusticePersistenceParticipant.Key, CrimePersistenceParticipant.Key, LegalPersistenceParticipant.Key, GovernmentPersistenceParticipant.Key, OrganizationPersistenceParticipant.Key, OrganizationAuthorityPersistenceParticipant.Key)
            };
        }

        private static Step13ContextRecordReference Reference(string runtimeName, string recordId, Step13InstitutionalProjectionState projectionState, Step13ProjectionVisibility visibility, string summary)
        {
            return new Step13ContextRecordReference(runtimeName, recordId, projectionState, visibility, summary);
        }

        private static bool CanProject(Step13ProjectionVisibility visibility, bool privileged, bool participant)
        {
            if (privileged) return true;
            return visibility switch
            {
                Step13ProjectionVisibility.Public => true,
                Step13ProjectionVisibility.KnowledgeSafe => true,
                Step13ProjectionVisibility.Participant => participant,
                Step13ProjectionVisibility.Official => participant,
                _ => false
            };
        }

        private static void AddLimited(List<Step13ContextRecordReference> records, List<string> diagnostics, ref bool truncated, string label, int limit, IEnumerable<Step13ContextRecordReference> candidates)
        {
            if (limit <= 0 || candidates == null)
            {
                return;
            }

            Step13ContextRecordReference[] ordered = candidates
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.RecordId))
                .OrderBy(item => item.RuntimeName, StringComparer.Ordinal)
                .ThenBy(item => item.RecordId, StringComparer.Ordinal)
                .ToArray();
            records.AddRange(ordered.Take(limit));
            if (ordered.Length > limit)
            {
                truncated = true;
                diagnostics.Add($"{label} truncated {ordered.Length}->{limit}");
            }
        }

        private string ResolveRuntimeForSubject(Step13InstitutionalSubjectType subjectType)
        {
            return subjectType switch
            {
                Step13InstitutionalSubjectType.Person => knownPersonIds.Length > 0 ? "PersonIdentity" : string.Empty,
                Step13InstitutionalSubjectType.Organization => organizations != null ? nameof(OrganizationRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Faction => factions != null ? nameof(FactionRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Polity => governments != null ? nameof(GovernmentRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Government => governments != null ? nameof(GovernmentRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Territory => governments != null ? nameof(GovernmentRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Place => knownPlaceIds.Length > 0 ? "PlaceIdentity" : governments != null ? nameof(GovernmentRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Property => resources != null ? nameof(OrganizationResourceRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Business => resources != null ? nameof(OrganizationResourceRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Office => memberships != null ? nameof(OrganizationMembershipRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Membership => memberships != null ? nameof(OrganizationMembershipRuntime) : string.Empty,
                Step13InstitutionalSubjectType.RankAssignment => memberships != null ? nameof(OrganizationMembershipRuntime) : string.Empty,
                Step13InstitutionalSubjectType.LegalInstrument => laws != null ? nameof(LegalRuntime) : string.Empty,
                Step13InstitutionalSubjectType.LegalProvision => laws != null ? nameof(LegalRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Incident => crimes != null ? nameof(CrimeRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Warrant => crimes != null ? nameof(CrimeRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Court => justice != null ? nameof(JusticeRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Case => justice != null ? nameof(JusticeRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Judgment => justice != null ? nameof(JusticeRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Sentence => justice != null ? nameof(JusticeRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Item => itemInstances != null ? nameof(ItemInstanceIdentityRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Inventory => resources != null ? nameof(OrganizationResourceRuntime) : string.Empty,
                Step13InstitutionalSubjectType.Contract => "ContractEconomyRuntime",
                Step13InstitutionalSubjectType.HistoricalEvent => "AuthoritativeHistoryRuntime",
                _ => string.Empty
            };
        }

        private static bool Matches(string value, string target)
        {
            return !string.IsNullOrWhiteSpace(target) && string.Equals(value, target, StringComparison.Ordinal);
        }

        private static bool Includes(IEnumerable<string> values, string target)
        {
            return !string.IsNullOrWhiteSpace(target) && (values ?? Array.Empty<string>()).Any(value => string.Equals(value, target, StringComparison.Ordinal));
        }

        private static bool IncludesDiplomaticActor(DiplomaticActorReferenceData actor, string organizationId)
        {
            return actor != null && !string.IsNullOrWhiteSpace(organizationId) && string.Equals(actor.actorId, organizationId, StringComparison.Ordinal);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return (values ?? Array.Empty<string>()).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static Step13ProjectionVisibility MapVisibility(PoliticalVisibility visibility)
        {
            return visibility switch
            {
                PoliticalVisibility.Public => Step13ProjectionVisibility.Public,
                PoliticalVisibility.Confidential => Step13ProjectionVisibility.Participant,
                PoliticalVisibility.Restricted => Step13ProjectionVisibility.KnowledgeSafe,
                PoliticalVisibility.Secret => Step13ProjectionVisibility.Concealed,
                _ => Step13ProjectionVisibility.Redacted
            };
        }

        private static Step13ProjectionVisibility MapVisibility(OrganizationVisibility visibility)
        {
            return visibility switch
            {
                OrganizationVisibility.Public => Step13ProjectionVisibility.Public,
                OrganizationVisibility.Restricted => Step13ProjectionVisibility.KnowledgeSafe,
                OrganizationVisibility.Secret => Step13ProjectionVisibility.Concealed,
                OrganizationVisibility.Hidden => Step13ProjectionVisibility.Concealed,
                _ => Step13ProjectionVisibility.Redacted
            };
        }

        private static Step13ProjectionVisibility MapVisibility(FactionVisibility visibility)
        {
            return visibility switch
            {
                FactionVisibility.Public => Step13ProjectionVisibility.Public,
                FactionVisibility.Secret => Step13ProjectionVisibility.Concealed,
                FactionVisibility.Hidden => Step13ProjectionVisibility.Concealed,
                _ => Step13ProjectionVisibility.Redacted
            };
        }

        private static Step13ProjectionVisibility MapVisibility(DiplomaticVisibility visibility)
        {
            return visibility switch
            {
                DiplomaticVisibility.Public => Step13ProjectionVisibility.Public,
                DiplomaticVisibility.Confidential => Step13ProjectionVisibility.KnowledgeSafe,
                DiplomaticVisibility.Restricted => Step13ProjectionVisibility.KnowledgeSafe,
                DiplomaticVisibility.Secret => Step13ProjectionVisibility.Concealed,
                DiplomaticVisibility.Hidden => Step13ProjectionVisibility.Concealed,
                _ => Step13ProjectionVisibility.Redacted
            };
        }

        private static string Fingerprint(IEnumerable<string> parts)
        {
            string joined = string.Join("\n", parts ?? Array.Empty<string>());
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
            return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
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

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step13InstitutionalTransactionCoordinator
    {
        private readonly HashSet<string> completedTransactionIds = new HashSet<string>(StringComparer.Ordinal);

        public Step13TransactionResult Execute(string transactionId, IEnumerable<Step13TransactionParticipantPlan> participants, bool preview = false)
        {
            string tx = Clean(transactionId);
            List<Step13TransactionParticipantResult> results = new List<Step13TransactionParticipantResult>();
            List<string> diagnostics = new List<string>();

            if (string.IsNullOrWhiteSpace(tx))
            {
                return new Step13TransactionResult(false, tx, preview, false, results, new[] { "Transaction ID is required." });
            }

            if (!preview && completedTransactionIds.Contains(tx))
            {
                return new Step13TransactionResult(true, tx, false, true, results, new[] { "Duplicate transaction ignored." });
            }

            Step13TransactionParticipantPlan[] ordered = (participants ?? Array.Empty<Step13TransactionParticipantPlan>())
                .Where(item => item != null)
                .OrderBy(item => item.RuntimeName, StringComparer.Ordinal)
                .ToArray();
            if (ordered.Length == 0)
            {
                return new Step13TransactionResult(false, tx, preview, false, results, new[] { "At least one participant is required." });
            }

            Step13TransactionStage firstStage = preview ? Step13TransactionStage.Preview : Step13TransactionStage.Prepare;
            if (!RunStage(ordered, firstStage, results, diagnostics, out _))
            {
                return new Step13TransactionResult(false, tx, preview, false, results, diagnostics);
            }

            if (preview)
            {
                return new Step13TransactionResult(true, tx, true, false, results, diagnostics);
            }

            if (!RunStage(ordered, Step13TransactionStage.Commit, results, diagnostics, out _))
            {
                Rollback(ordered, results);
                return new Step13TransactionResult(false, tx, false, false, results, diagnostics);
            }

            RunStage(ordered, Step13TransactionStage.PostCommit, results, diagnostics, out _, failRequired: false);
            completedTransactionIds.Add(tx);
            return new Step13TransactionResult(true, tx, false, false, results, diagnostics);
        }

        private static bool RunStage(IReadOnlyList<Step13TransactionParticipantPlan> participants, Step13TransactionStage stage, List<Step13TransactionParticipantResult> results, List<string> diagnostics, out Step13TransactionParticipantPlan failed, bool failRequired = true)
        {
            failed = null;
            foreach (Step13TransactionParticipantPlan participant in participants)
            {
                Func<bool> action = stage switch
                {
                    Step13TransactionStage.Preview => participant.Preview,
                    Step13TransactionStage.Prepare => participant.Prepare,
                    Step13TransactionStage.Commit => participant.Commit,
                    Step13TransactionStage.PostCommit => participant.PostCommit,
                    _ => null
                };

                bool succeeded = action == null || action();
                results.Add(new Step13TransactionParticipantResult(participant.RuntimeName, stage, succeeded, participant.FailurePolicy));
                if (!succeeded && failRequired && participant.FailurePolicy == Step13TransactionFailurePolicy.Required)
                {
                    failed = participant;
                    diagnostics.Add($"{stage} failed for required participant {participant.RuntimeName}.");
                    return false;
                }
            }

            return true;
        }

        private static void Rollback(IReadOnlyList<Step13TransactionParticipantPlan> participants, List<Step13TransactionParticipantResult> results)
        {
            foreach (Step13TransactionParticipantPlan participant in participants.Reverse())
            {
                bool succeeded = participant.Rollback == null || participant.Rollback();
                results.Add(new Step13TransactionParticipantResult(participant.RuntimeName, Step13TransactionStage.Rollback, succeeded, participant.FailurePolicy));
            }
        }

        private static string Clean(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public static class Step13InstitutionalIntegrationValidator
    {
        public static void ValidateOwnershipMap(IEnumerable<Step13OwnershipEntry> ownershipMap, Step13IntegrationValidationReport report)
        {
            Step13OwnershipEntry[] entries = (ownershipMap ?? Array.Empty<Step13OwnershipEntry>()).Where(item => item != null).ToArray();
            if (entries.Length == 0)
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Ownership, "missing-ownership-map", "Step 13 ownership map is empty.");
                return;
            }

            foreach (IGrouping<string, Step13OwnershipEntry> duplicate in entries.GroupBy(item => item.DomainId, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Ownership, "duplicate-domain", $"Domain '{duplicate.Key}' has multiple owners.", duplicate.Key);
            }

            foreach (Step13OwnershipEntry entry in entries.Where(item => string.IsNullOrWhiteSpace(item.FeatureId) || string.IsNullOrWhiteSpace(item.AuthoritativeRuntime)))
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Ownership, "incomplete-entry", "Ownership entries require a feature ID and authoritative runtime.", entry.DomainId);
            }
        }

        public static void ValidatePersistenceDependencies(IEnumerable<Step13PersistenceDependencyEntry> dependencies, Step13IntegrationValidationReport report)
        {
            Step13PersistenceDependencyEntry[] entries = (dependencies ?? Array.Empty<Step13PersistenceDependencyEntry>()).Where(item => item != null).ToArray();
            if (entries.Length == 0)
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Persistence, "missing-dependency-graph", "Step 13 persistence dependency graph is empty.");
                return;
            }

            HashSet<string> keys = new HashSet<string>(entries.Select(item => item.ParticipantKey), StringComparer.Ordinal);
            foreach (IGrouping<string, Step13PersistenceDependencyEntry> duplicate in entries.GroupBy(item => item.ParticipantKey, StringComparer.Ordinal).Where(group => group.Count() > 1))
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Persistence, "duplicate-participant", $"Participant '{duplicate.Key}' is declared more than once.", duplicate.Key);
            }

            foreach (Step13PersistenceDependencyEntry entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.ParticipantKey))
                {
                    report.AddError(Step13IntegrationDiagnosticDomain.Persistence, "empty-participant-key", "Persistence dependency participant key is empty.");
                }

                foreach (string dependency in entry.DependsOn)
                {
                    if (string.Equals(dependency, entry.ParticipantKey, StringComparison.Ordinal))
                    {
                        report.AddError(Step13IntegrationDiagnosticDomain.Persistence, "self-dependency", $"Participant '{entry.ParticipantKey}' depends on itself.", entry.ParticipantKey);
                    }
                    else if (keys.Contains(dependency) && HasPath(dependency, entry.ParticipantKey, entries, new HashSet<string>(StringComparer.Ordinal)))
                    {
                        report.AddError(Step13IntegrationDiagnosticDomain.Persistence, "dependency-cycle", $"Participant '{entry.ParticipantKey}' participates in a dependency cycle through '{dependency}'.", entry.ParticipantKey);
                    }
                }
            }
        }

        public static void ValidateSchedulerBudget(Step13SchedulerBudget budget, Step13IntegrationValidationReport report)
        {
            if (budget == null)
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Scheduler, "missing-budget", "Scheduler budget is required.");
                return;
            }

            if (budget.MaximumEvaluationsPerTick <= 0 || budget.MaximumQueuedInstitutionalConsequences <= 0)
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Scheduler, "invalid-limit", "Scheduler evaluation and queue limits must be positive.");
            }

            if (budget.MaximumTraversalDepth < 0 || budget.MaximumTraversalDepth > 16)
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Scheduler, "invalid-traversal-limit", "Institutional traversal depth must be bounded between 0 and 16.");
            }

            if (budget.UseSystemTime)
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Determinism, "system-time", "Step 13 scheduling must use explicit world time, not system time.");
            }

            if (budget.AllowImmediateRecursiveDispatch)
            {
                report.AddError(Step13IntegrationDiagnosticDomain.Scheduler, "immediate-recursion", "Immediate recursive institutional dispatch is not allowed.");
            }
        }

        private static bool HasPath(string start, string target, IReadOnlyList<Step13PersistenceDependencyEntry> entries, HashSet<string> visited)
        {
            if (!visited.Add(start))
            {
                return false;
            }

            Step13PersistenceDependencyEntry entry = entries.FirstOrDefault(item => string.Equals(item.ParticipantKey, start, StringComparison.Ordinal));
            if (entry == null)
            {
                return false;
            }

            foreach (string dependency in entry.DependsOn)
            {
                if (string.Equals(dependency, target, StringComparison.Ordinal) || HasPath(dependency, target, entries, visited))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
