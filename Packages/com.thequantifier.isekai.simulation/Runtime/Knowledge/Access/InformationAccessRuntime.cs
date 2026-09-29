using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Knowledge.Sources;

namespace UnityIsekaiGame.Knowledge.Access
{
    public sealed class InformationAccessRuntime
    {
        private readonly Dictionary<string, InformationAccessPolicyData> policiesById = new Dictionary<string, InformationAccessPolicyData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InformationAccessGrantData> grantsById = new Dictionary<string, InformationAccessGrantData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InformationAccessDenialData> denialsById = new Dictionary<string, InformationAccessDenialData>(StringComparer.Ordinal);
        private readonly Dictionary<string, InformationConcealmentData> concealmentsById = new Dictionary<string, InformationConcealmentData>(StringComparer.Ordinal);
        private readonly List<InformationClassificationRevisionData> classificationRevisions = new List<InformationClassificationRevisionData>();
        private readonly List<InformationAccessAuditData> auditRecords = new List<InformationAccessAuditData>();
        private readonly Dictionary<string, InformationAccessProcessedTransactionData> processedTransactions = new Dictionary<string, InformationAccessProcessedTransactionData>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private string ownerId;

        public string OwnerId => ownerId ?? string.Empty;
        public long AccessRevision { get; private set; }

        public void Configure(DefinitionRegistry definitionRegistry, string owner)
        {
            registry = definitionRegistry ?? registry;
            ownerId = owner ?? string.Empty;
        }

        public InformationAccessOperationResult RegisterPolicy(InformationAccessPolicyData policy, string transactionId = "", bool preview = false, bool restoring = false)
        {
            long prior = AccessRevision;
            if (!ValidatePolicy(policy, out string failure))
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.InvalidRequest, failure, transactionId, preview, AccessRevision);
            }

            if (!preview && IsDuplicate(transactionId, "policy", policy.policyId, out InformationAccessOperationResult duplicate))
            {
                return duplicate;
            }

            if (preview)
            {
                return InformationAccessOperationResult.Success("Information access policy preview succeeded.", transactionId, prior, prior, preview: true);
            }

            InformationAccessPolicyData clone = policy.Clone();
            if (policiesById.TryGetValue(clone.policyId, out InformationAccessPolicyData existing))
            {
                clone.revision = existing.revision + 1L;
            }

            policiesById[clone.policyId] = clone;
            AccessRevision++;
            Remember(transactionId, "policy", clone.policyId);
            return InformationAccessOperationResult.Success(restoring ? "Information access policy restored." : "Information access policy registered.", transactionId, prior, AccessRevision);
        }

        public InformationAccessOperationResult GrantAccess(InformationAccessGrantData grant, string transactionId = "", bool preview = false, bool restoring = false)
        {
            long prior = AccessRevision;
            if (!ValidateGrant(grant, out string failure))
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.InvalidRequest, failure, transactionId, preview, AccessRevision);
            }

            if (!preview && IsDuplicate(transactionId, "grant", grant.grantId, out InformationAccessOperationResult duplicate))
            {
                return duplicate;
            }

            if (preview)
            {
                return InformationAccessOperationResult.Success("Information access grant preview succeeded.", transactionId, prior, prior, preview: true);
            }

            InformationAccessGrantData clone = grant.Clone();
            if (grantsById.TryGetValue(clone.grantId, out InformationAccessGrantData existing))
            {
                clone.revision = existing.revision + 1L;
            }

            grantsById[clone.grantId] = clone;
            AccessRevision++;
            Remember(transactionId, "grant", clone.grantId);
            return InformationAccessOperationResult.Success(restoring ? "Information access grant restored." : "Information access grant registered.", transactionId, prior, AccessRevision);
        }

        public InformationAccessOperationResult AddDenial(InformationAccessDenialData denial, string transactionId = "", bool preview = false, bool restoring = false)
        {
            long prior = AccessRevision;
            if (!ValidateDenial(denial, out string failure))
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.InvalidRequest, failure, transactionId, preview, AccessRevision);
            }

            if (!preview && IsDuplicate(transactionId, "denial", denial.denialId, out InformationAccessOperationResult duplicate))
            {
                return duplicate;
            }

            if (preview)
            {
                return InformationAccessOperationResult.Success("Information access denial preview succeeded.", transactionId, prior, prior, preview: true);
            }

            denialsById[denial.denialId] = denial.Clone();
            AccessRevision++;
            Remember(transactionId, "denial", denial.denialId);
            return InformationAccessOperationResult.Success(restoring ? "Information access denial restored." : "Information access denial registered.", transactionId, prior, AccessRevision);
        }

        public InformationAccessOperationResult AddConcealment(InformationConcealmentData concealment, string transactionId = "", bool preview = false, bool restoring = false)
        {
            long prior = AccessRevision;
            if (!ValidateConcealment(concealment, out string failure))
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.InvalidRequest, failure, transactionId, preview, AccessRevision);
            }

            if (!preview && IsDuplicate(transactionId, "concealment", concealment.concealmentId, out InformationAccessOperationResult duplicate))
            {
                return duplicate;
            }

            if (preview)
            {
                return InformationAccessOperationResult.Success("Information concealment preview succeeded.", transactionId, prior, prior, preview: true);
            }

            concealmentsById[concealment.concealmentId] = concealment.Clone();
            AccessRevision++;
            Remember(transactionId, "concealment", concealment.concealmentId);
            return InformationAccessOperationResult.Success(restoring ? "Information concealment restored." : "Information concealment registered.", transactionId, prior, AccessRevision);
        }

        public InformationAccessOperationResult RevokeGrant(string grantId, string transactionId, double worldTimeSeconds = 0d)
        {
            long prior = AccessRevision;
            if (string.IsNullOrWhiteSpace(grantId) || !grantsById.TryGetValue(grantId, out InformationAccessGrantData grant))
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.MissingRecord, $"Information access grant '{grantId}' was not found.", transactionId, revision: AccessRevision);
            }

            if (IsDuplicate(transactionId, "revoke-grant", grantId, out InformationAccessOperationResult duplicate))
            {
                return duplicate;
            }

            grant.revoked = true;
            grant.revision++;
            AccessRevision++;
            Remember(transactionId, "revoke-grant", grantId);
            return InformationAccessOperationResult.Success("Information access grant revoked. Existing knowledge and memories are unchanged.", transactionId, prior, AccessRevision);
        }

        public InformationAccessOperationResult ChangeClassification(string policyId, InformationVisibilityClassification classification, string actorId, string transactionId, double worldTimeSeconds, string reason = "")
        {
            long prior = AccessRevision;
            if (!Enum.IsDefined(typeof(InformationVisibilityClassification), classification))
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.InvalidRequest, "Invalid information visibility classification.", transactionId, revision: AccessRevision);
            }

            if (string.IsNullOrWhiteSpace(policyId) || !policiesById.TryGetValue(policyId, out InformationAccessPolicyData policy))
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.MissingPolicy, $"Information access policy '{policyId}' was not found.", transactionId, revision: AccessRevision);
            }

            if (IsDuplicate(transactionId, "classification", policyId, out InformationAccessOperationResult duplicate))
            {
                return duplicate;
            }

            InformationVisibilityClassification previous = policy.classification;
            policy.classification = classification;
            policy.revision++;
            AccessRevision++;
            classificationRevisions.Add(new InformationClassificationRevisionData
            {
                revisionId = string.IsNullOrWhiteSpace(transactionId) ? $"classification.{policyId}.{AccessRevision}" : transactionId,
                policyId = policyId,
                previousClassification = previous,
                newClassification = classification,
                actorId = actorId ?? string.Empty,
                worldTimeSeconds = Math.Max(0d, worldTimeSeconds),
                reason = reason ?? string.Empty,
                revision = AccessRevision
            });
            Remember(transactionId, "classification", policyId);
            return InformationAccessOperationResult.Success("Information classification changed for future access. Existing knowledge is unchanged.", transactionId, prior, AccessRevision);
        }

        public InformationAccessDecision EvaluateAccess(InformationAccessContext context)
        {
            InformationAccessPolicyData policy = ResolvePolicy(context);
            InformationSubjectReferenceData subject = context?.Subject?.Clone() ?? policy?.subject?.Clone() ?? new InformationSubjectReferenceData();
            if (context == null)
            {
                return Decision(string.Empty, subject, InformationAccessMode.Inspect, InformationAccessDecisionKind.Denied, InformationAccessDenialCode.InvalidRequest, false, InformationResharingPolicy.None, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), 0d, "Access request is invalid.", "Access context is missing.", false);
            }

            if (policy == null)
            {
                return Decision(context.RequestingPersonId, subject, context.AccessMode, InformationAccessDecisionKind.Denied, InformationAccessDenialCode.MissingPolicy, false, InformationResharingPolicy.None, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), context.WorldTimeSeconds, context.RevealDenialReasons ? "Information access policy is missing." : string.Empty, "No policy resolved for access request.", false);
            }

            if (context.IsPrivileged)
            {
                return BuildAllowedDecision(context, policy, InformationAccessDecisionKind.FullAccess, "Privileged access granted by explicit context.");
            }

            if (policy.revoked)
            {
                return Denied(context, policy, InformationAccessDecisionKind.Revoked, InformationAccessDenialCode.Revoked, "Information access policy was revoked.");
            }

            if (context.WorldTimeSeconds + 0.0001d < policy.effectiveStartTime)
            {
                return Denied(context, policy, InformationAccessDecisionKind.Denied, InformationAccessDenialCode.NotYetEffective, "Information access policy is not yet effective.");
            }

            if (policy.expirationTime >= 0d && context.WorldTimeSeconds > policy.expirationTime + 0.0001d)
            {
                return Denied(context, policy, InformationAccessDecisionKind.Expired, InformationAccessDenialCode.Expired, "Information access policy has expired.");
            }

            InformationAccessDenialData denial = ActiveDenials(policy, context).FirstOrDefault();
            if (denial != null)
            {
                return Denied(context, policy, InformationAccessDecisionKind.Denied, InformationAccessDenialCode.ExplicitDenial, string.IsNullOrWhiteSpace(denial.reason) ? "Explicit denial blocks access." : denial.reason);
            }

            InformationConcealmentData concealment = ActiveConcealments(policy, context).FirstOrDefault();
            if (concealment != null && !MatchesAny(context.AuthorizationIds, concealment.authorizedExceptionIds))
            {
                return Denied(context, policy, InformationAccessDecisionKind.Denied, InformationAccessDenialCode.Concealed, "Information is concealed from this requester.");
            }

            if (policy.discoveryRequired && !context.HasDiscoveredSubject)
            {
                return Denied(context, policy, InformationAccessDecisionKind.NotDiscovered, InformationAccessDenialCode.NotDiscovered, "Requester has not discovered this information.");
            }

            InformationAccessGrantData grant = ActiveGrants(policy, context).FirstOrDefault();
            if (grant != null)
            {
                return BuildAllowedDecision(context, policy, DecisionKindForGrant(policy, grant, context), "Explicit access grant permits this mode.", grant);
            }

            if (IsPublic(policy.classification))
            {
                return BuildAllowedDecision(context, policy, InformationAccessDecisionKind.FullAccess, "Public information may be inspected, but is not automatically known.");
            }

            if (HasContextualAccess(policy, context))
            {
                return BuildAllowedDecision(context, policy, DecisionKindForPolicy(policy, context), "Contextual access granted.");
            }

            if (IsRestricted(policy.classification))
            {
                InformationAccessDenialCode code = RestrictionCode(policy.classification);
                InformationAccessDecisionKind kind = code == InformationAccessDenialCode.MissingAuthorization
                    ? InformationAccessDecisionKind.MissingAuthorization
                    : InformationAccessDecisionKind.Denied;
                return Denied(context, policy, kind, code, "Requester lacks required organization, role, or need-to-know access.");
            }

            return Denied(context, policy, InformationAccessDecisionKind.MissingAuthorization, InformationAccessDenialCode.MissingAuthorization, "Requester is not authorized for this information.");
        }

        public RedactedInformationProjection Project(InformationAccessContext context, IEnumerable<string> allDetailIds)
        {
            InformationAccessDecision decision = EvaluateAccess(context);
            Dictionary<string, InformationRedactionState> details = new Dictionary<string, InformationRedactionState>(StringComparer.Ordinal);
            foreach (string detailId in (allDetailIds ?? Array.Empty<string>()).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal))
            {
                if (decision.FullAccess)
                {
                    details[detailId] = InformationRedactionState.Visible;
                }
                else if (decision.AllowedDetails.Contains(detailId, StringComparer.Ordinal))
                {
                    details[detailId] = InformationRedactionState.Visible;
                }
                else if (decision.RedactedDetails.Contains(detailId, StringComparer.Ordinal))
                {
                    details[detailId] = InformationRedactionState.Redacted;
                }
                else if (decision.HiddenDetails.Contains(detailId, StringComparer.Ordinal))
                {
                    details[detailId] = InformationRedactionState.Hidden;
                }
                else
                {
                    details[detailId] = InformationRedactionState.Inaccessible;
                }
            }

            return new RedactedInformationProjection(context?.Subject, decision, details);
        }

        public InformationAccessOperationResult RecordAudit(InformationAccessDecision decision, InformationAccessContext context, bool gameplayAudit = true)
        {
            long prior = AccessRevision;
            if (decision == null || context == null)
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.InvalidRequest, "Cannot audit a missing access decision.", revision: AccessRevision);
            }

            if (!decision.AuditRequired)
            {
                return InformationAccessOperationResult.Success("Access decision did not require an audit record.", string.Empty, prior, prior, decision, preview: true);
            }

            auditRecords.Add(new InformationAccessAuditData
            {
                auditId = $"information-access.audit.{AccessRevision + 1}.{auditRecords.Count + 1}",
                policyId = decision.PolicyIds.FirstOrDefault() ?? string.Empty,
                subject = decision.Subject.Data.Clone(),
                requesterPersonId = context.RequestingPersonId ?? string.Empty,
                mode = context.AccessMode,
                decision = decision.Decision,
                denialCode = decision.DenialCode,
                unauthorized = decision.Denied,
                gameplayAudit = gameplayAudit,
                worldTimeSeconds = Math.Max(0d, context.WorldTimeSeconds),
                visibleReason = decision.VisibleReason,
                diagnosticReason = decision.DiagnosticReason,
                revision = AccessRevision + 1
            });
            AccessRevision++;
            return InformationAccessOperationResult.Success("Access audit recorded.", string.Empty, prior, AccessRevision, decision);
        }

        public InformationAccessSnapshot CreateSnapshot()
        {
            return new InformationAccessSnapshot(
                OwnerId,
                AccessRevision,
                policiesById.Values.OrderBy(policy => policy.policyId, StringComparer.Ordinal).Select(policy => new InformationAccessPolicyRecord(policy)).ToArray(),
                grantsById.Values.OrderBy(grant => grant.grantId, StringComparer.Ordinal).Select(grant => new InformationAccessGrantRecord(grant)).ToArray(),
                concealmentsById.Values.OrderBy(concealment => concealment.concealmentId, StringComparer.Ordinal).Select(concealment => new InformationConcealmentRecord(concealment)).ToArray(),
                auditRecords.OrderBy(audit => audit.auditId, StringComparer.Ordinal).ToArray());
        }

        public InformationAccessSaveData CreateSaveData()
        {
            return new InformationAccessSaveData
            {
                ownerId = OwnerId,
                accessRevision = AccessRevision,
                policies = policiesById.Values.OrderBy(policy => policy.policyId, StringComparer.Ordinal).Select(policy => policy.Clone()).ToArray(),
                grants = grantsById.Values.OrderBy(grant => grant.grantId, StringComparer.Ordinal).Select(grant => grant.Clone()).ToArray(),
                denials = denialsById.Values.OrderBy(denial => denial.denialId, StringComparer.Ordinal).Select(denial => denial.Clone()).ToArray(),
                concealments = concealmentsById.Values.OrderBy(concealment => concealment.concealmentId, StringComparer.Ordinal).Select(concealment => concealment.Clone()).ToArray(),
                classificationRevisions = classificationRevisions.OrderBy(revision => revision.revisionId, StringComparer.Ordinal).Select(revision => revision.Clone()).ToArray(),
                audits = auditRecords.OrderBy(audit => audit.auditId, StringComparer.Ordinal).Select(audit => audit.Clone()).ToArray(),
                processedTransactions = processedTransactions.Values.OrderBy(transaction => transaction.transactionId, StringComparer.Ordinal).ToArray()
            };
        }

        public InformationAccessOperationResult RestoreFromSaveData(InformationAccessSaveData saveData, DefinitionRegistry definitionRegistry, string expectedOwnerId, bool restoring = true)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, expectedOwnerId, out string failureReason))
            {
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.RestoreFailed, failureReason, revision: AccessRevision);
            }

            InformationAccessSaveData rollback = CreateSaveData();
            try
            {
                registry = definitionRegistry ?? registry;
                ownerId = saveData.ownerId ?? string.Empty;
                policiesById.Clear();
                grantsById.Clear();
                denialsById.Clear();
                concealmentsById.Clear();
                classificationRevisions.Clear();
                auditRecords.Clear();
                processedTransactions.Clear();

                foreach (InformationAccessPolicyData policy in saveData.policies ?? Array.Empty<InformationAccessPolicyData>())
                {
                    policiesById[policy.policyId] = policy.Clone();
                }

                foreach (InformationAccessGrantData grant in saveData.grants ?? Array.Empty<InformationAccessGrantData>())
                {
                    grantsById[grant.grantId] = grant.Clone();
                }

                foreach (InformationAccessDenialData denial in saveData.denials ?? Array.Empty<InformationAccessDenialData>())
                {
                    denialsById[denial.denialId] = denial.Clone();
                }

                foreach (InformationConcealmentData concealment in saveData.concealments ?? Array.Empty<InformationConcealmentData>())
                {
                    concealmentsById[concealment.concealmentId] = concealment.Clone();
                }

                classificationRevisions.AddRange((saveData.classificationRevisions ?? Array.Empty<InformationClassificationRevisionData>()).Select(revision => revision.Clone()));
                auditRecords.AddRange((saveData.audits ?? Array.Empty<InformationAccessAuditData>()).Select(audit => audit.Clone()));
                foreach (InformationAccessProcessedTransactionData transaction in saveData.processedTransactions ?? Array.Empty<InformationAccessProcessedTransactionData>())
                {
                    if (!string.IsNullOrWhiteSpace(transaction.transactionId))
                    {
                        processedTransactions[TransactionKey(transaction.transactionId)] = transaction;
                    }
                }

                AccessRevision = Math.Max(0L, saveData.accessRevision);
                return InformationAccessOperationResult.Success("Information access restored without replaying discovery, sharing, reveal, or audit side effects.", string.Empty, AccessRevision, AccessRevision);
            }
            catch (Exception exception)
            {
                RestoreFromSaveData(rollback, registry, rollback.ownerId, restoring: true);
                return InformationAccessOperationResult.Failure(InformationAccessResultCode.RestoreFailed, exception.Message, revision: AccessRevision);
            }
        }

        public static bool ValidateSaveData(InformationAccessSaveData saveData, DefinitionRegistry definitionRegistry, string expectedOwnerId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Information Access save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != InformationAccessSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported Information Access schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedOwnerId) && !string.Equals(saveData.ownerId, expectedOwnerId, StringComparison.Ordinal))
            {
                failureReason = $"Information Access save owner '{saveData.ownerId}' does not match expected owner '{expectedOwnerId}'.";
                return false;
            }

            HashSet<string> policyIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (InformationAccessPolicyData policy in saveData.policies ?? Array.Empty<InformationAccessPolicyData>())
            {
                if (!ValidatePolicy(policy, out failureReason) || !policyIds.Add(policy.policyId ?? string.Empty))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Information Access save has duplicate policy ID '{policy?.policyId}'." : failureReason;
                    return false;
                }
            }

            HashSet<string> grantIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (InformationAccessGrantData grant in saveData.grants ?? Array.Empty<InformationAccessGrantData>())
            {
                if (!ValidateGrant(grant, out failureReason) || !grantIds.Add(grant.grantId ?? string.Empty))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Information Access save has duplicate grant ID '{grant?.grantId}'." : failureReason;
                    return false;
                }
            }

            HashSet<string> denialIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (InformationAccessDenialData denial in saveData.denials ?? Array.Empty<InformationAccessDenialData>())
            {
                if (!ValidateDenial(denial, out failureReason) || !denialIds.Add(denial.denialId ?? string.Empty))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Information Access save has duplicate denial ID '{denial?.denialId}'." : failureReason;
                    return false;
                }
            }

            HashSet<string> concealmentIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (InformationConcealmentData concealment in saveData.concealments ?? Array.Empty<InformationConcealmentData>())
            {
                if (!ValidateConcealment(concealment, out failureReason) || !concealmentIds.Add(concealment.concealmentId ?? string.Empty))
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? $"Information Access save has duplicate concealment ID '{concealment?.concealmentId}'." : failureReason;
                    return false;
                }
            }

            return true;
        }

        private InformationAccessPolicyData ResolvePolicy(InformationAccessContext context)
        {
            if (context == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(context.DeterministicPolicyId) && policiesById.TryGetValue(context.DeterministicPolicyId, out InformationAccessPolicyData deterministic))
            {
                return deterministic;
            }

            string subjectId = context.Subject?.subjectId ?? string.Empty;
            InformationSubjectType subjectType = context.Subject?.subjectType ?? InformationSubjectType.Unknown;
            return policiesById.Values
                .Where(policy => policy.subject != null && string.Equals(policy.subject.subjectId, subjectId, StringComparison.Ordinal) && policy.subject.subjectType == subjectType)
                .OrderBy(policy => policy.policyId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private InformationAccessDecision BuildAllowedDecision(InformationAccessContext context, InformationAccessPolicyData policy, InformationAccessDecisionKind kind, string reason, InformationAccessGrantData grant = null)
        {
            string[] allowed = ResolveAllowedDetails(policy, grant, context);
            string[] redacted = ResolveRedactedDetails(policy, allowed);
            string[] hidden = ResolveHiddenDetails(policy, allowed, redacted);
            bool sourceVisible = SourceVisible(policy, grant, context);
            return Decision(context.RequestingPersonId, policy.subject, context.AccessMode, kind, InformationAccessDenialCode.None, sourceVisible, ResolveResharingOutcome(policy, grant), allowed, redacted, hidden, new[] { policy.policyId }, context.WorldTimeSeconds, reason, reason, RequiresAudit(policy, granted: true, unauthorized: false));
        }

        private InformationAccessDecision Denied(InformationAccessContext context, InformationAccessPolicyData policy, InformationAccessDecisionKind decision, InformationAccessDenialCode code, string diagnostic)
        {
            bool concealExistence = ActiveConcealments(policy, context).Any(concealment => concealment.concealmentKind == InformationConcealmentKind.Existence);
            string visible = context.RevealDenialReasons && !concealExistence ? diagnostic : string.Empty;
            InformationSubjectReferenceData subject = concealExistence
                ? new InformationSubjectReferenceData { subjectType = policy.subject.subjectType, parentSubjectId = policy.subject.parentSubjectId }
                : policy.subject;
            return Decision(context.RequestingPersonId, subject, context.AccessMode, decision, code, false, InformationResharingPolicy.None, Array.Empty<string>(), Array.Empty<string>(), concealExistence ? Array.Empty<string>() : AllPolicyDetails(policy), new[] { policy.policyId }, context.WorldTimeSeconds, visible, diagnostic, RequiresAudit(policy, granted: false, unauthorized: true));
        }

        private static InformationAccessDecision Decision(string requester, InformationSubjectReferenceData subject, InformationAccessMode mode, InformationAccessDecisionKind decision, InformationAccessDenialCode denialCode, bool sourceVisible, InformationResharingPolicy resharing, IReadOnlyList<string> allowed, IReadOnlyList<string> redacted, IReadOnlyList<string> hidden, IReadOnlyList<string> policies, double time, string visible, string diagnostic, bool audit)
        {
            return new InformationAccessDecision(requester, subject, mode, decision, denialCode, sourceVisible, resharing, allowed, redacted, hidden, policies, time, visible, diagnostic, audit);
        }

        private static bool ValidatePolicy(InformationAccessPolicyData policy, out string failure)
        {
            failure = string.Empty;
            if (policy == null || string.IsNullOrWhiteSpace(policy.policyId))
            {
                failure = "Information access policy requires a stable policy ID.";
                return false;
            }

            if (policy.subject == null || policy.subject.subjectType == InformationSubjectType.Unknown || string.IsNullOrWhiteSpace(policy.subject.subjectId))
            {
                failure = $"Information access policy '{policy.policyId}' requires a typed subject reference.";
                return false;
            }

            return Enum.IsDefined(typeof(InformationVisibilityClassification), policy.classification)
                && Enum.IsDefined(typeof(InformationDisclosurePolicy), policy.disclosurePolicy)
                && Enum.IsDefined(typeof(InformationResharingPolicy), policy.resharingPolicy)
                && Enum.IsDefined(typeof(InformationSourceVisibilityPolicy), policy.sourceVisibilityPolicy)
                && Enum.IsDefined(typeof(InformationDetailVisibilityPolicy), policy.detailVisibilityPolicy)
                && Enum.IsDefined(typeof(InformationAuditPolicy), policy.auditPolicy);
        }

        private static bool ValidateGrant(InformationAccessGrantData grant, out string failure)
        {
            failure = string.Empty;
            if (grant == null || string.IsNullOrWhiteSpace(grant.grantId))
            {
                failure = "Information access grant requires a stable grant ID.";
                return false;
            }

            if (grant.granteeKind != InformationGranteeKind.Public && string.IsNullOrWhiteSpace(grant.granteeId))
            {
                failure = $"Information access grant '{grant.grantId}' requires a grantee ID.";
                return false;
            }

            if ((grant.accessModes == null || grant.accessModes.Length == 0) && !grant.permitsDisclosure && !grant.permitsResharing)
            {
                failure = $"Information access grant '{grant.grantId}' must grant at least one access, disclosure, or resharing right.";
                return false;
            }

            return true;
        }

        private static bool ValidateDenial(InformationAccessDenialData denial, out string failure)
        {
            failure = string.Empty;
            if (denial == null || string.IsNullOrWhiteSpace(denial.denialId))
            {
                failure = "Information access denial requires a stable denial ID.";
                return false;
            }

            if (denial.deniedKind != InformationGranteeKind.Public && string.IsNullOrWhiteSpace(denial.deniedId))
            {
                failure = $"Information access denial '{denial.denialId}' requires a denied ID.";
                return false;
            }

            return true;
        }

        private static bool ValidateConcealment(InformationConcealmentData concealment, out string failure)
        {
            failure = string.Empty;
            if (concealment == null || string.IsNullOrWhiteSpace(concealment.concealmentId))
            {
                failure = "Information concealment requires a stable concealment ID.";
                return false;
            }

            if (concealment.subject == null || string.IsNullOrWhiteSpace(concealment.subject.subjectId))
            {
                failure = $"Information concealment '{concealment.concealmentId}' requires a target subject.";
                return false;
            }

            return Enum.IsDefined(typeof(InformationConcealmentKind), concealment.concealmentKind);
        }

        private IEnumerable<InformationAccessGrantData> ActiveGrants(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return grantsById.Values
                .Where(grant => AppliesToPolicyOrSubject(grant.policyId, grant.subject, policy) && ActiveAt(grant.effectiveStartTime, grant.expirationTime, grant.revoked, context.WorldTimeSeconds) && GrantMatchesContext(grant, context))
                .OrderBy(grant => grant.grantId, StringComparer.Ordinal);
        }

        private IEnumerable<InformationAccessDenialData> ActiveDenials(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return denialsById.Values
                .Where(denial => AppliesToPolicyOrSubject(denial.policyId, denial.subject, policy) && ActiveAt(denial.effectiveStartTime, denial.expirationTime, denial.revoked, context.WorldTimeSeconds) && DenialMatchesContext(denial, context))
                .OrderBy(denial => denial.denialId, StringComparer.Ordinal);
        }

        private IEnumerable<InformationConcealmentData> ActiveConcealments(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return concealmentsById.Values
                .Where(concealment => AppliesToPolicyOrSubject(concealment.policyId, concealment.subject, policy) && concealment.active && ActiveAt(concealment.startTime, concealment.endTime, false, context.WorldTimeSeconds))
                .OrderBy(concealment => concealment.concealmentId, StringComparer.Ordinal);
        }

        private static bool AppliesToPolicyOrSubject(string policyId, InformationSubjectReferenceData subject, InformationAccessPolicyData policy)
        {
            return !string.IsNullOrWhiteSpace(policyId) && string.Equals(policyId, policy.policyId, StringComparison.Ordinal)
                || subject != null && policy.subject != null && subject.subjectType == policy.subject.subjectType && string.Equals(subject.subjectId, policy.subject.subjectId, StringComparison.Ordinal);
        }

        private static bool ActiveAt(double start, double end, bool revoked, double time)
        {
            return !revoked && time + 0.0001d >= start && (end < 0d || time <= end + 0.0001d);
        }

        private static bool GrantMatchesContext(InformationAccessGrantData grant, InformationAccessContext context)
        {
            return GranteeMatches(grant.granteeKind, grant.granteeId, context) && (grant.accessModes == null || grant.accessModes.Length == 0 || grant.accessModes.Contains(context.AccessMode));
        }

        private static bool DenialMatchesContext(InformationAccessDenialData denial, InformationAccessContext context)
        {
            return GranteeMatches(denial.deniedKind, denial.deniedId, context) && (denial.accessModes == null || denial.accessModes.Length == 0 || denial.accessModes.Contains(context.AccessMode));
        }

        private static bool GranteeMatches(InformationGranteeKind kind, string id, InformationAccessContext context)
        {
            return kind switch
            {
                InformationGranteeKind.Public => true,
                InformationGranteeKind.Person => string.Equals(id, context.RequestingPersonId, StringComparison.Ordinal),
                InformationGranteeKind.Organization => Contains(context.OrganizationIds, id),
                InformationGranteeKind.Role => Contains(context.RoleIds, id),
                InformationGranteeKind.Title or InformationGranteeKind.Status => Contains(context.TitleOrStatusIds, id),
                InformationGranteeKind.Token => Contains(context.AuthorizationIds, id),
                _ => Contains(context.AuthorizationIds, id)
            };
        }

        private static bool IsPublic(InformationVisibilityClassification classification)
        {
            return classification == InformationVisibilityClassification.Public || classification == InformationVisibilityClassification.Open;
        }

        private static bool IsRestricted(InformationVisibilityClassification classification)
        {
            return classification == InformationVisibilityClassification.Restricted
                || classification == InformationVisibilityClassification.OrganizationRestricted
                || classification == InformationVisibilityClassification.RoleRestricted
                || classification == InformationVisibilityClassification.ProfessionRestricted
                || classification == InformationVisibilityClassification.Medical
                || classification == InformationVisibilityClassification.Legal
                || classification == InformationVisibilityClassification.Classified
                || classification == InformationVisibilityClassification.Secret
                || classification == InformationVisibilityClassification.HighlySecret
                || classification == InformationVisibilityClassification.NeedToKnow;
        }

        private static bool IsOwner(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return string.Equals(policy.subject.ownerPersonId, context.RequestingPersonId, StringComparison.Ordinal)
                || policy.classification == InformationVisibilityClassification.OwnerOnly && Contains(policy.allowedPersonIds, context.RequestingPersonId);
        }

        private static bool IsParticipant(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return context.IsParticipant || Contains(policy.participantPersonIds, context.RequestingPersonId);
        }

        private static bool IsWitness(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return context.IsWitness || Contains(policy.witnessPersonIds, context.RequestingPersonId);
        }

        private static bool IsRecipient(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return context.IsRecipient || Contains(policy.recipientPersonIds, context.RequestingPersonId);
        }

        private static bool HasContextualAccess(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return IsOwner(policy, context)
                || Contains(policy.allowedPersonIds, context.RequestingPersonId)
                || IsParticipant(policy, context)
                || IsWitness(policy, context)
                || IsRecipient(policy, context)
                || HasAny(policy.allowedOrganizationIds) && MatchesOrganizations(policy, context)
                || HasAny(policy.allowedRoleIds) && MatchesRoles(policy, context)
                || HasAny(policy.needToKnowTags) && MatchesNeedToKnow(policy, context);
        }

        private static InformationResharingPolicy ResolveResharingOutcome(InformationAccessPolicyData policy, InformationAccessGrantData grant)
        {
            if (grant == null)
            {
                return policy.resharingPolicy;
            }

            if (!grant.permitsResharing)
            {
                return InformationResharingPolicy.NoResharing;
            }

            return policy.resharingPolicy == InformationResharingPolicy.None || policy.resharingPolicy == InformationResharingPolicy.NoResharing
                ? InformationResharingPolicy.FreelyReshareable
                : policy.resharingPolicy;
        }

        private static bool MatchesOrganizations(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return !HasAny(policy.allowedOrganizationIds) || MatchesAny(context.OrganizationIds, policy.allowedOrganizationIds);
        }

        private static bool MatchesRoles(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return !HasAny(policy.allowedRoleIds) || MatchesAny(context.RoleIds, policy.allowedRoleIds);
        }

        private static bool MatchesNeedToKnow(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return !HasAny(policy.needToKnowTags) || MatchesAny(context.NeedToKnowTags, policy.needToKnowTags);
        }

        private static bool MatchesAny(IEnumerable<string> left, IEnumerable<string> right)
        {
            HashSet<string> set = new HashSet<string>((left ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            return (right ?? Array.Empty<string>()).Any(value => set.Contains(value));
        }

        private static bool Contains(IEnumerable<string> values, string expected)
        {
            return !string.IsNullOrWhiteSpace(expected) && (values ?? Array.Empty<string>()).Contains(expected, StringComparer.Ordinal);
        }

        private static bool HasAny(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Any(value => !string.IsNullOrWhiteSpace(value));
        }

        private static InformationAccessDenialCode RestrictionCode(InformationVisibilityClassification classification)
        {
            return classification switch
            {
                InformationVisibilityClassification.OrganizationRestricted => InformationAccessDenialCode.OrganizationRestriction,
                InformationVisibilityClassification.RoleRestricted or InformationVisibilityClassification.ProfessionRestricted => InformationAccessDenialCode.RoleRestriction,
                InformationVisibilityClassification.OwnerOnly => InformationAccessDenialCode.OwnerRestriction,
                InformationVisibilityClassification.ParticipantOnly => InformationAccessDenialCode.ParticipantRestriction,
                InformationVisibilityClassification.WitnessOnly => InformationAccessDenialCode.WitnessRestriction,
                InformationVisibilityClassification.RecipientOnly => InformationAccessDenialCode.RecipientRestriction,
                InformationVisibilityClassification.SourceProtected => InformationAccessDenialCode.SourceProtectionRestriction,
                InformationVisibilityClassification.NeedToKnow => InformationAccessDenialCode.NeedToKnowRestriction,
                InformationVisibilityClassification.Restricted
                    or InformationVisibilityClassification.Medical
                    or InformationVisibilityClassification.Legal
                    or InformationVisibilityClassification.Classified
                    or InformationVisibilityClassification.Secret
                    or InformationVisibilityClassification.HighlySecret => InformationAccessDenialCode.MissingAuthorization,
                _ => InformationAccessDenialCode.ClassificationRestriction
            };
        }

        private static InformationAccessDecisionKind DecisionKindForPolicy(InformationAccessPolicyData policy, InformationAccessContext context)
        {
            return policy.detailVisibilityPolicy == InformationDetailVisibilityPolicy.All
                ? InformationAccessDecisionKind.FullAccess
                : context.RedactedAccessAcceptable && policy.redactedAccessAcceptable
                    ? InformationAccessDecisionKind.RedactedAccess
                    : InformationAccessDecisionKind.PartialAccess;
        }

        private static InformationAccessDecisionKind DecisionKindForGrant(InformationAccessPolicyData policy, InformationAccessGrantData grant, InformationAccessContext context)
        {
            return grant.detailIds != null && grant.detailIds.Length > 0 || policy.detailVisibilityPolicy != InformationDetailVisibilityPolicy.All
                ? context.RedactedAccessAcceptable && policy.redactedAccessAcceptable ? InformationAccessDecisionKind.RedactedAccess : InformationAccessDecisionKind.PartialAccess
                : InformationAccessDecisionKind.FullAccess;
        }

        private static string[] ResolveAllowedDetails(InformationAccessPolicyData policy, InformationAccessGrantData grant, InformationAccessContext context)
        {
            string[] requested = InformationAccessPolicyData.CloneArray(context.RequestedDetailIds);
            string[] grantDetails = InformationAccessPolicyData.CloneArray(grant?.detailIds);
            string[] policyVisible = InformationAccessPolicyData.CloneArray(policy.defaultVisibleDetails);
            if (policy.detailVisibilityPolicy == InformationDetailVisibilityPolicy.None)
            {
                return Array.Empty<string>();
            }

            if (policy.detailVisibilityPolicy == InformationDetailVisibilityPolicy.ExistenceOnly || policy.detailVisibilityPolicy == InformationDetailVisibilityPolicy.ClassificationOnly)
            {
                return policyVisible;
            }

            IEnumerable<string> allowed = grantDetails.Length > 0 ? grantDetails : policyVisible.Length > 0 ? policyVisible : requested;
            if (policy.detailVisibilityPolicy == InformationDetailVisibilityPolicy.All && requested.Length > 0 && grantDetails.Length == 0)
            {
                allowed = requested;
            }

            return InformationAccessPolicyData.CloneArray(allowed.ToArray());
        }

        private static string[] ResolveRedactedDetails(InformationAccessPolicyData policy, string[] allowed)
        {
            HashSet<string> allowedSet = new HashSet<string>(allowed ?? Array.Empty<string>(), StringComparer.Ordinal);
            return InformationAccessPolicyData.CloneArray((policy.defaultRedactedDetails ?? Array.Empty<string>()).Where(detail => !allowedSet.Contains(detail)).ToArray());
        }

        private static string[] ResolveHiddenDetails(InformationAccessPolicyData policy, string[] allowed, string[] redacted)
        {
            HashSet<string> blocked = new HashSet<string>((allowed ?? Array.Empty<string>()).Concat(redacted ?? Array.Empty<string>()), StringComparer.Ordinal);
            return InformationAccessPolicyData.CloneArray((policy.defaultHiddenDetails ?? Array.Empty<string>()).Where(detail => !blocked.Contains(detail)).ToArray());
        }

        private static string[] AllPolicyDetails(InformationAccessPolicyData policy)
        {
            return InformationAccessPolicyData.CloneArray((policy.defaultVisibleDetails ?? Array.Empty<string>()).Concat(policy.defaultRedactedDetails ?? Array.Empty<string>()).Concat(policy.defaultHiddenDetails ?? Array.Empty<string>()).ToArray());
        }

        private static bool SourceVisible(InformationAccessPolicyData policy, InformationAccessGrantData grant, InformationAccessContext context)
        {
            if (context.AccessMode == InformationAccessMode.RevealSource || context.AccessMode == InformationAccessMode.RevealProvenance)
            {
                return grant != null && grant.sourceVisibility == InformationSourceVisibilityPolicy.Reveal || policy.sourceVisibilityPolicy == InformationSourceVisibilityPolicy.Reveal;
            }

            return policy.sourceVisibilityPolicy == InformationSourceVisibilityPolicy.Reveal
                || grant != null && grant.sourceVisibility == InformationSourceVisibilityPolicy.Reveal
                || context.KnowsSource && policy.sourceVisibilityPolicy != InformationSourceVisibilityPolicy.HideFullProvenance;
        }

        private static bool RequiresAudit(InformationAccessPolicyData policy, bool granted, bool unauthorized)
        {
            return policy.auditPolicy == InformationAuditPolicy.AuditDeniedAndGranted
                || granted && policy.auditPolicy == InformationAuditPolicy.AuditGranted
                || !granted && policy.auditPolicy == InformationAuditPolicy.AuditDenied
                || unauthorized && policy.auditPolicy == InformationAuditPolicy.AuditUnauthorizedOnly;
        }

        private bool IsDuplicate(string transactionId, string operation, string recordId, out InformationAccessOperationResult result)
        {
            result = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (!processedTransactions.TryGetValue(TransactionKey(transactionId), out InformationAccessProcessedTransactionData processed))
            {
                return false;
            }

            result = InformationAccessOperationResult.Success($"Duplicate information access {processed.operation} transaction ignored.", transactionId, AccessRevision, AccessRevision, duplicate: true);
            return true;
        }

        private void Remember(string transactionId, string operation, string recordId)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            processedTransactions[TransactionKey(transactionId)] = new InformationAccessProcessedTransactionData
            {
                transactionId = transactionId,
                operation = operation ?? string.Empty,
                recordId = recordId ?? string.Empty,
                revision = AccessRevision
            };
        }

        private static string TransactionKey(string transactionId)
        {
            return (transactionId ?? string.Empty).Trim();
        }

        public static InformationVisibilityClassification FromKnowledgeVisibility(KnowledgeVisibility visibility)
        {
            return visibility switch
            {
                KnowledgeVisibility.Public => InformationVisibilityClassification.Public,
                KnowledgeVisibility.PersonallyObservable => InformationVisibilityClassification.Personal,
                KnowledgeVisibility.Private => InformationVisibilityClassification.Private,
                KnowledgeVisibility.Confidential => InformationVisibilityClassification.Confidential,
                KnowledgeVisibility.Hidden => InformationVisibilityClassification.Hidden,
                KnowledgeVisibility.Secret => InformationVisibilityClassification.Secret,
                KnowledgeVisibility.DiagnosticOnly => InformationVisibilityClassification.Medical,
                KnowledgeVisibility.DevelopmentOnly => InformationVisibilityClassification.Sealed,
                _ => InformationVisibilityClassification.Unknown
            };
        }

        public static InformationVisibilityClassification FromSourcePrivacy(SourcePrivacyLevel privacy)
        {
            return privacy switch
            {
                SourcePrivacyLevel.Public => InformationVisibilityClassification.Public,
                SourcePrivacyLevel.Shared => InformationVisibilityClassification.Open,
                SourcePrivacyLevel.Personal => InformationVisibilityClassification.Personal,
                SourcePrivacyLevel.Private => InformationVisibilityClassification.Private,
                SourcePrivacyLevel.Hidden => InformationVisibilityClassification.Hidden,
                SourcePrivacyLevel.Secret => InformationVisibilityClassification.Secret,
                _ => InformationVisibilityClassification.Unknown
            };
        }
    }
}
