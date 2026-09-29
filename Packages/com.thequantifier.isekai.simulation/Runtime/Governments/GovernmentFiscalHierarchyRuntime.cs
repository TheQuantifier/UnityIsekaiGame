using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Governments
{
    public sealed partial class GovernmentRuntime
    {
        private readonly Dictionary<string, GovernmentRemittancePolicyRecordData> remittancePoliciesById = new Dictionary<string, GovernmentRemittancePolicyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentRemittanceSettlementRecordData> remittanceSettlementsById = new Dictionary<string, GovernmentRemittanceSettlementRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentFiscalReliefRecordData> fiscalReliefsById = new Dictionary<string, GovernmentFiscalReliefRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentActionRegulationRecordData> actionRegulationsById = new Dictionary<string, GovernmentActionRegulationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentOfficeVacancyRecordData> officeVacanciesById = new Dictionary<string, GovernmentOfficeVacancyRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentLegitimacyEventRecordData> legitimacyEventsById = new Dictionary<string, GovernmentLegitimacyEventRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, GovernmentBudgetCycleRecordData> budgetCyclesById = new Dictionary<string, GovernmentBudgetCycleRecordData>(StringComparer.Ordinal);

        private EconomyRuntime economy;
        private InstitutionalRevenueRuntime institutionalRevenue;

        public IReadOnlyList<GovernmentRemittancePolicyRecordData> RemittancePolicies => remittancePoliciesById.Values.OrderBy(value => value.policyId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentRemittanceSettlementRecordData> RemittanceSettlements => remittanceSettlementsById.Values.OrderBy(value => value.settledWorldTime).ThenBy(value => value.settlementId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentFiscalReliefRecordData> FiscalReliefs => fiscalReliefsById.Values.OrderBy(value => value.effectiveWorldTime).ThenBy(value => value.reliefId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentActionRegulationRecordData> ActionRegulations => actionRegulationsById.Values.OrderBy(value => value.regulationId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentOfficeVacancyRecordData> OfficeVacancies => officeVacanciesById.Values.OrderBy(value => value.openedWorldTime).ThenBy(value => value.vacancyId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentLegitimacyEventRecordData> LegitimacyEvents => legitimacyEventsById.Values.OrderBy(value => value.worldTime).ThenBy(value => value.eventId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();
        public IReadOnlyList<GovernmentBudgetCycleRecordData> BudgetCycles => budgetCyclesById.Values.OrderBy(value => value.cycleId, StringComparer.Ordinal).Select(value => value.Clone()).ToArray();

        public void ConfigureFiscalDependencies(EconomyRuntime economyRuntime, InstitutionalRevenueRuntime revenueRuntime)
        {
            economy = economyRuntime ?? economy;
            institutionalRevenue = revenueRuntime ?? institutionalRevenue;
        }

        public PoliticalOperationResult SetGovernmentParent(string governmentId, string parentGovernmentId, string transactionId, bool preview = false)
        {
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            governmentId = PoliticalModelUtility.Normalize(governmentId);
            parentGovernmentId = PoliticalModelUtility.Normalize(parentGovernmentId);
            if (TryDuplicate(transactionId, governmentId, "set-government-parent", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!governmentsById.TryGetValue(governmentId, out GovernmentRecordData government)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (!string.IsNullOrEmpty(parentGovernmentId) && !governmentsById.ContainsKey(parentGovernmentId)) return Fail(PoliticalOperationCode.MissingGovernment, $"Parent government '{parentGovernmentId}' is missing.", before);
            if (governmentId == parentGovernmentId || WouldCreateGovernmentCycle(governmentId, parentGovernmentId)) return Fail(PoliticalOperationCode.CycleRejected, "Government hierarchy cycle rejected.", before);
            if (government.parentGovernmentId == parentGovernmentId) return PoliticalOperationResult.Success("Government parent already matches.", before, before, preview: preview, subjectId: governmentId, government: government);
            if (preview) return PoliticalOperationResult.Success("Government parent change previewed.", before, before, preview: true, subjectId: governmentId, government: government);

            if (!string.IsNullOrEmpty(government.parentGovernmentId) && governmentsById.TryGetValue(government.parentGovernmentId, out GovernmentRecordData formerParent))
            {
                formerParent.subordinateGovernmentIds = PoliticalModelUtility.Clean(formerParent.subordinateGovernmentIds.Where(id => id != governmentId));
                formerParent.revision++;
            }
            if (!string.IsNullOrEmpty(parentGovernmentId) && governmentsById.TryGetValue(parentGovernmentId, out GovernmentRecordData newParent))
            {
                newParent.subordinateGovernmentIds = PoliticalModelUtility.Clean(newParent.subordinateGovernmentIds.Concat(new[] { governmentId }));
                newParent.revision++;
            }
            government.parentGovernmentId = parentGovernmentId;
            government.revision++;
            CompleteTransaction(transactionId, "set-government-parent", governmentId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government parent updated.", before, Revision, subjectId: governmentId, government: government));
        }

        public PoliticalOperationResult SetGovernmentOrganizations(string governmentId, string primaryOrganizationId, IEnumerable<string> governingOrganizationIds, string transactionId, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            governmentId = PoliticalModelUtility.Normalize(governmentId);
            primaryOrganizationId = PoliticalModelUtility.Normalize(primaryOrganizationId);
            string[] organizationIds = PoliticalModelUtility.Clean(governingOrganizationIds);
            if (!string.IsNullOrEmpty(primaryOrganizationId)) organizationIds = PoliticalModelUtility.Clean(organizationIds.Concat(new[] { primaryOrganizationId }));
            if (TryDuplicate(transactionId, governmentId, "set-government-organizations", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!governmentsById.TryGetValue(governmentId, out GovernmentRecordData government)) return Fail(PoliticalOperationCode.MissingGovernment, $"Government '{governmentId}' is missing.", before);
            if (organizationIds.Length == 0 || string.IsNullOrEmpty(primaryOrganizationId)) return Fail(PoliticalOperationCode.InvalidRequest, "At least one governing organization and a primary organization are required.", before);
            if (!TryGetDefinition(government.governmentDefinitionId, out GovernmentDefinition definition)) return Fail(PoliticalOperationCode.MissingDefinition, $"Government definition '{government.governmentDefinitionId}' is missing.", before);
            if (!definition.AllowsSeveralGoverningOrganizations && organizationIds.Length > 1) return Fail(PoliticalOperationCode.InvalidRequest, $"Government definition '{definition.Id}' permits only one governing organization.", before);
            foreach (string organizationId in organizationIds) if (!ValidateOrganization(organizationId, out string organizationFailure)) return Fail(PoliticalOperationCode.InvalidReference, organizationFailure, before);
            string[] activeRoleOrganizations = PoliticalModelUtility.Clean(institutionRolesById.Values.Where(value => value.governmentId == governmentId && value.endedWorldTime < 0d).Select(value => value.organizationId));
            string[] activePrimaryOrganizations = PoliticalModelUtility.Clean(institutionRolesById.Values.Where(value => value.governmentId == governmentId && value.endedWorldTime < 0d && value.primary).Select(value => value.organizationId));
            if (government.primaryGoverningOrganizationId == primaryOrganizationId && PoliticalModelUtility.Clean(government.governingOrganizationIds).SequenceEqual(organizationIds) && activeRoleOrganizations.SequenceEqual(organizationIds) && activePrimaryOrganizations.SequenceEqual(new[] { primaryOrganizationId })) return PoliticalOperationResult.Success("Government organizations already match.", before, before, preview: preview, subjectId: governmentId, government: government);
            if (preview) return PoliticalOperationResult.Success("Government organization alignment previewed.", before, before, preview: true, subjectId: governmentId, government: government);

            foreach (GovernmentInstitutionRoleRecordData role in institutionRolesById.Values.Where(value => value.governmentId == governmentId && value.endedWorldTime < 0d))
            {
                role.endedWorldTime = worldTime;
                role.revision++;
            }
            for (int index = 0; index < organizationIds.Length; index++)
            {
                string roleId = $"{governmentId}.institution.aligned.r{before + 1:000000}.{index + 1:000}";
                institutionRolesById[roleId] = new GovernmentInstitutionRoleRecordData
                {
                    roleId = roleId,
                    governmentId = governmentId,
                    organizationId = organizationIds[index],
                    roleCategory = organizationIds[index] == primaryOrganizationId ? GovernmentInstitutionRoleCategory.Executive : GovernmentInstitutionRoleCategory.Custom,
                    primary = organizationIds[index] == primaryOrganizationId,
                    effectiveWorldTime = worldTime,
                    visibility = government.visibility,
                    revision = 1L
                };
            }
            government.primaryGoverningOrganizationId = primaryOrganizationId;
            government.governingOrganizationIds = organizationIds;
            government.revision++;
            CompleteTransaction(transactionId, "set-government-organizations", governmentId);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Government organizations updated.", before, Revision, subjectId: governmentId, government: government));
        }

        public PoliticalOperationResult EnactRemittancePolicy(GovernmentRemittancePolicyRequest request)
        {
            request ??= new GovernmentRemittancePolicyRequest();
            long before = Revision;
            if (!Ready(out PoliticalOperationResult failure)) return failure;
            string id = PoliticalModelUtility.Normalize(request.policyId);
            if (TryDuplicate(request.transactionId, id, "enact-remittance-policy", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id) || remittancePoliciesById.ContainsKey(id)) return Fail(PoliticalOperationCode.InvalidRequest, "A unique remittance policy ID is required.", before);
            string sourceId = PoliticalModelUtility.Normalize(request.sourceGovernmentId);
            string destinationId = PoliticalModelUtility.Normalize(request.destinationGovernmentId);
            if (!governmentsById.TryGetValue(sourceId, out GovernmentRecordData source) || !governmentsById.TryGetValue(destinationId, out GovernmentRecordData destination)) return Fail(PoliticalOperationCode.MissingGovernment, "Both source and destination governments must exist.", before);
            if (!string.Equals(source.parentGovernmentId, destinationId, StringComparison.Ordinal)) return Fail(PoliticalOperationCode.InvalidReference, "Remittance must be paid to the source government's immediate parent.", before);
            if (source.lifecycleState != GovernmentLifecycleState.Active || destination.lifecycleState != GovernmentLifecycleState.Active) return Fail(PoliticalOperationCode.InvalidState, "Both governments must be active.", before);
            if (request.remittanceBasisPoints < 0 || request.remittanceBasisPoints > 10000 || request.fixedTributeUnits < 0L || request.minimumReserveUnits < 0L || request.settlementIntervalWorldTime <= 0d) return Fail(PoliticalOperationCode.InvalidRequest, "Remittance rate, fixed tribute, reserve, or interval is invalid.", before);
            if (string.IsNullOrWhiteSpace(request.sourceEconomyAccountId) || string.IsNullOrWhiteSpace(request.destinationEconomyAccountId) || string.IsNullOrWhiteSpace(request.currencyDefinitionId)) return Fail(PoliticalOperationCode.InvalidRequest, "Remittance accounts and currency are required.", before);
            if (economy != null)
            {
                if (!economy.TryGetAccount(request.sourceEconomyAccountId, out EconomyAccountSnapshot sourceAccount) || !economy.TryGetAccount(request.destinationEconomyAccountId, out EconomyAccountSnapshot destinationAccount)) return Fail(PoliticalOperationCode.InvalidReference, "A remittance economy account is missing.", before);
                if (sourceAccount.CurrencyId != request.currencyDefinitionId || destinationAccount.CurrencyId != request.currencyDefinitionId) return Fail(PoliticalOperationCode.InvalidReference, "Remittance accounts do not use the policy currency.", before);
            }
            string[] revenueIds = PoliticalModelUtility.Clean(request.eligibleRevenueDefinitionIds);
            if (revenueIds.Any(revenueId => registry != null && !registry.TryGet(revenueId, out InstitutionalRevenueDefinition _))) return Fail(PoliticalOperationCode.MissingDefinition, "A remittance revenue definition is missing.", before);
            GovernmentRemittancePolicyRecordData record = new GovernmentRemittancePolicyRecordData
            {
                policyId = id, sourceGovernmentId = sourceId, destinationGovernmentId = destinationId,
                sourceEconomyAccountId = request.sourceEconomyAccountId.Trim(), destinationEconomyAccountId = request.destinationEconomyAccountId.Trim(),
                currencyDefinitionId = request.currencyDefinitionId.Trim(), eligibleRevenueDefinitionIds = revenueIds,
                remittanceBasisPoints = request.remittanceBasisPoints, fixedTributeUnits = request.fixedTributeUnits,
                minimumReserveUnits = request.minimumReserveUnits, settlementIntervalWorldTime = request.settlementIntervalWorldTime,
                nextSettlementWorldTime = request.firstSettlementWorldTime, gracePeriodWorldTime = Math.Max(0d, request.gracePeriodWorldTime),
                state = GovernmentRemittancePolicyState.Active, sourceAuthorityId = PoliticalModelUtility.Normalize(request.sourceAuthorityId),
                provenanceId = PoliticalModelUtility.Normalize(request.provenanceId)
            };
            if (request.preview) return PoliticalOperationResult.Success("Remittance policy previewed.", before, before, preview: true, subjectId: id);
            remittancePoliciesById.Add(id, record);
            CompleteTransaction(request.transactionId, "enact-remittance-policy", id);
            Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Remittance policy enacted.", before, Revision, subjectId: id));
        }

        public PoliticalOperationResult GrantFiscalRelief(GovernmentFiscalReliefRequest request)
        {
            request ??= new GovernmentFiscalReliefRequest();
            long before = Revision;
            string id = PoliticalModelUtility.Normalize(request.reliefId);
            if (TryDuplicate(request.transactionId, id, "grant-fiscal-relief", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id) || fiscalReliefsById.ContainsKey(id) || !remittancePoliciesById.ContainsKey(PoliticalModelUtility.Normalize(request.remittancePolicyId))) return Fail(PoliticalOperationCode.InvalidReference, "Fiscal relief requires a unique ID and existing remittance policy.", before);
            if (request.reductionBasisPoints < 0 || request.reductionBasisPoints > 10000 || request.waivedFixedUnits < 0L || request.expirationWorldTime >= 0d && request.expirationWorldTime <= request.effectiveWorldTime) return Fail(PoliticalOperationCode.InvalidRequest, "Fiscal relief values or dates are invalid.", before);
            GovernmentFiscalReliefRecordData record = new GovernmentFiscalReliefRecordData { reliefId = id, remittancePolicyId = PoliticalModelUtility.Normalize(request.remittancePolicyId), reductionBasisPoints = request.reductionBasisPoints, waivedFixedUnits = request.waivedFixedUnits, effectiveWorldTime = request.effectiveWorldTime, expirationWorldTime = request.expirationWorldTime, reason = request.reason ?? string.Empty, sourceAuthorityId = PoliticalModelUtility.Normalize(request.sourceAuthorityId) };
            if (request.preview) return PoliticalOperationResult.Success("Fiscal relief previewed.", before, before, preview: true, subjectId: id);
            fiscalReliefsById.Add(id, record); CompleteTransaction(request.transactionId, "grant-fiscal-relief", id); Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Fiscal relief granted.", before, Revision, subjectId: id));
        }

        public PoliticalOperationResult RegisterBudgetCycle(GovernmentBudgetCycleRequest request)
        {
            request ??= new GovernmentBudgetCycleRequest();
            long before = Revision;
            string id = PoliticalModelUtility.Normalize(request.cycleId);
            if (TryDuplicate(request.transactionId, id, "register-budget-cycle", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id) || budgetCyclesById.ContainsKey(id) || !governmentsById.ContainsKey(PoliticalModelUtility.Normalize(request.governmentId))) return Fail(PoliticalOperationCode.InvalidReference, "Budget cycle identity or government is invalid.", before);
            if (resources == null || !resources.Accounts.Any(value => value.accountId == request.accountId && value.organizationId == request.organizationId && value.treasuryId == request.treasuryId)) return Fail(PoliticalOperationCode.InvalidReference, "Budget cycle account scope is invalid.", before);
            if (request.authorizedUnits < 0L || request.cycleDurationWorldTime <= 0d || string.IsNullOrWhiteSpace(request.budgetIdPrefix) || string.IsNullOrWhiteSpace(request.actorPersonId)) return Fail(PoliticalOperationCode.InvalidRequest, "Budget amount, duration, prefix, and actor are required.", before);
            GovernmentBudgetCycleRecordData record = new GovernmentBudgetCycleRecordData { cycleId = id, governmentId = PoliticalModelUtility.Normalize(request.governmentId), organizationId = PoliticalModelUtility.Normalize(request.organizationId), treasuryId = PoliticalModelUtility.Normalize(request.treasuryId), accountId = PoliticalModelUtility.Normalize(request.accountId), currencyDefinitionId = PoliticalModelUtility.Normalize(request.currencyDefinitionId), budgetIdPrefix = PoliticalModelUtility.Normalize(request.budgetIdPrefix), currentBudgetId = PoliticalModelUtility.Normalize(request.currentBudgetId), category = request.category, enforcementPolicy = request.enforcementPolicy, authorizedUnits = request.authorizedUnits, purpose = request.purpose ?? string.Empty, fundingSourceId = PoliticalModelUtility.Normalize(request.fundingSourceId), actorPersonId = PoliticalModelUtility.Normalize(request.actorPersonId), sourceAuthorityId = PoliticalModelUtility.Normalize(request.sourceAuthorityId), cycleDurationWorldTime = request.cycleDurationWorldTime, nextCycleWorldTime = request.nextCycleWorldTime, provenanceId = PoliticalModelUtility.Normalize(request.provenanceId) };
            if (request.preview) return PoliticalOperationResult.Success("Budget cycle previewed.", before, before, preview: true, subjectId: id);
            budgetCyclesById.Add(id, record); CompleteTransaction(request.transactionId, "register-budget-cycle", id); Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Renewable government budget cycle registered.", before, Revision, subjectId: id));
        }

        public PoliticalOperationResult RegisterActionRegulation(GovernmentActionRegulationRequest request)
        {
            request ??= new GovernmentActionRegulationRequest();
            long before = Revision;
            string id = PoliticalModelUtility.Normalize(request.regulationId);
            if (TryDuplicate(request.transactionId, id, "register-action-regulation", before, out PoliticalOperationResult duplicate)) return duplicate;
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            string jurisdictionId = PoliticalModelUtility.Normalize(request.jurisdictionId);
            if (string.IsNullOrWhiteSpace(id) || actionRegulationsById.ContainsKey(id) || !governmentsById.ContainsKey(governmentId)) return Fail(PoliticalOperationCode.InvalidReference, "Action regulation ID or government is invalid.", before);
            if (!jurisdictionsById.TryGetValue(jurisdictionId, out JurisdictionRecordData jurisdiction) || jurisdiction.governmentId != governmentId) return Fail(PoliticalOperationCode.InvalidReference, "Action regulation jurisdiction is invalid.", before);
            if (!TryGetDefinition(request.requiredPermitDefinitionId, out GovernmentPermitDefinition permit) || !permit.PermittedActionIds.Contains(PoliticalModelUtility.Normalize(request.actionId))) return Fail(PoliticalOperationCode.MissingDefinition, "The required permit does not authorize this action.", before);
            GovernmentPermitHolderCategory[] categories = (request.regulatedHolderCategories ?? Array.Empty<GovernmentPermitHolderCategory>()).Where(value => value != GovernmentPermitHolderCategory.Unknown).Distinct().ToArray();
            if (categories.Length == 0 || request.expirationWorldTime >= 0d && request.expirationWorldTime <= request.effectiveWorldTime) return Fail(PoliticalOperationCode.InvalidRequest, "Regulated holder categories or dates are invalid.", before);
            GovernmentActionRegulationRecordData record = new GovernmentActionRegulationRecordData { regulationId = id, governmentId = governmentId, jurisdictionId = jurisdictionId, actionId = PoliticalModelUtility.Normalize(request.actionId), requiredPermitDefinitionId = permit.Id, regulatedHolderCategories = categories, exemptHolderIds = PoliticalModelUtility.Clean(request.exemptHolderIds), effectiveWorldTime = request.effectiveWorldTime, expirationWorldTime = request.expirationWorldTime, provenanceId = PoliticalModelUtility.Normalize(request.provenanceId) };
            if (request.preview) return PoliticalOperationResult.Success("Action regulation previewed.", before, before, preview: true, subjectId: id);
            actionRegulationsById.Add(id, record); CompleteTransaction(request.transactionId, "register-action-regulation", id); Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Action regulation registered.", before, Revision, subjectId: id));
        }

        public GovernmentPermitCheckResult EvaluateRegulatedAction(string holderId, GovernmentPermitHolderCategory holderCategory, string actionId, string jurisdictionId, double worldTime)
        {
            holderId = PoliticalModelUtility.Normalize(holderId); actionId = PoliticalModelUtility.Normalize(actionId); jurisdictionId = PoliticalModelUtility.Normalize(jurisdictionId);
            GovernmentActionRegulationRecordData regulation = actionRegulationsById.Values
                .Where(value => value.IsActiveAt(worldTime) && value.actionId == actionId && value.jurisdictionId == jurisdictionId)
                .Where(value => value.regulatedHolderCategories.Contains(holderCategory))
                .OrderBy(value => value.regulationId, StringComparer.Ordinal).FirstOrDefault();
            if (regulation == null || regulation.exemptHolderIds.Contains(holderId, StringComparer.Ordinal)) return GovernmentPermitCheckResult.NotRegulated();
            GovernmentPermitRecordData permit = permitsById.Values
                .Where(value => value.holderCategory == holderCategory && value.holderId == holderId && value.jurisdictionId == jurisdictionId)
                .Where(value => value.permitDefinitionId == regulation.requiredPermitDefinitionId && value.actionIds.Contains(actionId, StringComparer.Ordinal) && value.IsActiveAt(worldTime))
                .OrderBy(value => value.expirationWorldTime < 0d ? double.MaxValue : value.expirationWorldTime).ThenBy(value => value.permitId, StringComparer.Ordinal).FirstOrDefault();
            return permit == null ? GovernmentPermitCheckResult.Denied(regulation.regulationId, $"Action '{actionId}' requires permit '{regulation.requiredPermitDefinitionId}'.") : GovernmentPermitCheckResult.Granted(regulation.regulationId, permit.permitId);
        }

        public bool HasActivePermitDefinition(string holderId, GovernmentPermitHolderCategory holderCategory, string permitDefinitionId, string jurisdictionId, double worldTime)
        {
            holderId = PoliticalModelUtility.Normalize(holderId); permitDefinitionId = PoliticalModelUtility.Normalize(permitDefinitionId); jurisdictionId = PoliticalModelUtility.Normalize(jurisdictionId);
            return permitsById.Values.Any(value => value.holderId == holderId && value.holderCategory == holderCategory && value.permitDefinitionId == permitDefinitionId && (string.IsNullOrWhiteSpace(jurisdictionId) || value.jurisdictionId == jurisdictionId) && value.IsActiveAt(worldTime));
        }

        public PoliticalOperationResult EndOfficeTenure(GovernmentOfficeEndRequest request)
        {
            request ??= new GovernmentOfficeEndRequest();
            long before = Revision;
            string tenureId = PoliticalModelUtility.Normalize(request.tenureId);
            if (TryDuplicate(request.transactionId, tenureId, "end-office-tenure", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (!officeTenuresById.TryGetValue(tenureId, out GovernmentOfficeTenureRecordData tenure) || !tenure.IsActiveAt(request.worldTime)) return Fail(PoliticalOperationCode.InvalidState, "The office tenure is missing or not active.", before);
            if (request.cause == GovernmentOfficeVacancyCause.Unknown || request.cause == GovernmentOfficeVacancyCause.Succession) return Fail(PoliticalOperationCode.InvalidRequest, "A valid vacancy cause is required.", before);
            if (request.preview) return PoliticalOperationResult.Success("Office tenure ending previewed.", before, before, preview: true, subjectId: tenureId);
            EndTenureForVacancy(tenure, request.cause, request.worldTime, request.sourceEventId);
            CompleteTransaction(request.transactionId, "end-office-tenure", tenureId); Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Office tenure ended and succession evaluated.", before, Revision, subjectId: tenureId));
        }

        public PoliticalOperationResult ReportPersonUnavailable(string personId, GovernmentOfficeVacancyCause cause, double worldTime, string transactionId, string sourceEventId = "", bool preview = false)
        {
            personId = PoliticalModelUtility.Normalize(personId);
            GovernmentOfficeTenureRecordData[] active = officeTenuresById.Values.Where(value => value.holderPersonId == personId && value.IsActiveAt(worldTime)).ToArray();
            long before = Revision;
            if (active.Length == 0) return PoliticalOperationResult.Success("Person holds no active government office.", before, before, subjectId: personId);
            if (preview) return PoliticalOperationResult.Success($"{active.Length} office tenure(s) would end.", before, before, preview: true, subjectId: personId);
            foreach (GovernmentOfficeTenureRecordData tenure in active) EndTenureForVacancy(tenure, cause, worldTime, sourceEventId);
            CompleteTransaction(transactionId, "report-officeholder-unavailable", personId); Revision++;
            return PublishCommit(PoliticalOperationResult.Success($"{active.Length} office tenure(s) ended and succession was evaluated.", before, Revision, subjectId: personId));
        }

        public PoliticalOperationResult RecordLegitimacyEvent(GovernmentLegitimacyEventRequest request)
        {
            request ??= new GovernmentLegitimacyEventRequest();
            long before = Revision;
            string id = PoliticalModelUtility.Normalize(request.eventId);
            string governmentId = PoliticalModelUtility.Normalize(request.governmentId);
            if (TryDuplicate(request.transactionId, id, "record-legitimacy-event", before, out PoliticalOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(id) || legitimacyEventsById.ContainsKey(id) || !governmentsById.ContainsKey(governmentId) || request.component == GovernmentLegitimacyComponent.Unknown) return Fail(PoliticalOperationCode.InvalidReference, "Legitimacy event identity, government, or component is invalid.", before);
            if (request.deltaBasisPoints < -10000 || request.deltaBasisPoints > 10000) return Fail(PoliticalOperationCode.InvalidRequest, "Legitimacy delta must be between -10000 and 10000 basis points.", before);
            GovernmentLegitimacyRecordData prior = legitimacyAssessmentsById.Values.Where(value => value.governmentId == governmentId).OrderByDescending(value => value.assessedWorldTime).ThenByDescending(value => value.revision).FirstOrDefault();
            GovernmentLegitimacyEventRecordData record = new GovernmentLegitimacyEventRecordData { eventId = id, governmentId = governmentId, component = request.component, deltaBasisPoints = request.deltaBasisPoints, reason = request.reason ?? string.Empty, sourceRecordId = PoliticalModelUtility.Normalize(request.sourceRecordId), worldTime = request.worldTime };
            GovernmentLegitimacyRecordData assessment = null;
            if (prior != null && TryGetDefinition(prior.policyDefinitionId, out GovernmentLegitimacyPolicyDefinition policy))
            {
                GovernmentLegitimacyComponentScore[] components = prior.components.Select(value => value).ToArray();
                int index = Array.FindIndex(components, value => value.component == request.component);
                if (index >= 0) { components[index].scoreBasisPoints = Math.Max(0, Math.Min(10000, components[index].scoreBasisPoints + request.deltaBasisPoints)); components[index].sourceId = id; }
                int total = policy.Weights.Sum(weight => components.Where(value => value.component == weight.component).Select(value => value.scoreBasisPoints).DefaultIfEmpty(0).First() * weight.weightBasisPoints) / 10000;
                string assessmentId = $"government-legitimacy.event.{id}";
                assessment = new GovernmentLegitimacyRecordData { assessmentId = assessmentId, governmentId = governmentId, policyDefinitionId = prior.policyDefinitionId, components = components, totalScoreBasisPoints = Math.Max(0, Math.Min(10000, total)), band = policy.GetBand(total), assessedWorldTime = request.worldTime, assessorId = "system.government-events", provenanceId = id };
                record.resultingAssessmentId = assessmentId;
            }
            if (request.preview) return PoliticalOperationResult.Success("Legitimacy event previewed.", before, before, preview: true, subjectId: id);
            legitimacyEventsById.Add(id, record); if (assessment != null) legitimacyAssessmentsById[assessment.assessmentId] = assessment;
            CompleteTransaction(request.transactionId, "record-legitimacy-event", id); Revision++;
            return PublishCommit(PoliticalOperationResult.Success("Legitimacy event recorded and current assessment recalculated.", before, Revision, subjectId: id));
        }

        private string ProcessFiscalHierarchyWorldTime(double worldTime, bool preview)
        {
            GovernmentRemittancePolicyRecordData[] due = remittancePoliciesById.Values.Where(value => value.IsDue(worldTime)).OrderByDescending(value => GovernmentDepth(value.sourceGovernmentId)).ThenBy(value => value.policyId, StringComparer.Ordinal).ToArray();
            GovernmentBudgetCycleRecordData[] budgetsDue = budgetCyclesById.Values.Where(value => value.active && worldTime >= value.nextCycleWorldTime).OrderBy(value => value.cycleId, StringComparer.Ordinal).ToArray();
            if (preview) return $"{due.Length} intergovernmental remittance settlement(s) and {budgetsDue.Length} budget renewal(s) are due";
            int paid = 0, partial = 0, grace = 0, arrears = 0;
            foreach (GovernmentRemittancePolicyRecordData policy in due)
            {
                while (policy.IsDue(worldTime))
                {
                    double dueWorldTime = policy.nextSettlementWorldTime;
                    GovernmentRemittanceSettlementRecordData settlement = SettlePolicy(policy, worldTime, dueWorldTime);
                    if (settlement == null) break;
                    if (settlement.state == GovernmentRemittanceSettlementState.Paid) paid++;
                    else if (settlement.state == GovernmentRemittanceSettlementState.PartiallyPaid) partial++;
                    else if (settlement.state == GovernmentRemittanceSettlementState.OutstandingInGracePeriod) grace++;
                    else arrears++;
                }
            }
            int renewed = 0;
            foreach (GovernmentBudgetCycleRecordData cycle in budgetsDue)
            {
                while (cycle.active && worldTime >= cycle.nextCycleWorldTime)
                {
                    if (!RenewBudgetCycle(cycle, worldTime)) break;
                    renewed++;
                }
            }
            return $"{paid} remittance(s) paid, {partial} partially paid, {grace} within grace, {arrears} left in arrears, and {renewed} budget(s) renewed";
        }

        private bool RenewBudgetCycle(GovernmentBudgetCycleRecordData cycle, double worldTime)
        {
            if (resources == null) return false;
            int nextSequence = cycle.completedCycles + 1;
            double start = cycle.nextCycleWorldTime;
            string budgetId = $"{cycle.budgetIdPrefix}.cycle-{nextSequence:D4}";
            string priorBudgetId = cycle.currentBudgetId;
            OrganizationResourceOperationResult result = resources.CreateBudget(new OrganizationBudgetRequest
            {
                transactionId = $"government-budget-renewal.{cycle.cycleId}.{nextSequence:D4}", budgetId = budgetId,
                organizationId = cycle.organizationId, treasuryId = cycle.treasuryId, accountId = cycle.accountId,
                category = cycle.category, enforcementPolicy = cycle.enforcementPolicy, currencyDefinitionId = cycle.currencyDefinitionId,
                authorizedUnits = cycle.authorizedUnits, purpose = cycle.purpose, fundingSourceId = cycle.fundingSourceId,
                sourceAuthorityId = cycle.sourceAuthorityId, actorPersonId = cycle.actorPersonId,
                startWorldTime = start, endWorldTime = start + cycle.cycleDurationWorldTime, provenanceId = cycle.provenanceId
            });
            if (!result.Succeeded) return false;
            cycle.currentBudgetId = budgetId; cycle.completedCycles = nextSequence;
            cycle.nextCycleWorldTime += cycle.cycleDurationWorldTime;
            cycle.revision++; Revision++;
            GovernmentFiscalMandateRecordData mandate = fiscalMandatesById.Values.FirstOrDefault(value => value.governmentId == cycle.governmentId && value.organizationBudgetId != null && (value.organizationBudgetId == priorBudgetId || value.organizationBudgetId.StartsWith(cycle.budgetIdPrefix, StringComparison.Ordinal)));
            if (mandate != null) { mandate.organizationBudgetId = budgetId; mandate.revision++; }
            RecordLegitimacyEventInternal($"budget-renewal.{cycle.cycleId}.{nextSequence:D4}", cycle.governmentId, GovernmentLegitimacyComponent.AdministrativePerformance, 25, "Budget renewed", budgetId, worldTime);
            return true;
        }

        private GovernmentRemittanceSettlementRecordData SettlePolicy(GovernmentRemittancePolicyRecordData policy, double worldTime, double dueWorldTime)
        {
            string[] processedRevenue = policy.processedRevenueRecordIds ?? Array.Empty<string>();
            string[] processedIncoming = policy.processedIncomingRemittanceIds ?? Array.Empty<string>();
            InstitutionalRevenueRecordData[] revenueRecords = institutionalRevenue == null ? Array.Empty<InstitutionalRevenueRecordData>() : institutionalRevenue.RevenueRecords
                .Where(value => !processedRevenue.Contains(value.revenueRecordId, StringComparer.Ordinal))
                .Where(value => policy.eligibleRevenueDefinitionIds.Length == 0 || policy.eligibleRevenueDefinitionIds.Contains(value.revenueDefinitionId, StringComparer.Ordinal))
                .Where(value => value.recognizedWorldTime <= dueWorldTime)
                .Where(value => institutionalRevenue.Payments.Any(payment => payment.paymentId == value.sourcePaymentId && payment.institutionAccountId == policy.sourceEconomyAccountId))
                .ToArray();
            GovernmentRemittanceSettlementRecordData[] incoming = remittanceSettlementsById.Values
                .Where(value => value.destinationGovernmentId == policy.sourceGovernmentId && value.paidUnits > 0L && value.dueWorldTime <= dueWorldTime && !processedIncoming.Contains(value.settlementId, StringComparer.Ordinal)).ToArray();
            long eligibleUnits = revenueRecords.Sum(value => value.units) + incoming.Sum(value => value.paidUnits);
            long proportional = eligibleUnits <= 0L || policy.remittanceBasisPoints <= 0 ? 0L : checked(eligibleUnits * policy.remittanceBasisPoints / 10000L);
            long fixedDue = policy.fixedTributeUnits;
            foreach (GovernmentFiscalReliefRecordData relief in fiscalReliefsById.Values.Where(value => value.remittancePolicyId == policy.policyId && value.IsActiveAt(worldTime)))
            {
                proportional = proportional * Math.Max(0, 10000 - relief.reductionBasisPoints) / 10000L;
                fixedDue = Math.Max(0L, fixedDue - relief.waivedFixedUnits);
            }
            long newlyAssessed = checked(proportional + fixedDue);
            long totalDue = checked(newlyAssessed + policy.arrearsUnits);
            long available = 0L;
            if (economy != null && economy.TryGetAccount(policy.sourceEconomyAccountId, out EconomyAccountSnapshot account)) available = Math.Max(0L, account.BalanceUnits - policy.minimumReserveUnits);
            long paidUnits = Math.Min(totalDue, available);
            string settlementId = $"government-remittance.{policy.policyId}.{dueWorldTime:0}";
            string economyTransactionId = string.Empty;
            if (paidUnits > 0L && economy != null)
            {
                economyTransactionId = $"{settlementId}.transfer";
                EconomyOperationResult transfer = economy.Transfer(economyTransactionId, policy.sourceEconomyAccountId, policy.destinationEconomyAccountId, new MoneyAmount(policy.currencyDefinitionId, paidUnits), EconomyTransactionKind.Transfer, actorId: policy.sourceAuthorityId);
                if (!transfer.Succeeded) paidUnits = 0L;
            }
            long remaining = totalDue - paidUnits;
            bool withinGrace = remaining > 0L && policy.gracePeriodWorldTime > 0d && worldTime <= dueWorldTime + policy.gracePeriodWorldTime;
            GovernmentRemittanceSettlementState state = remaining == 0L ? GovernmentRemittanceSettlementState.Paid : withinGrace ? GovernmentRemittanceSettlementState.OutstandingInGracePeriod : paidUnits > 0L ? GovernmentRemittanceSettlementState.PartiallyPaid : GovernmentRemittanceSettlementState.InArrears;
            GovernmentRemittanceSettlementRecordData settlement = new GovernmentRemittanceSettlementRecordData
            {
                settlementId = settlementId, policyId = policy.policyId, sourceGovernmentId = policy.sourceGovernmentId, destinationGovernmentId = policy.destinationGovernmentId,
                sourceEconomyAccountId = policy.sourceEconomyAccountId, destinationEconomyAccountId = policy.destinationEconomyAccountId, currencyDefinitionId = policy.currencyDefinitionId,
                newlyAssessedUnits = newlyAssessed, priorArrearsUnits = policy.arrearsUnits, paidUnits = paidUnits, remainingArrearsUnits = remaining,
                sourceRevenueRecordIds = revenueRecords.Select(value => value.revenueRecordId).ToArray(), sourceRemittanceRecordIds = incoming.Select(value => value.settlementId).ToArray(),
                economyTransactionId = economyTransactionId, state = state, dueWorldTime = dueWorldTime, settledWorldTime = worldTime, provenanceId = policy.policyId
            };
            remittanceSettlementsById[settlementId] = settlement;
            policy.processedRevenueRecordIds = processedRevenue.Concat(settlement.sourceRevenueRecordIds).Distinct(StringComparer.Ordinal).ToArray();
            policy.processedIncomingRemittanceIds = processedIncoming.Concat(settlement.sourceRemittanceRecordIds).Distinct(StringComparer.Ordinal).ToArray();
            policy.arrearsUnits = remaining;
            policy.nextSettlementWorldTime += policy.settlementIntervalWorldTime;
            policy.revision++; Revision++;
            int delta = state == GovernmentRemittanceSettlementState.Paid ? 25 : state == GovernmentRemittanceSettlementState.OutstandingInGracePeriod ? 0 : state == GovernmentRemittanceSettlementState.PartiallyPaid ? -75 : -150;
            RecordLegitimacyEventInternal($"fiscal-settlement.{settlementId}", policy.sourceGovernmentId, GovernmentLegitimacyComponent.AdministrativePerformance, delta, state.ToString(), settlementId, worldTime);
            return settlement;
        }

        private void EndTenureForVacancy(GovernmentOfficeTenureRecordData tenure, GovernmentOfficeVacancyCause cause, double worldTime, string sourceEventId)
        {
            if (memberships != null)
            {
                memberships.TransitionOfficeAssignment(new OrganizationOfficeAssignmentTransitionRequest
                {
                    transactionId = $"government-office-assignment-end.{tenure.tenureId}.{worldTime:0}",
                    officeAssignmentId = tenure.officeAssignmentId,
                    targetState = cause == GovernmentOfficeVacancyCause.Removal ? OrganizationOfficeAssignmentState.Removed : OrganizationOfficeAssignmentState.Ended,
                    worldTime = worldTime,
                    sourceEventId = sourceEventId
                });
            }
            tenure.state = cause == GovernmentOfficeVacancyCause.Death ? GovernmentOfficeTenureState.Deceased : cause == GovernmentOfficeVacancyCause.Removal ? GovernmentOfficeTenureState.Removed : GovernmentOfficeTenureState.Ended;
            tenure.endWorldTime = worldTime; tenure.revision++;
            GovernmentOfficeTenureRecordData successor = officeTenuresById.Values.Where(value => value.governmentId == tenure.governmentId && value.officeDefinitionId == tenure.officeDefinitionId && value.state == GovernmentOfficeTenureState.Designated && value.tenureId != tenure.tenureId).OrderBy(value => value.startWorldTime).ThenBy(value => value.tenureId, StringComparer.Ordinal).FirstOrDefault();
            if (successor != null && memberships != null)
            {
                OrganizationMembershipOperationResult activated = memberships.TransitionOfficeAssignment(new OrganizationOfficeAssignmentTransitionRequest
                {
                    transactionId = $"government-office-assignment-activate.{successor.tenureId}.{worldTime:0}",
                    officeAssignmentId = successor.officeAssignmentId,
                    targetState = OrganizationOfficeAssignmentState.Active,
                    worldTime = worldTime,
                    sourceEventId = sourceEventId
                });
                if (!activated.Succeeded) successor = null;
            }
            string vacancyId = $"government-office-vacancy.{tenure.tenureId}";
            GovernmentOfficeVacancyRecordData vacancy = new GovernmentOfficeVacancyRecordData { vacancyId = vacancyId, governmentId = tenure.governmentId, officeDefinitionId = tenure.officeDefinitionId, priorTenureId = tenure.tenureId, priorHolderPersonId = tenure.holderPersonId, cause = cause, state = successor == null ? GovernmentOfficeVacancyState.Open : GovernmentOfficeVacancyState.SuccessorInstalled, successorTenureId = successor?.tenureId ?? string.Empty, openedWorldTime = worldTime, resolvedWorldTime = successor == null ? -1d : worldTime, sourceEventId = PoliticalModelUtility.Normalize(sourceEventId) };
            if (successor != null) { successor.state = GovernmentOfficeTenureState.Active; successor.startWorldTime = worldTime; successor.successionSourceId = vacancyId; successor.revision++; ReassignGovernmentOperationalAuthority(tenure, successor); }
            officeVacanciesById[vacancyId] = vacancy;
            RecordLegitimacyEventInternal($"office-vacancy.{vacancyId}", tenure.governmentId, GovernmentLegitimacyComponent.AdministrativePerformance, successor == null ? -300 : 100, cause.ToString(), vacancyId, worldTime);
        }

        private void ResolveVacancyForInstalledTenure(GovernmentOfficeTenureRecordData tenure, double worldTime)
        {
            GovernmentOfficeVacancyRecordData vacancy = officeVacanciesById.Values.Where(value => value.governmentId == tenure.governmentId && value.officeDefinitionId == tenure.officeDefinitionId && value.state == GovernmentOfficeVacancyState.Open).OrderByDescending(value => value.openedWorldTime).FirstOrDefault();
            if (vacancy == null) return;
            vacancy.state = tenure.state == GovernmentOfficeTenureState.Acting ? GovernmentOfficeVacancyState.ActingHolderInstalled : GovernmentOfficeVacancyState.SuccessorInstalled;
            vacancy.successorTenureId = tenure.tenureId; vacancy.resolvedWorldTime = worldTime; vacancy.revision++;
            if (officeTenuresById.TryGetValue(vacancy.priorTenureId, out GovernmentOfficeTenureRecordData priorTenure)) ReassignGovernmentOperationalAuthority(priorTenure, tenure);
            RecordLegitimacyEventInternal($"office-filled.{vacancy.vacancyId}.{tenure.tenureId}", tenure.governmentId, GovernmentLegitimacyComponent.AdministrativePerformance, 150, "Office vacancy filled", vacancy.vacancyId, worldTime);
        }

        private void ReassignGovernmentOperationalAuthority(GovernmentOfficeTenureRecordData priorTenure, GovernmentOfficeTenureRecordData successorTenure)
        {
            if (priorTenure == null || successorTenure == null || priorTenure.governmentId != successorTenure.governmentId) return;
            foreach (GovernmentBudgetCycleRecordData cycle in budgetCyclesById.Values.Where(value => value.governmentId == priorTenure.governmentId && (value.actorPersonId == priorTenure.holderPersonId || value.sourceAuthorityId == priorTenure.tenureId)))
            {
                cycle.actorPersonId = successorTenure.holderPersonId;
                cycle.sourceAuthorityId = successorTenure.tenureId;
                cycle.revision++;
            }
            foreach (GovernmentRemittancePolicyRecordData policy in remittancePoliciesById.Values.Where(value => value.sourceGovernmentId == priorTenure.governmentId && value.sourceAuthorityId == priorTenure.tenureId))
            {
                policy.sourceAuthorityId = successorTenure.tenureId;
                policy.revision++;
            }
            foreach (GovernmentFiscalMandateRecordData mandate in fiscalMandatesById.Values.Where(value => value.governmentId == priorTenure.governmentId && value.sourceAuthorityId == priorTenure.tenureId))
            {
                mandate.sourceAuthorityId = successorTenure.tenureId;
                mandate.revision++;
            }
        }

        private void RecordLegitimacyEventInternal(string eventId, string governmentId, GovernmentLegitimacyComponent component, int delta, string reason, string sourceId, double worldTime)
        {
            eventId = PoliticalModelUtility.Normalize(eventId);
            if (legitimacyEventsById.ContainsKey(eventId)) return;
            GovernmentLegitimacyRecordData prior = legitimacyAssessmentsById.Values.Where(value => value.governmentId == governmentId).OrderByDescending(value => value.assessedWorldTime).ThenByDescending(value => value.revision).FirstOrDefault();
            GovernmentLegitimacyEventRecordData record = new GovernmentLegitimacyEventRecordData { eventId = eventId, governmentId = governmentId, component = component, deltaBasisPoints = delta, reason = reason ?? string.Empty, sourceRecordId = PoliticalModelUtility.Normalize(sourceId), worldTime = worldTime };
            if (prior != null && TryGetDefinition(prior.policyDefinitionId, out GovernmentLegitimacyPolicyDefinition policy))
            {
                GovernmentLegitimacyComponentScore[] components = prior.components.ToArray(); int index = Array.FindIndex(components, value => value.component == component);
                if (index >= 0) { components[index].scoreBasisPoints = Math.Max(0, Math.Min(10000, components[index].scoreBasisPoints + delta)); components[index].sourceId = eventId; }
                int total = policy.Weights.Sum(weight => components.Where(value => value.component == weight.component).Select(value => value.scoreBasisPoints).DefaultIfEmpty(0).First() * weight.weightBasisPoints) / 10000;
                record.resultingAssessmentId = $"government-legitimacy.event.{eventId}";
                legitimacyAssessmentsById[record.resultingAssessmentId] = new GovernmentLegitimacyRecordData { assessmentId = record.resultingAssessmentId, governmentId = governmentId, policyDefinitionId = prior.policyDefinitionId, components = components, totalScoreBasisPoints = total, band = policy.GetBand(total), assessedWorldTime = worldTime, assessorId = "system.government-events", provenanceId = eventId };
            }
            legitimacyEventsById[eventId] = record;
        }

        private int GovernmentDepth(string governmentId)
        {
            int depth = 0; HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal); string cursor = governmentId;
            while (!string.IsNullOrWhiteSpace(cursor) && visited.Add(cursor) && governmentsById.TryGetValue(cursor, out GovernmentRecordData government)) { cursor = government.parentGovernmentId; depth++; }
            return depth;
        }

        private static bool ValidateFiscalHierarchySaveData(GovernmentRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, ISet<string> governmentIds, ISet<string> jurisdictionIds, out string failure)
        {
            failure = string.Empty;
            if (!UniqueIds(saveData.remittancePolicies?.Select(value => value?.policyId), "remittance policy", out failure)
                || !UniqueIds(saveData.remittanceSettlements?.Select(value => value?.settlementId), "remittance settlement", out failure)
                || !UniqueIds(saveData.fiscalReliefs?.Select(value => value?.reliefId), "fiscal relief", out failure)
                || !UniqueIds(saveData.actionRegulations?.Select(value => value?.regulationId), "action regulation", out failure)
                || !UniqueIds(saveData.officeVacancies?.Select(value => value?.vacancyId), "office vacancy", out failure)
                || !UniqueIds(saveData.legitimacyEvents?.Select(value => value?.eventId), "legitimacy event", out failure)) return false;
            if (!UniqueIds(saveData.budgetCycles?.Select(value => value?.cycleId), "budget cycle", out failure)) return false;
            HashSet<string> policyIds = new HashSet<string>((saveData.remittancePolicies ?? Array.Empty<GovernmentRemittancePolicyRecordData>()).Select(value => PoliticalModelUtility.Normalize(value.policyId)), StringComparer.Ordinal);
            foreach (GovernmentRemittancePolicyRecordData policy in saveData.remittancePolicies ?? Array.Empty<GovernmentRemittancePolicyRecordData>())
            {
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(policy.sourceGovernmentId)) || !governmentIds.Contains(PoliticalModelUtility.Normalize(policy.destinationGovernmentId))) { failure = $"Remittance policy '{policy.policyId}' references a missing government."; return false; }
                if (policy.remittanceBasisPoints < 0 || policy.remittanceBasisPoints > 10000 || policy.fixedTributeUnits < 0L || policy.minimumReserveUnits < 0L || policy.settlementIntervalWorldTime <= 0d || policy.arrearsUnits < 0L) { failure = $"Remittance policy '{policy.policyId}' contains invalid fiscal values."; return false; }
                if ((policy.eligibleRevenueDefinitionIds ?? Array.Empty<string>()).Any(id => definitionRegistry != null && !definitionRegistry.TryGet(id, out InstitutionalRevenueDefinition _))) { failure = $"Remittance policy '{policy.policyId}' references a missing revenue definition."; return false; }
            }
            foreach (GovernmentRemittanceSettlementRecordData settlement in saveData.remittanceSettlements ?? Array.Empty<GovernmentRemittanceSettlementRecordData>()) if (!policyIds.Contains(PoliticalModelUtility.Normalize(settlement.policyId)) || settlement.paidUnits < 0L || settlement.remainingArrearsUnits < 0L) { failure = $"Remittance settlement '{settlement.settlementId}' is invalid."; return false; }
            foreach (GovernmentFiscalReliefRecordData relief in saveData.fiscalReliefs ?? Array.Empty<GovernmentFiscalReliefRecordData>()) if (!policyIds.Contains(PoliticalModelUtility.Normalize(relief.remittancePolicyId)) || relief.reductionBasisPoints < 0 || relief.reductionBasisPoints > 10000) { failure = $"Fiscal relief '{relief.reliefId}' is invalid."; return false; }
            foreach (GovernmentActionRegulationRecordData regulation in saveData.actionRegulations ?? Array.Empty<GovernmentActionRegulationRecordData>())
            {
                if (!governmentIds.Contains(PoliticalModelUtility.Normalize(regulation.governmentId)) || !jurisdictionIds.Contains(PoliticalModelUtility.Normalize(regulation.jurisdictionId))) { failure = $"Action regulation '{regulation.regulationId}' references a missing government or jurisdiction."; return false; }
                if (definitionRegistry != null && !definitionRegistry.TryGet(regulation.requiredPermitDefinitionId, out GovernmentPermitDefinition _)) { failure = $"Action regulation '{regulation.regulationId}' references a missing permit definition."; return false; }
            }
            foreach (GovernmentOfficeVacancyRecordData vacancy in saveData.officeVacancies ?? Array.Empty<GovernmentOfficeVacancyRecordData>()) if (!governmentIds.Contains(PoliticalModelUtility.Normalize(vacancy.governmentId))) { failure = $"Office vacancy '{vacancy.vacancyId}' references a missing government."; return false; }
            foreach (GovernmentLegitimacyEventRecordData legitimacyEvent in saveData.legitimacyEvents ?? Array.Empty<GovernmentLegitimacyEventRecordData>()) if (!governmentIds.Contains(PoliticalModelUtility.Normalize(legitimacyEvent.governmentId))) { failure = $"Legitimacy event '{legitimacyEvent.eventId}' references a missing government."; return false; }
            foreach (GovernmentBudgetCycleRecordData cycle in saveData.budgetCycles ?? Array.Empty<GovernmentBudgetCycleRecordData>()) if (!governmentIds.Contains(PoliticalModelUtility.Normalize(cycle.governmentId)) || cycle.authorizedUnits < 0L || cycle.cycleDurationWorldTime <= 0d) { failure = $"Budget cycle '{cycle.cycleId}' is invalid."; return false; }
            return true;
        }
    }
}
