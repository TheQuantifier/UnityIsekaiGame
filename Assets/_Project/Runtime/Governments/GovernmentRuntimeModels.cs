using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Governments
{
    public static class PoliticalModelUtility
    {
        public static string Normalize(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static bool IsHidden(PoliticalVisibility visibility)
        {
            return visibility == PoliticalVisibility.Confidential
                || visibility == PoliticalVisibility.Secret
                || visibility == PoliticalVisibility.Hidden
                || visibility == PoliticalVisibility.DevelopmentOnly;
        }
    }

    [Serializable]
    public sealed class PoliticalNameRecordData
    {
        public string nameRecordId;
        public string ownerId;
        public PoliticalNameCategory category = PoliticalNameCategory.Official;
        public string value;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public string sourceId;
        public string recognitionContextId;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string provenanceId;
        public long revision = 1L;

        public PoliticalNameRecordData Clone()
        {
            return new PoliticalNameRecordData
            {
                nameRecordId = nameRecordId ?? string.Empty,
                ownerId = ownerId ?? string.Empty,
                category = category,
                value = value ?? string.Empty,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                sourceId = sourceId ?? string.Empty,
                recognitionContextId = recognitionContextId ?? string.Empty,
                visibility = visibility,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class PolityRecordData
    {
        public string polityId;
        public string polityDefinitionId;
        public string officialName;
        public string currentGovernmentId;
        public string recognizedPrimaryGovernmentId;
        public string diplomaticActorId;
        public string[] claimantGovernmentIds = Array.Empty<string>();
        public string[] capitalPlaceIds = Array.Empty<string>();
        public string[] claimedTerritoryIds = Array.Empty<string>();
        public string[] predecessorPolityIds = Array.Empty<string>();
        public string[] successorPolityIds = Array.Empty<string>();
        public string[] splitSourcePolityIds = Array.Empty<string>();
        public string[] mergerSourcePolityIds = Array.Empty<string>();
        public PolityLifecycleState lifecycleState = PolityLifecycleState.Active;
        public double foundingWorldTime;
        public double dissolvedWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public PolityRecordData Clone()
        {
            return new PolityRecordData
            {
                polityId = polityId ?? string.Empty,
                polityDefinitionId = polityDefinitionId ?? string.Empty,
                officialName = officialName ?? string.Empty,
                currentGovernmentId = currentGovernmentId ?? string.Empty,
                recognizedPrimaryGovernmentId = recognizedPrimaryGovernmentId ?? string.Empty,
                diplomaticActorId = diplomaticActorId ?? string.Empty,
                claimantGovernmentIds = PoliticalModelUtility.Clean(claimantGovernmentIds),
                capitalPlaceIds = PoliticalModelUtility.Clean(capitalPlaceIds),
                claimedTerritoryIds = PoliticalModelUtility.Clean(claimedTerritoryIds),
                predecessorPolityIds = PoliticalModelUtility.Clean(predecessorPolityIds),
                successorPolityIds = PoliticalModelUtility.Clean(successorPolityIds),
                splitSourcePolityIds = PoliticalModelUtility.Clean(splitSourcePolityIds),
                mergerSourcePolityIds = PoliticalModelUtility.Clean(mergerSourcePolityIds),
                lifecycleState = lifecycleState,
                foundingWorldTime = foundingWorldTime,
                dissolvedWorldTime = dissolvedWorldTime,
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                tags = PoliticalModelUtility.Clean(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class GovernmentInstitutionRoleRecordData
    {
        public string roleId;
        public string governmentId;
        public string organizationId;
        public GovernmentInstitutionRoleCategory roleCategory = GovernmentInstitutionRoleCategory.Executive;
        public bool primary;
        public double effectiveWorldTime;
        public double endedWorldTime = -1d;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public long revision = 1L;

        public GovernmentInstitutionRoleRecordData Clone()
        {
            return new GovernmentInstitutionRoleRecordData
            {
                roleId = roleId ?? string.Empty,
                governmentId = governmentId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                roleCategory = roleCategory,
                primary = primary,
                effectiveWorldTime = effectiveWorldTime,
                endedWorldTime = endedWorldTime,
                sourceAuthorityGrantId = sourceAuthorityGrantId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                visibility = visibility,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class GovernmentRecordData
    {
        public string governmentId;
        public string governmentDefinitionId;
        public string polityId;
        public string officialName;
        public string primaryGoverningOrganizationId;
        public string[] governingOrganizationIds = Array.Empty<string>();
        public string parentGovernmentId;
        public string[] subordinateGovernmentIds = Array.Empty<string>();
        public GovernmentLevel level = GovernmentLevel.Central;
        public GovernmentLifecycleState lifecycleState = GovernmentLifecycleState.Active;
        public double establishedWorldTime;
        public double endedWorldTime = -1d;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public string sourceDiplomaticRecognitionId;
        public string sourceEventId;
        public string sourceRecordId;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public GovernmentRecordData Clone()
        {
            return new GovernmentRecordData
            {
                governmentId = governmentId ?? string.Empty,
                governmentDefinitionId = governmentDefinitionId ?? string.Empty,
                polityId = polityId ?? string.Empty,
                officialName = officialName ?? string.Empty,
                primaryGoverningOrganizationId = primaryGoverningOrganizationId ?? string.Empty,
                governingOrganizationIds = PoliticalModelUtility.Clean(governingOrganizationIds),
                parentGovernmentId = parentGovernmentId ?? string.Empty,
                subordinateGovernmentIds = PoliticalModelUtility.Clean(subordinateGovernmentIds),
                level = level,
                lifecycleState = lifecycleState,
                establishedWorldTime = establishedWorldTime,
                endedWorldTime = endedWorldTime,
                sourceAuthorityGrantId = sourceAuthorityGrantId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                sourceDiplomaticRecognitionId = sourceDiplomaticRecognitionId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                visibility = visibility,
                tags = PoliticalModelUtility.Clean(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TerritoryPlaceMembershipRecordData
    {
        public string membershipId;
        public string territoryId;
        public string placeId;
        public TerritoryMembershipKind membershipKind = TerritoryMembershipKind.ContainsPlace;
        public double effectiveWorldTime;
        public double endedWorldTime = -1d;
        public string sourceId;
        public long revision = 1L;

        public TerritoryPlaceMembershipRecordData Clone()
        {
            return new TerritoryPlaceMembershipRecordData
            {
                membershipId = membershipId ?? string.Empty,
                territoryId = territoryId ?? string.Empty,
                placeId = placeId ?? string.Empty,
                membershipKind = membershipKind,
                effectiveWorldTime = effectiveWorldTime,
                endedWorldTime = endedWorldTime,
                sourceId = sourceId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class PoliticalTerritoryRecordData
    {
        public string territoryId;
        public string territoryDefinitionId;
        public string displayName;
        public string parentTerritoryId;
        public string polityId;
        public string primaryGovernmentId;
        public TerritoryLifecycleState lifecycleState = TerritoryLifecycleState.Active;
        public string[] placeIds = Array.Empty<string>();
        public string[] childTerritoryIds = Array.Empty<string>();
        public double createdWorldTime;
        public double endedWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public PoliticalTerritoryRecordData Clone()
        {
            return new PoliticalTerritoryRecordData
            {
                territoryId = territoryId ?? string.Empty,
                territoryDefinitionId = territoryDefinitionId ?? string.Empty,
                displayName = displayName ?? string.Empty,
                parentTerritoryId = parentTerritoryId ?? string.Empty,
                polityId = polityId ?? string.Empty,
                primaryGovernmentId = primaryGovernmentId ?? string.Empty,
                lifecycleState = lifecycleState,
                placeIds = PoliticalModelUtility.Clean(placeIds),
                childTerritoryIds = PoliticalModelUtility.Clean(childTerritoryIds),
                createdWorldTime = createdWorldTime,
                endedWorldTime = endedWorldTime,
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                tags = PoliticalModelUtility.Clean(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TerritorialClaimRecordData
    {
        public string claimId;
        public string claimDefinitionId;
        public string territoryId;
        public string claimantPolityId;
        public string claimantGovernmentId;
        public TerritorialClaimCategory category = TerritorialClaimCategory.Sovereignty;
        public TerritorialClaimLifecycleState lifecycleState = TerritorialClaimLifecycleState.Asserted;
        public string basisAgreementId;
        public string basisDiplomaticRelationId;
        public string recognitionRelationId;
        public string sourceDecisionId;
        public double assertedWorldTime;
        public double endedWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string[] disputedByGovernmentIds = Array.Empty<string>();
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public TerritorialClaimRecordData Clone()
        {
            return new TerritorialClaimRecordData
            {
                claimId = claimId ?? string.Empty,
                claimDefinitionId = claimDefinitionId ?? string.Empty,
                territoryId = territoryId ?? string.Empty,
                claimantPolityId = claimantPolityId ?? string.Empty,
                claimantGovernmentId = claimantGovernmentId ?? string.Empty,
                category = category,
                lifecycleState = lifecycleState,
                basisAgreementId = basisAgreementId ?? string.Empty,
                basisDiplomaticRelationId = basisDiplomaticRelationId ?? string.Empty,
                recognitionRelationId = recognitionRelationId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                assertedWorldTime = assertedWorldTime,
                endedWorldTime = endedWorldTime,
                visibility = visibility,
                disputedByGovernmentIds = PoliticalModelUtility.Clean(disputedByGovernmentIds),
                tags = PoliticalModelUtility.Clean(tags),
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TerritorialControlRecordData
    {
        public string controlId;
        public string territoryId;
        public string controllingGovernmentId;
        public TerritorialControlState state = TerritorialControlState.Controlled;
        public string sourceWarId;
        public string sourceAgreementId;
        public string sourceDecisionId;
        public double effectiveWorldTime;
        public double endedWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public long revision = 1L;

        public TerritorialControlRecordData Clone()
        {
            return new TerritorialControlRecordData
            {
                controlId = controlId ?? string.Empty,
                territoryId = territoryId ?? string.Empty,
                controllingGovernmentId = controllingGovernmentId ?? string.Empty,
                state = state,
                sourceWarId = sourceWarId ?? string.Empty,
                sourceAgreementId = sourceAgreementId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime,
                endedWorldTime = endedWorldTime,
                visibility = visibility,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class TerritoryAdministrationRecordData
    {
        public string administrationId;
        public string territoryId;
        public string administeringGovernmentId;
        public string delegatedByGovernmentId;
        public AdministrationState state = AdministrationState.Administered;
        public string sourceAgreementId;
        public string sourceDecisionId;
        public double effectiveWorldTime;
        public double endedWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public long revision = 1L;

        public TerritoryAdministrationRecordData Clone()
        {
            return new TerritoryAdministrationRecordData
            {
                administrationId = administrationId ?? string.Empty,
                territoryId = territoryId ?? string.Empty,
                administeringGovernmentId = administeringGovernmentId ?? string.Empty,
                delegatedByGovernmentId = delegatedByGovernmentId ?? string.Empty,
                state = state,
                sourceAgreementId = sourceAgreementId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime,
                endedWorldTime = endedWorldTime,
                visibility = visibility,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class GovernmentSeatRecordData
    {
        public string seatId;
        public string governmentId;
        public string placeId;
        public SeatCategory category = SeatCategory.Capital;
        public bool primary;
        public double effectiveWorldTime;
        public double endedWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public long revision = 1L;

        public GovernmentSeatRecordData Clone()
        {
            return new GovernmentSeatRecordData
            {
                seatId = seatId ?? string.Empty,
                governmentId = governmentId ?? string.Empty,
                placeId = placeId ?? string.Empty,
                category = category,
                primary = primary,
                effectiveWorldTime = effectiveWorldTime,
                endedWorldTime = endedWorldTime,
                visibility = visibility,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class SovereigntyClaimRecordData
    {
        public string sovereigntyClaimId;
        public string polityId;
        public string governmentId;
        public string territoryId;
        public SovereigntyClaimCategory category = SovereigntyClaimCategory.FullSovereignty;
        public SovereigntyClaimState state = SovereigntyClaimState.Claimed;
        public string recognitionRelationId;
        public string sourceDecisionId;
        public double assertedWorldTime;
        public double endedWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public long revision = 1L;

        public SovereigntyClaimRecordData Clone()
        {
            return new SovereigntyClaimRecordData
            {
                sovereigntyClaimId = sovereigntyClaimId ?? string.Empty,
                polityId = polityId ?? string.Empty,
                governmentId = governmentId ?? string.Empty,
                territoryId = territoryId ?? string.Empty,
                category = category,
                state = state,
                recognitionRelationId = recognitionRelationId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                assertedWorldTime = assertedWorldTime,
                endedWorldTime = endedWorldTime,
                visibility = visibility,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class JurisdictionRecordData
    {
        public string jurisdictionId;
        public string jurisdictionDefinitionId;
        public string governmentId;
        public string sourceJurisdictionId;
        public string parentJurisdictionId;
        public JurisdictionCategory category = JurisdictionCategory.GeneralGovernment;
        public JurisdictionScopeDimension scopeDimensions = JurisdictionScopeDimension.Territory | JurisdictionScopeDimension.SubjectMatter;
        public JurisdictionSubjectMatter[] subjectMatters = Array.Empty<JurisdictionSubjectMatter>();
        public string[] territoryIds = Array.Empty<string>();
        public string[] placeIds = Array.Empty<string>();
        public string[] personIds = Array.Empty<string>();
        public string[] organizationIds = Array.Empty<string>();
        public string[] propertyIds = Array.Empty<string>();
        public string[] officeIds = Array.Empty<string>();
        public string[] statusIds = Array.Empty<string>();
        public JurisdictionLifecycleState lifecycleState = JurisdictionLifecycleState.Active;
        public JurisdictionConflictPolicy conflictPolicy = JurisdictionConflictPolicy.SpecificOverridesGeneral;
        public int priority;
        public bool exclusive;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public double effectiveWorldTime;
        public double expirationWorldTime = -1d;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public long revision = 1L;

        public JurisdictionRecordData Clone()
        {
            return new JurisdictionRecordData
            {
                jurisdictionId = jurisdictionId ?? string.Empty,
                jurisdictionDefinitionId = jurisdictionDefinitionId ?? string.Empty,
                governmentId = governmentId ?? string.Empty,
                sourceJurisdictionId = sourceJurisdictionId ?? string.Empty,
                parentJurisdictionId = parentJurisdictionId ?? string.Empty,
                category = category,
                scopeDimensions = scopeDimensions,
                subjectMatters = (subjectMatters ?? Array.Empty<JurisdictionSubjectMatter>()).Where(item => item != JurisdictionSubjectMatter.Unknown).Distinct().OrderBy(item => item).ToArray(),
                territoryIds = PoliticalModelUtility.Clean(territoryIds),
                placeIds = PoliticalModelUtility.Clean(placeIds),
                personIds = PoliticalModelUtility.Clean(personIds),
                organizationIds = PoliticalModelUtility.Clean(organizationIds),
                propertyIds = PoliticalModelUtility.Clean(propertyIds),
                officeIds = PoliticalModelUtility.Clean(officeIds),
                statusIds = PoliticalModelUtility.Clean(statusIds),
                lifecycleState = lifecycleState,
                conflictPolicy = conflictPolicy,
                priority = priority,
                exclusive = exclusive,
                sourceAuthorityGrantId = sourceAuthorityGrantId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                effectiveWorldTime = effectiveWorldTime,
                expirationWorldTime = expirationWorldTime,
                visibility = visibility,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class PoliticalTransitionPlanRecordData
    {
        public string transitionId;
        public PoliticalTransitionKind transitionKind = PoliticalTransitionKind.BoundaryChange;
        public string sourcePolityId;
        public string targetPolityId;
        public string sourceGovernmentId;
        public string targetGovernmentId;
        public string[] territoryIds = Array.Empty<string>();
        public string sourceAgreementId;
        public string sourceDecisionId;
        public bool executed;
        public double plannedWorldTime;
        public double executedWorldTime = -1d;
        public string diagnostics;
        public long revision = 1L;

        public PoliticalTransitionPlanRecordData Clone()
        {
            return new PoliticalTransitionPlanRecordData
            {
                transitionId = transitionId ?? string.Empty,
                transitionKind = transitionKind,
                sourcePolityId = sourcePolityId ?? string.Empty,
                targetPolityId = targetPolityId ?? string.Empty,
                sourceGovernmentId = sourceGovernmentId ?? string.Empty,
                targetGovernmentId = targetGovernmentId ?? string.Empty,
                territoryIds = PoliticalModelUtility.Clean(territoryIds),
                sourceAgreementId = sourceAgreementId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                executed = executed,
                plannedWorldTime = plannedWorldTime,
                executedWorldTime = executedWorldTime,
                diagnostics = diagnostics ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class GovernmentOrganizationCharterRecordData
    {
        public string charterId;
        public string charterDefinitionId;
        public string governmentId;
        public string organizationId;
        public string supervisingOrganizationId;
        public string legalInstrumentId;
        public string[] jurisdictionIds = Array.Empty<string>();
        public string[] grantedPowerIds = Array.Empty<string>();
        public string[] dutyIds = Array.Empty<string>();
        public GovernmentOrganizationCharterCategory category;
        public GovernmentOrganizationCharterLifecycleState lifecycleState = GovernmentOrganizationCharterLifecycleState.Active;
        public bool revocable = true;
        public double effectiveWorldTime;
        public double endedWorldTime = -1d;
        public string sourceAuthorityGrantId;
        public string sourceDecisionId;
        public PoliticalVisibility visibility = PoliticalVisibility.Public;
        public string provenanceId;
        public long revision = 1L;

        public GovernmentOrganizationCharterRecordData Clone()
        {
            return new GovernmentOrganizationCharterRecordData
            {
                charterId = charterId ?? string.Empty,
                charterDefinitionId = charterDefinitionId ?? string.Empty,
                governmentId = governmentId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                supervisingOrganizationId = supervisingOrganizationId ?? string.Empty,
                legalInstrumentId = legalInstrumentId ?? string.Empty,
                jurisdictionIds = PoliticalModelUtility.Clean(jurisdictionIds),
                grantedPowerIds = PoliticalModelUtility.Clean(grantedPowerIds),
                dutyIds = PoliticalModelUtility.Clean(dutyIds),
                category = category,
                lifecycleState = lifecycleState,
                revocable = revocable,
                effectiveWorldTime = effectiveWorldTime,
                endedWorldTime = endedWorldTime,
                sourceAuthorityGrantId = sourceAuthorityGrantId ?? string.Empty,
                sourceDecisionId = sourceDecisionId ?? string.Empty,
                visibility = visibility,
                provenanceId = provenanceId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }
    }

    [Serializable]
    public sealed class PoliticalTransactionRecordData
    {
        public string transactionId;
        public string operationKind;
        public string subjectId;
        public long revision;

        public PoliticalTransactionRecordData Clone()
        {
            return new PoliticalTransactionRecordData
            {
                transactionId = transactionId ?? string.Empty,
                operationKind = operationKind ?? string.Empty,
                subjectId = subjectId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class GovernmentRuntimeSaveData
    {
        public int schemaVersion = 1;
        public long revision;
        public string worldId;
        public PolityRecordData[] polities = Array.Empty<PolityRecordData>();
        public PoliticalNameRecordData[] names = Array.Empty<PoliticalNameRecordData>();
        public GovernmentRecordData[] governments = Array.Empty<GovernmentRecordData>();
        public GovernmentInstitutionRoleRecordData[] institutionRoles = Array.Empty<GovernmentInstitutionRoleRecordData>();
        public PoliticalTerritoryRecordData[] territories = Array.Empty<PoliticalTerritoryRecordData>();
        public TerritoryPlaceMembershipRecordData[] territoryPlaceMemberships = Array.Empty<TerritoryPlaceMembershipRecordData>();
        public TerritorialClaimRecordData[] claims = Array.Empty<TerritorialClaimRecordData>();
        public TerritorialControlRecordData[] controls = Array.Empty<TerritorialControlRecordData>();
        public TerritoryAdministrationRecordData[] administrations = Array.Empty<TerritoryAdministrationRecordData>();
        public GovernmentSeatRecordData[] seats = Array.Empty<GovernmentSeatRecordData>();
        public SovereigntyClaimRecordData[] sovereigntyClaims = Array.Empty<SovereigntyClaimRecordData>();
        public JurisdictionRecordData[] jurisdictions = Array.Empty<JurisdictionRecordData>();
        public GovernmentOrganizationCharterRecordData[] organizationCharters = Array.Empty<GovernmentOrganizationCharterRecordData>();
        public GovernmentOfficeTenureRecordData[] officeTenures = Array.Empty<GovernmentOfficeTenureRecordData>();
        public GovernmentElectionRecordData[] elections = Array.Empty<GovernmentElectionRecordData>();
        public GovernmentPermitRecordData[] permits = Array.Empty<GovernmentPermitRecordData>();
        public GovernmentFiscalMandateRecordData[] fiscalMandates = Array.Empty<GovernmentFiscalMandateRecordData>();
        public GovernmentLegitimacyRecordData[] legitimacyAssessments = Array.Empty<GovernmentLegitimacyRecordData>();
        public GovernmentRemittancePolicyRecordData[] remittancePolicies = Array.Empty<GovernmentRemittancePolicyRecordData>();
        public GovernmentRemittanceSettlementRecordData[] remittanceSettlements = Array.Empty<GovernmentRemittanceSettlementRecordData>();
        public GovernmentFiscalReliefRecordData[] fiscalReliefs = Array.Empty<GovernmentFiscalReliefRecordData>();
        public GovernmentActionRegulationRecordData[] actionRegulations = Array.Empty<GovernmentActionRegulationRecordData>();
        public GovernmentOfficeVacancyRecordData[] officeVacancies = Array.Empty<GovernmentOfficeVacancyRecordData>();
        public GovernmentLegitimacyEventRecordData[] legitimacyEvents = Array.Empty<GovernmentLegitimacyEventRecordData>();
        public GovernmentBudgetCycleRecordData[] budgetCycles = Array.Empty<GovernmentBudgetCycleRecordData>();
        public PoliticalTransitionPlanRecordData[] transitions = Array.Empty<PoliticalTransitionPlanRecordData>();
        public PoliticalTransactionRecordData[] transactions = Array.Empty<PoliticalTransactionRecordData>();

        public GovernmentRuntimeSaveData Clone()
        {
            return new GovernmentRuntimeSaveData
            {
                schemaVersion = Math.Max(1, schemaVersion),
                revision = revision,
                worldId = worldId ?? string.Empty,
                polities = polities == null ? Array.Empty<PolityRecordData>() : polities.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                names = names == null ? Array.Empty<PoliticalNameRecordData>() : names.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                governments = governments == null ? Array.Empty<GovernmentRecordData>() : governments.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                institutionRoles = institutionRoles == null ? Array.Empty<GovernmentInstitutionRoleRecordData>() : institutionRoles.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                territories = territories == null ? Array.Empty<PoliticalTerritoryRecordData>() : territories.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                territoryPlaceMemberships = territoryPlaceMemberships == null ? Array.Empty<TerritoryPlaceMembershipRecordData>() : territoryPlaceMemberships.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                claims = claims == null ? Array.Empty<TerritorialClaimRecordData>() : claims.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                controls = controls == null ? Array.Empty<TerritorialControlRecordData>() : controls.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                administrations = administrations == null ? Array.Empty<TerritoryAdministrationRecordData>() : administrations.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                seats = seats == null ? Array.Empty<GovernmentSeatRecordData>() : seats.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                sovereigntyClaims = sovereigntyClaims == null ? Array.Empty<SovereigntyClaimRecordData>() : sovereigntyClaims.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                jurisdictions = jurisdictions == null ? Array.Empty<JurisdictionRecordData>() : jurisdictions.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                organizationCharters = organizationCharters == null ? Array.Empty<GovernmentOrganizationCharterRecordData>() : organizationCharters.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                officeTenures = officeTenures == null ? Array.Empty<GovernmentOfficeTenureRecordData>() : officeTenures.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                elections = elections == null ? Array.Empty<GovernmentElectionRecordData>() : elections.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                permits = permits == null ? Array.Empty<GovernmentPermitRecordData>() : permits.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                fiscalMandates = fiscalMandates == null ? Array.Empty<GovernmentFiscalMandateRecordData>() : fiscalMandates.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                legitimacyAssessments = legitimacyAssessments == null ? Array.Empty<GovernmentLegitimacyRecordData>() : legitimacyAssessments.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                remittancePolicies = remittancePolicies == null ? Array.Empty<GovernmentRemittancePolicyRecordData>() : remittancePolicies.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                remittanceSettlements = remittanceSettlements == null ? Array.Empty<GovernmentRemittanceSettlementRecordData>() : remittanceSettlements.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                fiscalReliefs = fiscalReliefs == null ? Array.Empty<GovernmentFiscalReliefRecordData>() : fiscalReliefs.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                actionRegulations = actionRegulations == null ? Array.Empty<GovernmentActionRegulationRecordData>() : actionRegulations.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                officeVacancies = officeVacancies == null ? Array.Empty<GovernmentOfficeVacancyRecordData>() : officeVacancies.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                legitimacyEvents = legitimacyEvents == null ? Array.Empty<GovernmentLegitimacyEventRecordData>() : legitimacyEvents.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                budgetCycles = budgetCycles == null ? Array.Empty<GovernmentBudgetCycleRecordData>() : budgetCycles.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                transitions = transitions == null ? Array.Empty<PoliticalTransitionPlanRecordData>() : transitions.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                transactions = transactions == null ? Array.Empty<PoliticalTransactionRecordData>() : transactions.Select(item => item?.Clone()).Where(item => item != null).ToArray()
            };
        }
    }

    public sealed class PoliticalOperationResult
    {
        public bool Succeeded { get; private set; }
        public PoliticalOperationCode Code { get; private set; }
        public bool Preview { get; private set; }
        public bool Duplicate { get; private set; }
        public string SubjectId { get; private set; }
        public string Message { get; private set; }
        public long RevisionBefore { get; private set; }
        public long RevisionAfter { get; private set; }
        public PolityRecordData Polity { get; private set; }
        public GovernmentRecordData Government { get; private set; }
        public PoliticalTerritoryRecordData Territory { get; private set; }
        public TerritorialClaimRecordData Claim { get; private set; }
        public JurisdictionRecordData Jurisdiction { get; private set; }
        public GovernmentOrganizationCharterRecordData OrganizationCharter { get; private set; }
        public PoliticalTransitionPlanRecordData Transition { get; private set; }

        public static PoliticalOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false, string subjectId = "", PolityRecordData polity = null, GovernmentRecordData government = null, PoliticalTerritoryRecordData territory = null, TerritorialClaimRecordData claim = null, JurisdictionRecordData jurisdiction = null, PoliticalTransitionPlanRecordData transition = null, GovernmentOrganizationCharterRecordData organizationCharter = null)
        {
            return new PoliticalOperationResult
            {
                Succeeded = true,
                Code = preview ? PoliticalOperationCode.Preview : duplicate ? PoliticalOperationCode.Duplicate : PoliticalOperationCode.Succeeded,
                Preview = preview,
                Duplicate = duplicate,
                SubjectId = subjectId ?? string.Empty,
                Message = message ?? string.Empty,
                RevisionBefore = before,
                RevisionAfter = after,
                Polity = polity?.Clone(),
                Government = government?.Clone(),
                Territory = territory?.Clone(),
                Claim = claim?.Clone(),
                Jurisdiction = jurisdiction?.Clone(),
                Transition = transition?.Clone(),
                OrganizationCharter = organizationCharter?.Clone()
            };
        }

        public static PoliticalOperationResult Failure(PoliticalOperationCode code, string message, long revision)
        {
            return new PoliticalOperationResult
            {
                Succeeded = false,
                Code = code,
                Message = message ?? string.Empty,
                RevisionBefore = revision,
                RevisionAfter = revision
            };
        }
    }

    public sealed class PoliticalProjectionResult<TRecord> where TRecord : class
    {
        public bool Succeeded { get; private set; }
        public string Decision { get; private set; }
        public string SubjectId { get; private set; }
        public TRecord Record { get; private set; }
        public bool Redacted { get; private set; }
        public string Message { get; private set; }

        public static PoliticalProjectionResult<TRecord> Full(string subjectId, TRecord record) => Create(true, "FullAccess", subjectId, record, false, "Full political projection.");
        public static PoliticalProjectionResult<TRecord> RedactedProjection(string subjectId, TRecord record) => Create(true, "RedactedAccess", subjectId, record, true, "Political projection redacted.");
        public static PoliticalProjectionResult<TRecord> Denied(string subjectId, string message) => Create(false, "Denied", subjectId, null, true, message);

        private static PoliticalProjectionResult<TRecord> Create(bool succeeded, string decision, string subjectId, TRecord record, bool redacted, string message)
        {
            return new PoliticalProjectionResult<TRecord>
            {
                Succeeded = succeeded,
                Decision = decision,
                SubjectId = subjectId ?? string.Empty,
                Record = record,
                Redacted = redacted,
                Message = message ?? string.Empty
            };
        }
    }

    public sealed class JurisdictionResolutionRequest
    {
        public string requesterGovernmentId;
        public string territoryId;
        public string placeId;
        public string personId;
        public string organizationId;
        public string propertyId;
        public string officeId;
        public string statusId;
        public JurisdictionSubjectMatter subjectMatter = JurisdictionSubjectMatter.GeneralAdministration;
        public double worldTime;
    }

    public sealed class JurisdictionResolutionResult
    {
        public JurisdictionResolutionStatus Status { get; private set; }
        public IReadOnlyList<JurisdictionRecordData> ApplicableJurisdictions { get; private set; }
        public JurisdictionRecordData SelectedJurisdiction { get; private set; }
        public string Message { get; private set; }

        public static JurisdictionResolutionResult Create(JurisdictionResolutionStatus status, IEnumerable<JurisdictionRecordData> applicable, JurisdictionRecordData selected, string message)
        {
            return new JurisdictionResolutionResult
            {
                Status = status,
                ApplicableJurisdictions = (applicable ?? Array.Empty<JurisdictionRecordData>()).Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                SelectedJurisdiction = selected?.Clone(),
                Message = message ?? string.Empty
            };
        }
    }
}
