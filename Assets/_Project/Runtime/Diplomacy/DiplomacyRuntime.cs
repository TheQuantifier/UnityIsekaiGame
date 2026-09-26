using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Organizations;

namespace UnityIsekaiGame.Diplomacy
{
    public sealed class DiplomacyRuntime : IDisposable
    {
        private readonly Dictionary<string, DiplomaticRelationRecordData> relationsById = new Dictionary<string, DiplomaticRelationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticAgreementRecordData> agreementsById = new Dictionary<string, DiplomaticAgreementRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticClauseRecordData> clausesById = new Dictionary<string, DiplomaticClauseRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticSignatureRecordData> signaturesById = new Dictionary<string, DiplomaticSignatureRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticRatificationRecordData> ratificationsById = new Dictionary<string, DiplomaticRatificationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticBreachRecordData> breachesById = new Dictionary<string, DiplomaticBreachRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticWarRecordData> warsById = new Dictionary<string, DiplomaticWarRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticWarSideRecordData> warSidesById = new Dictionary<string, DiplomaticWarSideRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticWarParticipationRecordData> warParticipationsById = new Dictionary<string, DiplomaticWarParticipationRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticIncidentRecordData> incidentsById = new Dictionary<string, DiplomaticIncidentRecordData>(StringComparer.Ordinal);
        private readonly Dictionary<string, DiplomaticTransactionRecordData> transactionsById = new Dictionary<string, DiplomaticTransactionRecordData>(StringComparer.Ordinal);

        private DefinitionRegistry registry;
        private OrganizationRuntime organizations;
        private FactionRuntime factions;
        private OrganizationAuthorityRuntime authority;
        private OrganizationDecisionRuntime decisions;
        private OrganizationResourceRuntime resources;
        private string worldId = string.Empty;
        private string[] knownPersonIds = Array.Empty<string>();

        public long Revision { get; private set; }
        public int RelationCount => relationsById.Count;
        public int AgreementCount => agreementsById.Count;
        public int WarCount => warsById.Count;
        public IReadOnlyList<DiplomaticRelationRecordData> Relations => relationsById.Values.OrderBy(item => item.startWorldTime).ThenBy(item => item.relationId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<DiplomaticAgreementRecordData> Agreements => agreementsById.Values.OrderBy(item => item.draftedWorldTime).ThenBy(item => item.agreementId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();
        public IReadOnlyList<DiplomaticWarRecordData> Wars => warsById.Values.OrderBy(item => item.declaredWorldTime).ThenBy(item => item.warId, StringComparer.Ordinal).Select(item => item.Clone()).ToArray();

        public void Configure(DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, FactionRuntime factionRuntime, OrganizationAuthorityRuntime authorityRuntime, OrganizationDecisionRuntime decisionRuntime, OrganizationResourceRuntime resourceRuntime, string runtimeWorldId, IEnumerable<string> personIds)
        {
            registry = definitionRegistry;
            organizations = organizationRuntime;
            factions = factionRuntime;
            authority = authorityRuntime;
            decisions = decisionRuntime;
            resources = resourceRuntime;
            worldId = DiplomacyModelUtility.Normalize(runtimeWorldId);
            knownPersonIds = DiplomacyModelUtility.Clean(personIds);
        }

        public bool TryGetRelation(string relationId, out DiplomaticRelationRecordData relation)
        {
            if (relationsById.TryGetValue(DiplomacyModelUtility.Normalize(relationId), out DiplomaticRelationRecordData found))
            {
                relation = found.Clone();
                return true;
            }

            relation = null;
            return false;
        }

        public bool TryGetAgreement(string agreementId, out DiplomaticAgreementRecordData agreement)
        {
            if (agreementsById.TryGetValue(DiplomacyModelUtility.Normalize(agreementId), out DiplomaticAgreementRecordData found))
            {
                agreement = found.Clone();
                return true;
            }

            agreement = null;
            return false;
        }

        public bool TryGetWar(string warId, out DiplomaticWarRecordData war)
        {
            if (warsById.TryGetValue(DiplomacyModelUtility.Normalize(warId), out DiplomaticWarRecordData found))
            {
                war = found.Clone();
                return true;
            }

            war = null;
            return false;
        }

        public DiplomacyOperationResult CreateRelation(DiplomaticRelationRequest request)
        {
            request ??= new DiplomaticRelationRequest();
            long before = Revision;
            string relationId = DiplomacyModelUtility.Normalize(request.relationId);
            string transactionId = DiplomacyModelUtility.Normalize(request.transactionId);
            if (TryDuplicate(transactionId, relationId, "create-relation", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(relationId)) return Fail(DiplomaticOperationCode.InvalidRequest, "Diplomatic relation ID is required.", before);
            if (relationsById.ContainsKey(relationId)) return Fail(DiplomaticOperationCode.InvalidRequest, $"Diplomatic relation '{relationId}' already exists with different transaction identity.", before);
            if (!TryGetDefinition(request.relationDefinitionId, out DiplomaticRelationDefinition definition)) return Fail(DiplomaticOperationCode.MissingDefinition, $"Diplomatic Relation definition '{request.relationDefinitionId}' is missing.", before);
            DiplomaticActorReferenceData source = NormalizeActor(request.sourceActor);
            DiplomaticActorReferenceData target = NormalizeActor(request.targetActor);
            if (!ValidateActor(source, DiplomaticOperationKind.CreateRelation, out string sourceFailure)) return Fail(DiplomaticOperationCode.ActorIneligible, sourceFailure, before);
            if (!ValidateActor(target, DiplomaticOperationKind.CreateRelation, out string targetFailure)) return Fail(DiplomaticOperationCode.ActorIneligible, targetFailure, before);
            if (source.Equals(target)) return Fail(DiplomaticOperationCode.InvalidRequest, "Diplomatic relation cannot target the same actor.", before);
            if (!ValidateAuthoritySource(source, request.sourceAuthorityGrantId, out string authorityFailure)) return Fail(DiplomaticOperationCode.InvalidAuthority, authorityFailure, before);

            DiplomaticRelationRecordData record = new DiplomaticRelationRecordData
            {
                relationId = relationId,
                relationDefinitionId = definition.Id,
                sourceActor = source,
                targetActor = target,
                category = request.category == DiplomaticRelationCategory.Unknown ? definition.Category : request.category,
                lifecycleState = request.lifecycleState == DiplomaticLifecycleState.Unknown ? DiplomaticLifecycleState.Active : request.lifecycleState,
                startWorldTime = request.worldTime,
                sourceAgreementId = DiplomacyModelUtility.Normalize(request.sourceAgreementId),
                sourceWarId = DiplomacyModelUtility.Normalize(request.sourceWarId),
                sourceDecisionId = DiplomacyModelUtility.Normalize(request.sourceDecisionId),
                sourceAuthorityGrantId = DiplomacyModelUtility.Normalize(request.sourceAuthorityGrantId),
                visibility = request.visibility,
                publicSummary = request.publicSummary ?? string.Empty,
                tags = new[] { "diplomacy", "relation" },
                revision = 1L
            };

            if (request.preview) return Success("Diplomatic relation previewed.", before, before, preview: true, subjectId: relationId, relation: record);
            relationsById[relationId] = record;
            CompleteTransaction(transactionId, "create-relation", relationId);
            if (definition.ReciprocityPolicy == DiplomaticReciprocityPolicy.MirrorOnCreate)
            {
                string mirrorId = $"{relationId}.reciprocal";
                if (!relationsById.ContainsKey(mirrorId))
                {
                    relationsById[mirrorId] = new DiplomaticRelationRecordData
                    {
                        relationId = mirrorId,
                        relationDefinitionId = definition.Id,
                        sourceActor = target.Clone(),
                        targetActor = source.Clone(),
                        category = record.category,
                        lifecycleState = record.lifecycleState,
                        startWorldTime = record.startWorldTime,
                        sourceAgreementId = record.sourceAgreementId,
                        sourceWarId = record.sourceWarId,
                        sourceDecisionId = record.sourceDecisionId,
                        sourceAuthorityGrantId = record.sourceAuthorityGrantId,
                        visibility = record.visibility,
                        publicSummary = record.publicSummary,
                        tags = new[] { "diplomacy", "relation", "reciprocal" },
                        revision = 1L
                    };
                }
            }

            Revision++;
            return Success("Diplomatic relation recorded.", before, Revision, subjectId: relationId, relation: record);
        }

        public DiplomacyOperationResult TransitionRelation(string transactionId, string relationId, DiplomaticLifecycleState targetState, double worldTime, string sourceDecisionId = "", bool preview = false)
        {
            long before = Revision;
            relationId = DiplomacyModelUtility.Normalize(relationId);
            if (TryDuplicate(transactionId, relationId, "transition-relation", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (!relationsById.TryGetValue(relationId, out DiplomaticRelationRecordData record)) return Fail(DiplomaticOperationCode.MissingRelation, $"Diplomatic relation '{relationId}' is missing.", before);
            if (!Enum.IsDefined(typeof(DiplomaticLifecycleState), targetState) || targetState == DiplomaticLifecycleState.Unknown) return Fail(DiplomaticOperationCode.InvalidState, "Target relation state is invalid.", before);
            DiplomaticRelationRecordData changed = record.Clone();
            changed.lifecycleState = targetState;
            changed.endWorldTime = targetState == DiplomaticLifecycleState.Ended || targetState == DiplomaticLifecycleState.Historical || targetState == DiplomaticLifecycleState.Superseded ? worldTime : changed.endWorldTime;
            changed.sourceDecisionId = string.IsNullOrWhiteSpace(sourceDecisionId) ? changed.sourceDecisionId : sourceDecisionId.Trim();
            changed.revision++;
            if (preview) return Success("Diplomatic relation transition previewed.", before, before, preview: true, subjectId: relationId, relation: changed);
            relationsById[relationId] = changed;
            CompleteTransaction(transactionId, "transition-relation", relationId);
            Revision++;
            return Success("Diplomatic relation transitioned.", before, Revision, subjectId: relationId, relation: changed);
        }

        public DiplomacyOperationResult CreateAgreement(DiplomaticAgreementRequest request)
        {
            request ??= new DiplomaticAgreementRequest();
            long before = Revision;
            string agreementId = DiplomacyModelUtility.Normalize(request.agreementId);
            if (TryDuplicate(request.transactionId, agreementId, "create-agreement", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(agreementId)) return Fail(DiplomaticOperationCode.InvalidRequest, "Diplomatic agreement ID is required.", before);
            if (agreementsById.ContainsKey(agreementId)) return Fail(DiplomaticOperationCode.InvalidRequest, $"Diplomatic agreement '{agreementId}' already exists with different transaction identity.", before);
            if (!TryGetDefinition(request.agreementDefinitionId, out DiplomaticAgreementDefinition definition)) return Fail(DiplomaticOperationCode.MissingDefinition, $"Diplomatic Agreement definition '{request.agreementDefinitionId}' is missing.", before);

            DiplomaticAgreementPartyRecordData[] parties = (request.parties ?? Array.Empty<DiplomaticAgreementPartyRecordData>())
                .Select(item => item?.Clone())
                .Where(item => item != null)
                .ToArray();
            if (parties.Count(item => item.role == DiplomaticPartyRole.Principal) < definition.MinimumPrincipalParties) return Fail(DiplomaticOperationCode.InvalidRequest, $"Diplomatic agreement '{agreementId}' requires at least {definition.MinimumPrincipalParties} principal parties.", before);
            foreach (DiplomaticAgreementPartyRecordData party in parties)
            {
                party.partyId = string.IsNullOrWhiteSpace(party.partyId) ? $"{agreementId}.party.{party.actor?.StableKey}".Replace(':', '-') : party.partyId.Trim();
                party.actor = NormalizeActor(party.actor);
                if (!ValidateActor(party.actor, DiplomaticOperationKind.DraftAgreement, out string actorFailure)) return Fail(DiplomaticOperationCode.ActorIneligible, actorFailure, before);
                if (!ValidateAuthoritySource(party.actor, party.sourceAuthorityGrantId, out string authorityFailure)) return Fail(DiplomaticOperationCode.InvalidAuthority, authorityFailure, before);
            }

            DiplomaticClauseRecordData[] clauses = (request.clauses ?? Array.Empty<DiplomaticClauseRecordData>())
                .Select(item => item?.Clone())
                .Where(item => item != null)
                .ToArray();
            foreach (DiplomaticClauseRecordData clause in clauses)
            {
                clause.agreementId = agreementId;
                clause.clauseId = string.IsNullOrWhiteSpace(clause.clauseId) ? $"{agreementId}.clause.{clauses.ToList().IndexOf(clause) + 1:00}" : clause.clauseId.Trim();
                if (!TryGetDefinition(clause.clauseDefinitionId, out DiplomaticClauseDefinition clauseDefinition)) return Fail(DiplomaticOperationCode.MissingDefinition, $"Diplomatic Clause definition '{clause.clauseDefinitionId}' is missing.", before);
                if (definition.AllowedClauseDefinitionIds.Count > 0 && !definition.AllowedClauseDefinitionIds.Contains(clauseDefinition.Id)) return Fail(DiplomaticOperationCode.InvalidRequest, $"Clause '{clauseDefinition.Id}' is not allowed by agreement '{definition.Id}'.", before);
                if (DiplomacyModelUtility.IsSecret(clause.visibility) && !definition.PermitsSecretClauses) return Fail(DiplomaticOperationCode.InvalidRequest, $"Agreement '{definition.Id}' does not permit secret clauses.", before);
                clause.category = clause.category == DiplomaticClauseCategory.Unknown ? clauseDefinition.Category : clause.category;
                clause.lifecycleState = clause.lifecycleState == DiplomaticClauseLifecycleState.Unknown ? DiplomaticClauseLifecycleState.Active : clause.lifecycleState;
            }

            DiplomaticAgreementRecordData record = new DiplomaticAgreementRecordData
            {
                agreementId = agreementId,
                agreementDefinitionId = definition.Id,
                title = string.IsNullOrWhiteSpace(request.title) ? definition.DisplayName : request.title.Trim(),
                category = definition.Category,
                lifecycleState = request.initialState == DiplomaticAgreementLifecycleState.Unknown ? DiplomaticAgreementLifecycleState.Draft : request.initialState,
                visibility = request.visibility,
                parties = parties.OrderBy(item => item.partyId, StringComparer.Ordinal).ToArray(),
                clauseIds = DiplomacyModelUtility.Clean(clauses.Select(item => item.clauseId)),
                sourceProposalId = DiplomacyModelUtility.Normalize(request.sourceProposalId),
                sourceResolutionId = DiplomacyModelUtility.Normalize(request.sourceResolutionId),
                sourceContractId = DiplomacyModelUtility.Normalize(request.sourceContractId),
                draftedWorldTime = request.worldTime,
                effectiveWorldTime = request.initialState == DiplomaticAgreementLifecycleState.Active ? request.worldTime : -1d,
                expirationWorldTime = -1d,
                revision = 1L
            };

            if (request.preview) return Success("Diplomatic agreement previewed.", before, before, preview: true, subjectId: agreementId, agreement: record);
            agreementsById[agreementId] = record;
            foreach (DiplomaticClauseRecordData clause in clauses) clausesById[clause.clauseId] = clause;
            CompleteTransaction(request.transactionId, "create-agreement", agreementId);
            Revision++;
            return Success("Diplomatic agreement recorded.", before, Revision, subjectId: agreementId, agreement: record);
        }

        public DiplomacyOperationResult SignAgreement(DiplomaticSignatureRequest request)
        {
            request ??= new DiplomaticSignatureRequest();
            long before = Revision;
            string agreementId = DiplomacyModelUtility.Normalize(request.agreementId);
            string signatureId = DiplomacyModelUtility.Normalize(request.signatureId);
            if (TryDuplicate(request.transactionId, signatureId, "sign-agreement", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (!agreementsById.TryGetValue(agreementId, out DiplomaticAgreementRecordData agreement)) return Fail(DiplomaticOperationCode.MissingAgreement, $"Diplomatic agreement '{agreementId}' is missing.", before);
            DiplomaticAgreementPartyRecordData party = agreement.parties.FirstOrDefault(item => string.Equals(item.partyId, request.partyId, StringComparison.Ordinal));
            if (party == null) return Fail(DiplomaticOperationCode.MissingParty, $"Diplomatic agreement party '{request.partyId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.authorityGrantId) && !ValidateAuthoritySource(party.actor, request.authorityGrantId, out string authorityFailure)) return Fail(DiplomaticOperationCode.InvalidAuthority, authorityFailure, before);
            if (!string.IsNullOrWhiteSpace(request.signerPersonId) && knownPersonIds.Length > 0 && !knownPersonIds.Contains(request.signerPersonId)) return Fail(DiplomaticOperationCode.InvalidAuthority, $"Signer Person '{request.signerPersonId}' is not known.", before);
            if (string.IsNullOrWhiteSpace(signatureId)) signatureId = $"{agreementId}.signature.{party.partyId}";
            if (signaturesById.ContainsKey(signatureId)) return Fail(DiplomaticOperationCode.InvalidRequest, $"Diplomatic signature '{signatureId}' already exists with different transaction identity.", before);
            DiplomaticSignatureRecordData signature = new DiplomaticSignatureRecordData { signatureId = signatureId, agreementId = agreementId, partyId = party.partyId, signerPersonId = request.signerPersonId ?? string.Empty, authorityGrantId = request.authorityGrantId ?? string.Empty, status = DiplomaticSignatureStatus.Signed, signedWorldTime = request.worldTime, revision = 1L };
            if (request.preview) return Success("Diplomatic signature previewed.", before, before, preview: true, subjectId: signatureId, agreement: agreement);
            signaturesById[signatureId] = signature;
            if (agreement.lifecycleState == DiplomaticAgreementLifecycleState.Draft || agreement.lifecycleState == DiplomaticAgreementLifecycleState.Negotiating) agreement.lifecycleState = DiplomaticAgreementLifecycleState.Signed;
            agreement.revision++;
            CompleteTransaction(request.transactionId, "sign-agreement", signatureId);
            Revision++;
            return Success("Diplomatic agreement signed.", before, Revision, subjectId: signatureId, agreement: agreement);
        }

        public DiplomacyOperationResult RatifyAgreement(string transactionId, string agreementId, string partyId, string sourceDecisionId, double worldTime, bool preview = false)
        {
            long before = Revision;
            agreementId = DiplomacyModelUtility.Normalize(agreementId);
            partyId = DiplomacyModelUtility.Normalize(partyId);
            string ratificationId = $"{agreementId}.ratification.{partyId}";
            if (TryDuplicate(transactionId, ratificationId, "ratify-agreement", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (!agreementsById.TryGetValue(agreementId, out DiplomaticAgreementRecordData agreement)) return Fail(DiplomaticOperationCode.MissingAgreement, $"Diplomatic agreement '{agreementId}' is missing.", before);
            if (!agreement.parties.Any(item => string.Equals(item.partyId, partyId, StringComparison.Ordinal))) return Fail(DiplomaticOperationCode.MissingParty, $"Diplomatic agreement party '{partyId}' is missing.", before);
            DiplomaticRatificationRecordData ratification = new DiplomaticRatificationRecordData { ratificationId = ratificationId, agreementId = agreementId, partyId = partyId, sourceDecisionId = sourceDecisionId ?? string.Empty, status = DiplomaticRatificationStatus.Ratified, ratifiedWorldTime = worldTime };
            if (preview) return Success("Diplomatic ratification previewed.", before, before, preview: true, subjectId: ratificationId, agreement: agreement);
            ratificationsById[ratificationId] = ratification;
            agreement.lifecycleState = DiplomaticAgreementLifecycleState.Ratified;
            agreement.revision++;
            CompleteTransaction(transactionId, "ratify-agreement", ratificationId);
            Revision++;
            return Success("Diplomatic agreement ratified.", before, Revision, subjectId: ratificationId, agreement: agreement);
        }

        public DiplomacyOperationResult ActivateAgreement(string transactionId, string agreementId, double worldTime, bool preview = false)
        {
            long before = Revision;
            agreementId = DiplomacyModelUtility.Normalize(agreementId);
            if (TryDuplicate(transactionId, agreementId, "activate-agreement", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (!agreementsById.TryGetValue(agreementId, out DiplomaticAgreementRecordData agreement)) return Fail(DiplomaticOperationCode.MissingAgreement, $"Diplomatic agreement '{agreementId}' is missing.", before);
            DiplomaticAgreementRecordData changed = agreement.Clone();
            changed.lifecycleState = DiplomaticAgreementLifecycleState.Active;
            changed.effectiveWorldTime = worldTime;
            changed.revision++;
            if (preview) return Success("Diplomatic agreement activation previewed.", before, before, preview: true, subjectId: agreementId, agreement: changed);
            agreementsById[agreementId] = changed;
            foreach (string clauseId in changed.clauseIds)
            {
                if (clausesById.TryGetValue(clauseId, out DiplomaticClauseRecordData clause) && clause.lifecycleState == DiplomaticClauseLifecycleState.Draft)
                {
                    clause.lifecycleState = DiplomaticClauseLifecycleState.Active;
                    clause.effectiveWorldTime = worldTime;
                    clause.revision++;
                }
            }
            CompleteTransaction(transactionId, "activate-agreement", agreementId);
            Revision++;
            return Success("Diplomatic agreement activated.", before, Revision, subjectId: agreementId, agreement: changed);
        }

        public DiplomacyOperationResult TerminateAgreement(string transactionId, string agreementId, DiplomaticAgreementLifecycleState terminalState, double worldTime, string sourceDecisionId = "", bool preview = false)
        {
            long before = Revision;
            agreementId = DiplomacyModelUtility.Normalize(agreementId);
            if (TryDuplicate(transactionId, agreementId, "terminate-agreement", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (!agreementsById.TryGetValue(agreementId, out DiplomaticAgreementRecordData agreement)) return Fail(DiplomaticOperationCode.MissingAgreement, $"Diplomatic agreement '{agreementId}' is missing.", before);
            if (terminalState != DiplomaticAgreementLifecycleState.Suspended && terminalState != DiplomaticAgreementLifecycleState.Terminated && terminalState != DiplomaticAgreementLifecycleState.Expired && terminalState != DiplomaticAgreementLifecycleState.Withdrawn && terminalState != DiplomaticAgreementLifecycleState.Superseded) return Fail(DiplomaticOperationCode.InvalidState, "Agreement terminal state is invalid.", before);
            DiplomaticAgreementRecordData changed = agreement.Clone();
            changed.lifecycleState = terminalState;
            changed.expirationWorldTime = terminalState == DiplomaticAgreementLifecycleState.Expired ? worldTime : changed.expirationWorldTime;
            changed.sourceResolutionId = string.IsNullOrWhiteSpace(sourceDecisionId) ? changed.sourceResolutionId : sourceDecisionId.Trim();
            changed.revision++;
            if (preview) return Success("Diplomatic agreement termination previewed.", before, before, preview: true, subjectId: agreementId, agreement: changed);
            agreementsById[agreementId] = changed;
            foreach (string clauseId in changed.clauseIds)
            {
                if (clausesById.TryGetValue(clauseId, out DiplomaticClauseRecordData clause) && clause.lifecycleState == DiplomaticClauseLifecycleState.Active)
                {
                    clause.lifecycleState = terminalState == DiplomaticAgreementLifecycleState.Suspended ? DiplomaticClauseLifecycleState.Suspended : DiplomaticClauseLifecycleState.Terminated;
                    clause.revision++;
                }
            }
            CompleteTransaction(transactionId, "terminate-agreement", agreementId);
            Revision++;
            return Success("Diplomatic agreement state changed.", before, Revision, subjectId: agreementId, agreement: changed);
        }

        public DiplomacyOperationResult RecordBreach(DiplomaticBreachRequest request)
        {
            request ??= new DiplomaticBreachRequest();
            long before = Revision;
            string breachId = DiplomacyModelUtility.Normalize(request.breachId);
            if (TryDuplicate(request.transactionId, breachId, "record-breach", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(breachId)) return Fail(DiplomaticOperationCode.InvalidRequest, "Diplomatic breach ID is required.", before);
            if (breachesById.ContainsKey(breachId)) return Fail(DiplomaticOperationCode.InvalidRequest, $"Diplomatic breach '{breachId}' already exists with different transaction identity.", before);
            string agreementId = DiplomacyModelUtility.Normalize(request.agreementId);
            string clauseId = DiplomacyModelUtility.Normalize(request.clauseId);
            if (!agreementsById.ContainsKey(agreementId)) return Fail(DiplomaticOperationCode.MissingAgreement, $"Diplomatic agreement '{agreementId}' is missing.", before);
            if (!clausesById.ContainsKey(clauseId)) return Fail(DiplomaticOperationCode.MissingClause, $"Diplomatic clause '{clauseId}' is missing.", before);
            DiplomaticActorReferenceData actor = NormalizeActor(request.allegedActor);
            if (!ValidateActor(actor, DiplomaticOperationKind.RecordBreach, out string actorFailure)) return Fail(DiplomaticOperationCode.ActorIneligible, actorFailure, before);
            DiplomaticBreachRecordData breach = new DiplomaticBreachRecordData { breachId = breachId, agreementId = agreementId, clauseId = clauseId, allegedActor = actor, state = request.state == DiplomaticBreachState.Unknown ? DiplomaticBreachState.Alleged : request.state, reportedWorldTime = request.worldTime, sourceEventId = request.sourceEventId ?? string.Empty, sourceRecordId = request.sourceRecordId ?? string.Empty, notes = request.notes ?? string.Empty };
            if (request.preview) return Success("Diplomatic breach previewed.", before, before, preview: true, subjectId: breachId);
            breachesById[breachId] = breach;
            if (breach.state == DiplomaticBreachState.Confirmed && clausesById.TryGetValue(clauseId, out DiplomaticClauseRecordData clause))
            {
                clause.lifecycleState = DiplomaticClauseLifecycleState.Breached;
                clause.revision++;
            }
            CompleteTransaction(request.transactionId, "record-breach", breachId);
            Revision++;
            return Success("Diplomatic breach recorded.", before, Revision, subjectId: breachId);
        }

        public DiplomacyOperationResult DeclareWar(DiplomaticWarDeclarationRequest request)
        {
            request ??= new DiplomaticWarDeclarationRequest();
            long before = Revision;
            string warId = DiplomacyModelUtility.Normalize(request.warId);
            if (TryDuplicate(request.transactionId, warId, "declare-war", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(warId)) return Fail(DiplomaticOperationCode.InvalidRequest, "Diplomatic war ID is required.", before);
            if (warsById.ContainsKey(warId)) return Fail(DiplomaticOperationCode.InvalidRequest, $"Diplomatic war '{warId}' already exists with different transaction identity.", before);
            if (!TryGetDefinition(request.warDefinitionId, out DiplomaticWarDefinition definition)) return Fail(DiplomaticOperationCode.MissingDefinition, $"Diplomatic War definition '{request.warDefinitionId}' is missing.", before);
            DiplomaticActorReferenceData[] sideA = CleanActors(request.sideA);
            DiplomaticActorReferenceData[] sideB = CleanActors(request.sideB);
            if (sideA.Length == 0 || sideB.Length == 0) return Fail(DiplomaticOperationCode.InvalidRequest, "War declaration requires two non-empty sides.", before);
            foreach (DiplomaticActorReferenceData actor in sideA.Concat(sideB))
            {
                if (!ValidateActor(actor, DiplomaticOperationKind.DeclareWar, out string actorFailure)) return Fail(DiplomaticOperationCode.ActorIneligible, actorFailure, before);
                if (actor.actorKind == DiplomaticActorKind.Faction && !definition.SupportsFactionalParticipants) return Fail(DiplomaticOperationCode.ActorIneligible, $"War definition '{definition.Id}' does not support faction participants.", before);
            }
            if (sideA.Any(a => sideB.Any(b => a.Equals(b)))) return Fail(DiplomaticOperationCode.InvalidRequest, "War sides cannot contain the same actor.", before);

            string sideAId = $"{warId}.side.a";
            string sideBId = $"{warId}.side.b";
            List<DiplomaticWarParticipationRecordData> participations = new List<DiplomaticWarParticipationRecordData>();
            AddParticipations(participations, warId, sideAId, sideA, request.sourceDecisionId, request.worldTime);
            AddParticipations(participations, warId, sideBId, sideB, request.sourceDecisionId, request.worldTime);
            DiplomaticWarRecordData war = new DiplomaticWarRecordData
            {
                warId = warId,
                warDefinitionId = definition.Id,
                title = string.IsNullOrWhiteSpace(request.title) ? definition.DisplayName : request.title.Trim(),
                category = definition.Category,
                lifecycleState = definition.RequiresDeclaration ? DiplomaticWarLifecycleState.Declared : DiplomaticWarLifecycleState.Active,
                visibility = request.visibility,
                sideIds = new[] { sideAId, sideBId },
                participationIds = DiplomacyModelUtility.Clean(participations.Select(item => item.participationId)),
                declarationRecordId = request.declarationRecordId ?? string.Empty,
                declaredWorldTime = request.worldTime,
                revision = 1L
            };
            if (request.preview) return Success("Diplomatic war declaration previewed.", before, before, preview: true, subjectId: warId, war: war);
            warsById[warId] = war;
            warSidesById[sideAId] = new DiplomaticWarSideRecordData { sideId = sideAId, warId = warId, displayName = "Side A", principalActors = sideA };
            warSidesById[sideBId] = new DiplomaticWarSideRecordData { sideId = sideBId, warId = warId, displayName = "Side B", principalActors = sideB };
            foreach (DiplomaticWarParticipationRecordData participation in participations) warParticipationsById[participation.participationId] = participation;
            CompleteTransaction(request.transactionId, "declare-war", warId);
            Revision++;
            return Success("Diplomatic war declared.", before, Revision, subjectId: warId, war: war);
        }

        public DiplomacyOperationResult TransitionWar(string transactionId, string warId, DiplomaticWarLifecycleState targetState, double worldTime, string agreementId = "", bool preview = false)
        {
            long before = Revision;
            warId = DiplomacyModelUtility.Normalize(warId);
            if (TryDuplicate(transactionId, warId, "transition-war", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (!warsById.TryGetValue(warId, out DiplomaticWarRecordData war)) return Fail(DiplomaticOperationCode.MissingWar, $"Diplomatic war '{warId}' is missing.", before);
            if (!Enum.IsDefined(typeof(DiplomaticWarLifecycleState), targetState) || targetState == DiplomaticWarLifecycleState.Unknown) return Fail(DiplomaticOperationCode.InvalidState, "War target state is invalid.", before);
            DiplomaticWarRecordData changed = war.Clone();
            changed.lifecycleState = targetState;
            changed.endedWorldTime = targetState == DiplomaticWarLifecycleState.Ended || targetState == DiplomaticWarLifecycleState.Historical ? worldTime : changed.endedWorldTime;
            if (targetState == DiplomaticWarLifecycleState.Ceasefire || targetState == DiplomaticWarLifecycleState.Armistice) changed.ceasefireAgreementId = agreementId ?? string.Empty;
            if (targetState == DiplomaticWarLifecycleState.Ended) changed.peaceAgreementId = agreementId ?? string.Empty;
            changed.revision++;
            if (preview) return Success("Diplomatic war transition previewed.", before, before, preview: true, subjectId: warId, war: changed);
            warsById[warId] = changed;
            CompleteTransaction(transactionId, "transition-war", warId);
            Revision++;
            return Success("Diplomatic war transitioned.", before, Revision, subjectId: warId, war: changed);
        }

        public DiplomacyOperationResult RecordIncident(DiplomaticIncidentRequest request)
        {
            request ??= new DiplomaticIncidentRequest();
            long before = Revision;
            string incidentId = DiplomacyModelUtility.Normalize(request.incidentId);
            if (TryDuplicate(request.transactionId, incidentId, "record-incident", before, out DiplomacyOperationResult duplicate)) return duplicate;
            if (string.IsNullOrWhiteSpace(incidentId)) return Fail(DiplomaticOperationCode.InvalidRequest, "Diplomatic incident ID is required.", before);
            if (!string.IsNullOrWhiteSpace(request.warId) && !warsById.ContainsKey(request.warId)) return Fail(DiplomaticOperationCode.MissingWar, $"Diplomatic war '{request.warId}' is missing.", before);
            if (!string.IsNullOrWhiteSpace(request.relationId) && !relationsById.ContainsKey(request.relationId)) return Fail(DiplomaticOperationCode.MissingRelation, $"Diplomatic relation '{request.relationId}' is missing.", before);
            DiplomaticActorReferenceData source = NormalizeActor(request.sourceActor);
            DiplomaticActorReferenceData target = NormalizeActor(request.targetActor);
            if (!ValidateActor(source, DiplomaticOperationKind.RecordIncident, out string sourceFailure)) return Fail(DiplomaticOperationCode.ActorIneligible, sourceFailure, before);
            if (!ValidateActor(target, DiplomaticOperationKind.RecordIncident, out string targetFailure)) return Fail(DiplomaticOperationCode.ActorIneligible, targetFailure, before);
            DiplomaticIncidentRecordData incident = new DiplomaticIncidentRecordData { incidentId = incidentId, warId = request.warId ?? string.Empty, relationId = request.relationId ?? string.Empty, category = request.category, sourceActor = source, targetActor = target, worldTime = request.worldTime, sourceEventId = request.sourceEventId ?? string.Empty, sourceRecordId = request.sourceRecordId ?? string.Empty, publicSummary = request.publicSummary ?? string.Empty, visibility = request.visibility };
            if (request.preview) return Success("Diplomatic incident previewed.", before, before, preview: true, subjectId: incidentId, incident: incident);
            incidentsById[incidentId] = incident;
            CompleteTransaction(request.transactionId, "record-incident", incidentId);
            Revision++;
            return Success("Diplomatic incident recorded.", before, Revision, subjectId: incidentId, incident: incident);
        }

        public IReadOnlyList<DiplomaticRelationRecordData> QueryRelationsForActor(DiplomaticActorReferenceData actor, bool activeOnly = false)
        {
            actor = NormalizeActor(actor);
            return relationsById.Values
                .Where(item => (item.sourceActor?.Equals(actor) == true || item.targetActor?.Equals(actor) == true) && (!activeOnly || item.IsActive))
                .OrderBy(item => item.startWorldTime)
                .ThenBy(item => item.relationId, StringComparer.Ordinal)
                .Select(item => item.Clone())
                .ToArray();
        }

        public DiplomaticProjection GetProjection(string recordId, bool privileged = false)
        {
            recordId = DiplomacyModelUtility.Normalize(recordId);
            object snapshot = null;
            DiplomaticVisibility visibility = DiplomaticVisibility.Public;
            if (relationsById.TryGetValue(recordId, out DiplomaticRelationRecordData relation)) { snapshot = relation.Clone(); visibility = relation.visibility; }
            else if (agreementsById.TryGetValue(recordId, out DiplomaticAgreementRecordData agreement)) { snapshot = agreement.Clone(); visibility = agreement.visibility; }
            else if (warsById.TryGetValue(recordId, out DiplomaticWarRecordData war)) { snapshot = war.Clone(); visibility = war.visibility; }
            else if (incidentsById.TryGetValue(recordId, out DiplomaticIncidentRecordData incident)) { snapshot = incident.Clone(); visibility = incident.visibility; }
            if (snapshot == null) return new DiplomaticProjection(DiplomaticProjectionAccess.Denied, recordId, visibility, null, "Diplomatic record is missing.");
            if (privileged || visibility == DiplomaticVisibility.Public) return new DiplomaticProjection(privileged ? DiplomaticProjectionAccess.Privileged : DiplomaticProjectionAccess.Full, recordId, visibility, snapshot, "Diplomatic record visible.");
            if (visibility == DiplomaticVisibility.Hidden) return new DiplomaticProjection(DiplomaticProjectionAccess.Concealed, string.Empty, visibility, null, "Diplomatic record is concealed.");
            return new DiplomaticProjection(DiplomaticProjectionAccess.Redacted, recordId, visibility, null, "Diplomatic record is redacted.");
        }

        public DiplomacyRuntimeSaveData CreateSaveData()
        {
            return new DiplomacyRuntimeSaveData
            {
                schemaVersion = DiplomacyRuntimeSaveData.CurrentSchemaVersion,
                worldId = worldId,
                revision = Revision,
                relations = relationsById.Values.OrderBy(item => item.relationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                agreements = agreementsById.Values.OrderBy(item => item.agreementId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                clauses = clausesById.Values.OrderBy(item => item.clauseId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                signatures = signaturesById.Values.OrderBy(item => item.signatureId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                ratifications = ratificationsById.Values.OrderBy(item => item.ratificationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                breaches = breachesById.Values.OrderBy(item => item.breachId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                wars = warsById.Values.OrderBy(item => item.warId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                warSides = warSidesById.Values.OrderBy(item => item.sideId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                warParticipations = warParticipationsById.Values.OrderBy(item => item.participationId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                incidents = incidentsById.Values.OrderBy(item => item.incidentId, StringComparer.Ordinal).Select(item => item.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(item => item.revision).ThenBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).RetainNewestTransactions().ToList()
            };
        }

        public DiplomacyOperationResult RestoreFromSaveData(DiplomacyRuntimeSaveData saveData, DefinitionRegistry definitionRegistry, OrganizationRuntime organizationRuntime, FactionRuntime factionRuntime, OrganizationAuthorityRuntime authorityRuntime, OrganizationDecisionRuntime decisionRuntime, OrganizationResourceRuntime resourceRuntime, string runtimeWorldId, IEnumerable<string> personIds, bool restoring = false)
        {
            long before = Revision;
            if (!ValidateSaveData(saveData, definitionRegistry, organizationRuntime, factionRuntime, runtimeWorldId, personIds, out string failure)) return Fail(DiplomaticOperationCode.RestoreFailed, failure, before);
            Configure(definitionRegistry, organizationRuntime, factionRuntime, authorityRuntime, decisionRuntime, resourceRuntime, runtimeWorldId, personIds);
            RestoreInternal(saveData ?? new DiplomacyRuntimeSaveData());
            return Success(restoring ? "Diplomacy runtime restored." : "Diplomacy runtime loaded.", before, Revision);
        }

        public static bool ValidateSaveData(DiplomacyRuntimeSaveData saveData, DefinitionRegistry registry, OrganizationRuntime organizations, FactionRuntime factions, string worldId, IEnumerable<string> personIds, out string failure)
        {
            saveData ??= new DiplomacyRuntimeSaveData();
            if (saveData.schemaVersion != DiplomacyRuntimeSaveData.CurrentSchemaVersion) return Invalid($"Unsupported diplomacy schema version {saveData.schemaVersion}.", out failure);
            HashSet<string> relationIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> agreementIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> clauseIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> partyIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> warIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> sideIds = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> participationIds = new HashSet<string>(StringComparer.Ordinal);
            string runtimeWorld = DiplomacyModelUtility.Normalize(worldId);

            foreach (DiplomaticRelationRecordData relation in saveData.relations ?? new List<DiplomaticRelationRecordData>())
            {
                if (relation == null || string.IsNullOrWhiteSpace(relation.relationId)) return Invalid("Diplomatic relation has no stable ID.", out failure);
                if (!relationIds.Add(relation.relationId)) return Invalid($"Duplicate Diplomatic Relation '{relation.relationId}'.", out failure);
                if (!TryGetDefinition(registry, relation.relationDefinitionId, out DiplomaticRelationDefinition _)) return Invalid($"Diplomatic Relation '{relation.relationId}' references missing definition '{relation.relationDefinitionId}'.", out failure);
                if (!ValidateActorReference(relation.sourceActor, organizations, factions, runtimeWorld, out failure)) return false;
                if (!ValidateActorReference(relation.targetActor, organizations, factions, runtimeWorld, out failure)) return false;
                if (relation.sourceActor.Equals(relation.targetActor)) return Invalid($"Diplomatic Relation '{relation.relationId}' targets itself.", out failure);
            }

            foreach (DiplomaticAgreementRecordData agreement in saveData.agreements ?? new List<DiplomaticAgreementRecordData>())
            {
                if (agreement == null || string.IsNullOrWhiteSpace(agreement.agreementId)) return Invalid("Diplomatic agreement has no stable ID.", out failure);
                if (!agreementIds.Add(agreement.agreementId)) return Invalid($"Duplicate Diplomatic Agreement '{agreement.agreementId}'.", out failure);
                if (!TryGetDefinition(registry, agreement.agreementDefinitionId, out DiplomaticAgreementDefinition definition)) return Invalid($"Diplomatic Agreement '{agreement.agreementId}' references missing definition '{agreement.agreementDefinitionId}'.", out failure);
                int principals = 0;
                foreach (DiplomaticAgreementPartyRecordData party in agreement.parties ?? Array.Empty<DiplomaticAgreementPartyRecordData>())
                {
                    if (party == null || string.IsNullOrWhiteSpace(party.partyId)) return Invalid($"Diplomatic Agreement '{agreement.agreementId}' has a party without stable ID.", out failure);
                    if (!partyIds.Add($"{agreement.agreementId}:{party.partyId}")) return Invalid($"Diplomatic Agreement '{agreement.agreementId}' has duplicate party '{party.partyId}'.", out failure);
                    if (party.role == DiplomaticPartyRole.Principal) principals++;
                    if (!ValidateActorReference(party.actor, organizations, factions, runtimeWorld, out failure)) return false;
                }
                if (principals < definition.MinimumPrincipalParties) return Invalid($"Diplomatic Agreement '{agreement.agreementId}' has too few principal parties.", out failure);
            }

            foreach (DiplomaticClauseRecordData clause in saveData.clauses ?? new List<DiplomaticClauseRecordData>())
            {
                if (clause == null || string.IsNullOrWhiteSpace(clause.clauseId)) return Invalid("Diplomatic clause has no stable ID.", out failure);
                if (!clauseIds.Add(clause.clauseId)) return Invalid($"Duplicate Diplomatic Clause '{clause.clauseId}'.", out failure);
                if (!agreementIds.Contains(clause.agreementId)) return Invalid($"Diplomatic Clause '{clause.clauseId}' references missing Agreement '{clause.agreementId}'.", out failure);
                if (!TryGetDefinition(registry, clause.clauseDefinitionId, out DiplomaticClauseDefinition _)) return Invalid($"Diplomatic Clause '{clause.clauseId}' references missing definition '{clause.clauseDefinitionId}'.", out failure);
            }

            foreach (DiplomaticAgreementRecordData agreement in saveData.agreements ?? new List<DiplomaticAgreementRecordData>())
            {
                foreach (string clauseId in agreement.clauseIds ?? Array.Empty<string>())
                {
                    if (!clauseIds.Contains(clauseId)) return Invalid($"Diplomatic Agreement '{agreement.agreementId}' references missing Clause '{clauseId}'.", out failure);
                }
            }

            foreach (DiplomaticBreachRecordData breach in saveData.breaches ?? new List<DiplomaticBreachRecordData>())
            {
                if (breach == null || string.IsNullOrWhiteSpace(breach.breachId)) return Invalid("Diplomatic breach has no stable ID.", out failure);
                if (!agreementIds.Contains(breach.agreementId)) return Invalid($"Diplomatic Breach '{breach.breachId}' references missing Agreement '{breach.agreementId}'.", out failure);
                if (!clauseIds.Contains(breach.clauseId)) return Invalid($"Diplomatic Breach '{breach.breachId}' references missing Clause '{breach.clauseId}'.", out failure);
                if (!ValidateActorReference(breach.allegedActor, organizations, factions, runtimeWorld, out failure)) return false;
            }

            foreach (DiplomaticWarRecordData war in saveData.wars ?? new List<DiplomaticWarRecordData>())
            {
                if (war == null || string.IsNullOrWhiteSpace(war.warId)) return Invalid("Diplomatic war has no stable ID.", out failure);
                if (!warIds.Add(war.warId)) return Invalid($"Duplicate Diplomatic War '{war.warId}'.", out failure);
                if (!TryGetDefinition(registry, war.warDefinitionId, out DiplomaticWarDefinition _)) return Invalid($"Diplomatic War '{war.warId}' references missing definition '{war.warDefinitionId}'.", out failure);
            }

            foreach (DiplomaticWarSideRecordData side in saveData.warSides ?? new List<DiplomaticWarSideRecordData>())
            {
                if (side == null || string.IsNullOrWhiteSpace(side.sideId)) return Invalid("Diplomatic war side has no stable ID.", out failure);
                if (!sideIds.Add(side.sideId)) return Invalid($"Duplicate Diplomatic War Side '{side.sideId}'.", out failure);
                if (!warIds.Contains(side.warId)) return Invalid($"Diplomatic War Side '{side.sideId}' references missing War '{side.warId}'.", out failure);
                foreach (DiplomaticActorReferenceData actor in side.principalActors ?? Array.Empty<DiplomaticActorReferenceData>()) if (!ValidateActorReference(actor, organizations, factions, runtimeWorld, out failure)) return false;
            }

            foreach (DiplomaticWarParticipationRecordData participation in saveData.warParticipations ?? new List<DiplomaticWarParticipationRecordData>())
            {
                if (participation == null || string.IsNullOrWhiteSpace(participation.participationId)) return Invalid("Diplomatic war participation has no stable ID.", out failure);
                if (!participationIds.Add(participation.participationId)) return Invalid($"Duplicate Diplomatic War Participation '{participation.participationId}'.", out failure);
                if (!warIds.Contains(participation.warId)) return Invalid($"Diplomatic War Participation '{participation.participationId}' references missing War '{participation.warId}'.", out failure);
                if (!sideIds.Contains(participation.sideId)) return Invalid($"Diplomatic War Participation '{participation.participationId}' references missing Side '{participation.sideId}'.", out failure);
                if (!ValidateActorReference(participation.actor, organizations, factions, runtimeWorld, out failure)) return false;
            }

            foreach (DiplomaticIncidentRecordData incident in saveData.incidents ?? new List<DiplomaticIncidentRecordData>())
            {
                if (incident == null || string.IsNullOrWhiteSpace(incident.incidentId)) return Invalid("Diplomatic incident has no stable ID.", out failure);
                if (!string.IsNullOrWhiteSpace(incident.warId) && !warIds.Contains(incident.warId)) return Invalid($"Diplomatic Incident '{incident.incidentId}' references missing War '{incident.warId}'.", out failure);
                if (!string.IsNullOrWhiteSpace(incident.relationId) && !relationIds.Contains(incident.relationId)) return Invalid($"Diplomatic Incident '{incident.incidentId}' references missing Relation '{incident.relationId}'.", out failure);
                if (!ValidateActorReference(incident.sourceActor, organizations, factions, runtimeWorld, out failure)) return false;
                if (!ValidateActorReference(incident.targetActor, organizations, factions, runtimeWorld, out failure)) return false;
            }

            failure = string.Empty;
            return true;
        }

        public void Reset()
        {
            relationsById.Clear();
            agreementsById.Clear();
            clausesById.Clear();
            signaturesById.Clear();
            ratificationsById.Clear();
            breachesById.Clear();
            warsById.Clear();
            warSidesById.Clear();
            warParticipationsById.Clear();
            incidentsById.Clear();
            transactionsById.Clear();
            Revision = 0L;
        }

        public void Dispose()
        {
            Reset();
        }

        private void RestoreInternal(DiplomacyRuntimeSaveData saveData)
        {
            Reset();
            worldId = saveData.worldId ?? string.Empty;
            Revision = Math.Max(0L, saveData.revision);
            foreach (DiplomaticRelationRecordData item in saveData.relations ?? new List<DiplomaticRelationRecordData>()) relationsById[item.relationId] = item.Clone();
            foreach (DiplomaticAgreementRecordData item in saveData.agreements ?? new List<DiplomaticAgreementRecordData>()) agreementsById[item.agreementId] = item.Clone();
            foreach (DiplomaticClauseRecordData item in saveData.clauses ?? new List<DiplomaticClauseRecordData>()) clausesById[item.clauseId] = item.Clone();
            foreach (DiplomaticSignatureRecordData item in saveData.signatures ?? new List<DiplomaticSignatureRecordData>()) signaturesById[item.signatureId] = item.Clone();
            foreach (DiplomaticRatificationRecordData item in saveData.ratifications ?? new List<DiplomaticRatificationRecordData>()) ratificationsById[item.ratificationId] = item.Clone();
            foreach (DiplomaticBreachRecordData item in saveData.breaches ?? new List<DiplomaticBreachRecordData>()) breachesById[item.breachId] = item.Clone();
            foreach (DiplomaticWarRecordData item in saveData.wars ?? new List<DiplomaticWarRecordData>()) warsById[item.warId] = item.Clone();
            foreach (DiplomaticWarSideRecordData item in saveData.warSides ?? new List<DiplomaticWarSideRecordData>()) warSidesById[item.sideId] = item.Clone();
            foreach (DiplomaticWarParticipationRecordData item in saveData.warParticipations ?? new List<DiplomaticWarParticipationRecordData>()) warParticipationsById[item.participationId] = item.Clone();
            foreach (DiplomaticIncidentRecordData item in saveData.incidents ?? new List<DiplomaticIncidentRecordData>()) incidentsById[item.incidentId] = item.Clone();
            foreach (DiplomaticTransactionRecordData item in saveData.transactions ?? new List<DiplomaticTransactionRecordData>()) transactionsById[item.transactionId] = item.Clone();
        }

        private bool ValidateActor(DiplomaticActorReferenceData actor, DiplomaticOperationKind operation, out string failure)
        {
            return ValidateActorReference(actor, organizations, factions, worldId, out failure);
        }

        private static bool ValidateActorReference(DiplomaticActorReferenceData actor, OrganizationRuntime organizations, FactionRuntime factions, string worldId, out string failure)
        {
            actor = NormalizeActor(actor);
            if (actor == null || string.IsNullOrWhiteSpace(actor.actorId)) return Invalid("Diplomatic actor is missing.", out failure);
            string actorWorld = DiplomacyModelUtility.Normalize(actor.worldId);
            string runtimeWorld = DiplomacyModelUtility.Normalize(worldId);
            if (!string.IsNullOrWhiteSpace(actorWorld) && !string.IsNullOrWhiteSpace(runtimeWorld) && !string.Equals(actorWorld, runtimeWorld, StringComparison.Ordinal)) return Invalid($"Diplomatic actor '{actor.StableKey}' belongs to a different world.", out failure);
            if (actor.actorKind == DiplomaticActorKind.Organization)
            {
                if (organizations == null || !organizations.TryGetSnapshot(actor.actorId, out OrganizationSnapshot snapshot)) return Invalid($"Organization diplomatic actor '{actor.actorId}' is missing.", out failure);
                if (snapshot.LifecycleState != OrganizationLifecycleState.Active && snapshot.LifecycleState != OrganizationLifecycleState.Forming && snapshot.LifecycleState != OrganizationLifecycleState.Dormant) return Invalid($"Organization diplomatic actor '{actor.actorId}' is not eligible for diplomacy in state {snapshot.LifecycleState}.", out failure);
                return Success(out failure);
            }
            if (actor.actorKind == DiplomaticActorKind.Faction)
            {
                if (factions == null || !factions.TryGetFaction(actor.actorId, out FactionRecordData faction) || !faction.IsActive) return Invalid($"Faction diplomatic actor '{actor.actorId}' is missing or inactive.", out failure);
                if (faction.hostContext == null) return Invalid($"Faction diplomatic actor '{actor.actorId}' has no host context.", out failure);
                if (faction.hostContext.contextKind == FactionHostContextKind.SingleOrganization) return Invalid($"Faction diplomatic actor '{actor.actorId}' is internal to a single organization and cannot create formal diplomacy.", out failure);
                return Success(out failure);
            }
            return Invalid($"Diplomatic actor kind '{actor.actorKind}' is reserved for a future system.", out failure);
        }

        private bool ValidateAuthoritySource(DiplomaticActorReferenceData actor, string grantId, out string failure)
        {
            grantId = DiplomacyModelUtility.Normalize(grantId);
            if (string.IsNullOrWhiteSpace(grantId) || authority == null) return Success(out failure);
            if (!authority.TryGetGrant(grantId, out OrganizationAuthoritySnapshot grant)) return Invalid($"Authority grant '{grantId}' is missing.", out failure);
            if (actor.actorKind == DiplomaticActorKind.Organization && !string.Equals(grant.OrganizationId, actor.actorId, StringComparison.Ordinal)) return Invalid($"Authority grant '{grantId}' belongs to Organization '{grant.OrganizationId}', not '{actor.actorId}'.", out failure);
            if (grant.LifecycleState != OrganizationAuthorityGrantLifecycleState.Active) return Invalid($"Authority grant '{grantId}' is not active.", out failure);
            return Success(out failure);
        }

        private static DiplomaticActorReferenceData NormalizeActor(DiplomaticActorReferenceData actor)
        {
            return new DiplomaticActorReferenceData
            {
                actorKind = actor?.actorKind ?? DiplomaticActorKind.Unknown,
                actorId = DiplomacyModelUtility.Normalize(actor?.actorId),
                worldId = DiplomacyModelUtility.Normalize(actor?.worldId)
            };
        }

        private static DiplomaticActorReferenceData[] CleanActors(IEnumerable<DiplomaticActorReferenceData> actors)
        {
            return (actors ?? Array.Empty<DiplomaticActorReferenceData>())
                .Select(NormalizeActor)
                .Where(item => item.actorKind != DiplomaticActorKind.Unknown && !string.IsNullOrWhiteSpace(item.actorId))
                .GroupBy(item => item.StableKey, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(item => item.StableKey, StringComparer.Ordinal)
                .ToArray();
        }

        private static void AddParticipations(ICollection<DiplomaticWarParticipationRecordData> target, string warId, string sideId, IEnumerable<DiplomaticActorReferenceData> actors, string decisionId, double worldTime)
        {
            foreach (DiplomaticActorReferenceData actor in actors ?? Array.Empty<DiplomaticActorReferenceData>())
            {
                target.Add(new DiplomaticWarParticipationRecordData
                {
                    participationId = $"{warId}.participation.{sideId}.{actor.StableKey}".Replace(':', '-'),
                    warId = warId,
                    sideId = sideId,
                    actor = actor.Clone(),
                    status = DiplomaticWarParticipantStatus.Belligerent,
                    sourceDecisionId = decisionId ?? string.Empty,
                    joinedWorldTime = worldTime
                });
            }
        }

        private bool TryDuplicate(string transactionId, string subjectId, string operation, long before, out DiplomacyOperationResult result)
        {
            transactionId = DiplomacyModelUtility.Normalize(transactionId);
            if (!string.IsNullOrWhiteSpace(transactionId) && transactionsById.TryGetValue(transactionId, out DiplomaticTransactionRecordData previous))
            {
                result = Success("Duplicate diplomatic transaction ignored.", before, before, duplicate: true, subjectId: previous.subjectId);
                return true;
            }
            result = null;
            return false;
        }

        private void CompleteTransaction(string transactionId, string operation, string subjectId)
        {
            transactionId = DiplomacyModelUtility.Normalize(transactionId);
            if (string.IsNullOrWhiteSpace(transactionId)) return;
            transactionsById[transactionId] = new DiplomaticTransactionRecordData { transactionId = transactionId, operation = operation ?? string.Empty, subjectId = subjectId ?? string.Empty, revision = Revision + 1L };
        }

        private bool TryGetDefinition<TDefinition>(string id, out TDefinition definition) where TDefinition : class, IGameDefinition
        {
            return TryGetDefinition(registry, id, out definition);
        }

        private static bool TryGetDefinition<TDefinition>(DefinitionRegistry definitionRegistry, string id, out TDefinition definition) where TDefinition : class, IGameDefinition
        {
            definition = null;
            return definitionRegistry != null && definitionRegistry.TryGet(DiplomacyModelUtility.Normalize(id), out definition);
        }

        private static DiplomacyOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false, string subjectId = "", DiplomaticRelationRecordData relation = null, DiplomaticAgreementRecordData agreement = null, DiplomaticClauseRecordData clause = null, DiplomaticWarRecordData war = null, DiplomaticIncidentRecordData incident = null)
        {
            return DiplomacyOperationResult.Success(message, before, after, preview, duplicate, subjectId, relation, agreement, clause, war, incident);
        }

        private static DiplomacyOperationResult Fail(DiplomaticOperationCode code, string message, long before)
        {
            return DiplomacyOperationResult.Failure(code, message, before);
        }

        private static bool Invalid(string message, out string failure)
        {
            failure = message ?? string.Empty;
            return false;
        }

        private static bool Success(out string failure)
        {
            failure = string.Empty;
            return true;
        }
    }
}
