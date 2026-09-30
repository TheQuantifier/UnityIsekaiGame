using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace UnityIsekaiGame.Knowledge.Access
{
    [Serializable]
    public sealed class InformationSubjectReferenceData
    {
        public InformationSubjectType subjectType;
        public string subjectId;
        public string parentSubjectId;
        public string ownerPersonId;
        public string controllingEntityId;
        public string[] tags = Array.Empty<string>();

        public InformationSubjectReferenceData Clone()
        {
            return new InformationSubjectReferenceData
            {
                subjectType = subjectType,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                controllingEntityId = controllingEntityId ?? string.Empty,
                tags = tags == null ? Array.Empty<string>() : tags.ToArray()
            };
        }
    }

    public sealed class InformationSubjectReference
    {
        public InformationSubjectReference(InformationSubjectReferenceData data)
        {
            Data = data?.Clone() ?? new InformationSubjectReferenceData();
        }

        public InformationSubjectReferenceData Data { get; }
        public InformationSubjectType SubjectType => Data.subjectType;
        public string SubjectId => Data.subjectId ?? string.Empty;
        public string ParentSubjectId => Data.parentSubjectId ?? string.Empty;
        public string OwnerPersonId => Data.ownerPersonId ?? string.Empty;
        public string ControllingEntityId => Data.controllingEntityId ?? string.Empty;
        public IReadOnlyList<string> Tags => Data.tags ?? Array.Empty<string>();
    }

    [Serializable]
    public sealed class InformationAccessPolicyData
    {
        public string policyId;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public InformationVisibilityClassification classification = InformationVisibilityClassification.Public;
        public InformationDisclosurePolicy disclosurePolicy = InformationDisclosurePolicy.SameAsAccess;
        public InformationResharingPolicy resharingPolicy = InformationResharingPolicy.FreelyReshareable;
        public InformationSourceVisibilityPolicy sourceVisibilityPolicy = InformationSourceVisibilityPolicy.Reveal;
        public InformationDetailVisibilityPolicy detailVisibilityPolicy = InformationDetailVisibilityPolicy.All;
        public InformationAuditPolicy auditPolicy = InformationAuditPolicy.None;
        public string[] allowedPersonIds = Array.Empty<string>();
        public string[] deniedPersonIds = Array.Empty<string>();
        public string[] allowedOrganizationIds = Array.Empty<string>();
        public string[] deniedOrganizationIds = Array.Empty<string>();
        public string[] allowedRoleIds = Array.Empty<string>();
        public string[] deniedRoleIds = Array.Empty<string>();
        public string[] allowedTitleOrStatusIds = Array.Empty<string>();
        public string[] deniedTitleOrStatusIds = Array.Empty<string>();
        public string[] participantPersonIds = Array.Empty<string>();
        public string[] witnessPersonIds = Array.Empty<string>();
        public string[] recipientPersonIds = Array.Empty<string>();
        public string[] needToKnowTags = Array.Empty<string>();
        public string[] requiredAuthorizationIds = Array.Empty<string>();
        public string[] defaultVisibleDetails = Array.Empty<string>();
        public string[] defaultRedactedDetails = Array.Empty<string>();
        public string[] defaultHiddenDetails = Array.Empty<string>();
        public double effectiveStartTime;
        public double expirationTime = -1d;
        public bool revoked;
        public bool inheritFromParent = true;
        public bool redactedAccessAcceptable = true;
        public bool discoveryRequired;
        public string provenance;
        public long revision = 1L;

        public InformationAccessPolicyData Clone()
        {
            return new InformationAccessPolicyData
            {
                policyId = policyId ?? string.Empty,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                classification = classification,
                disclosurePolicy = disclosurePolicy,
                resharingPolicy = resharingPolicy,
                sourceVisibilityPolicy = sourceVisibilityPolicy,
                detailVisibilityPolicy = detailVisibilityPolicy,
                auditPolicy = auditPolicy,
                allowedPersonIds = CloneArray(allowedPersonIds),
                deniedPersonIds = CloneArray(deniedPersonIds),
                allowedOrganizationIds = CloneArray(allowedOrganizationIds),
                deniedOrganizationIds = CloneArray(deniedOrganizationIds),
                allowedRoleIds = CloneArray(allowedRoleIds),
                deniedRoleIds = CloneArray(deniedRoleIds),
                allowedTitleOrStatusIds = CloneArray(allowedTitleOrStatusIds),
                deniedTitleOrStatusIds = CloneArray(deniedTitleOrStatusIds),
                participantPersonIds = CloneArray(participantPersonIds),
                witnessPersonIds = CloneArray(witnessPersonIds),
                recipientPersonIds = CloneArray(recipientPersonIds),
                needToKnowTags = CloneArray(needToKnowTags),
                requiredAuthorizationIds = CloneArray(requiredAuthorizationIds),
                defaultVisibleDetails = CloneArray(defaultVisibleDetails),
                defaultRedactedDetails = CloneArray(defaultRedactedDetails),
                defaultHiddenDetails = CloneArray(defaultHiddenDetails),
                effectiveStartTime = effectiveStartTime,
                expirationTime = expirationTime,
                revoked = revoked,
                inheritFromParent = inheritFromParent,
                redactedAccessAcceptable = redactedAccessAcceptable,
                discoveryRequired = discoveryRequired,
                provenance = provenance ?? string.Empty,
                revision = revision
            };
        }

        internal static string[] CloneArray(string[] values)
        {
            return values == null ? Array.Empty<string>() : values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    public sealed class InformationAccessPolicyRecord
    {
        public InformationAccessPolicyRecord(InformationAccessPolicyData data)
        {
            Data = data?.Clone() ?? new InformationAccessPolicyData();
        }

        public InformationAccessPolicyData Data { get; }
        public string PolicyId => Data.policyId ?? string.Empty;
        public InformationSubjectReference Subject => new InformationSubjectReference(Data.subject);
        public InformationVisibilityClassification Classification => Data.classification;
        public InformationDisclosurePolicy DisclosurePolicy => Data.disclosurePolicy;
        public InformationResharingPolicy ResharingPolicy => Data.resharingPolicy;
        public InformationSourceVisibilityPolicy SourceVisibilityPolicy => Data.sourceVisibilityPolicy;
        public InformationDetailVisibilityPolicy DetailVisibilityPolicy => Data.detailVisibilityPolicy;
        public IReadOnlyList<string> VisibleDetails => Data.defaultVisibleDetails ?? Array.Empty<string>();
        public IReadOnlyList<string> RedactedDetails => Data.defaultRedactedDetails ?? Array.Empty<string>();
        public IReadOnlyList<string> HiddenDetails => Data.defaultHiddenDetails ?? Array.Empty<string>();
        public long Revision => Data.revision;
    }

    [Serializable]
    public sealed class InformationAccessGrantData
    {
        public string grantId;
        public string policyId;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public InformationGranteeKind granteeKind = InformationGranteeKind.Person;
        public string granteeId;
        public string grantorId;
        public InformationAccessMode[] accessModes = Array.Empty<InformationAccessMode>();
        public string[] detailIds = Array.Empty<string>();
        public InformationSourceVisibilityPolicy sourceVisibility = InformationSourceVisibilityPolicy.PrivilegedOnly;
        public bool permitsDisclosure;
        public bool permitsResharing;
        public double effectiveStartTime;
        public double expirationTime = -1d;
        public bool revoked;
        public string reason;
        public string provenance;
        public bool transferable;
        public InformationAuditPolicy auditPolicy = InformationAuditPolicy.None;
        public long revision = 1L;

        public InformationAccessGrantData Clone()
        {
            return new InformationAccessGrantData
            {
                grantId = grantId ?? string.Empty,
                policyId = policyId ?? string.Empty,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                granteeKind = granteeKind,
                granteeId = granteeId ?? string.Empty,
                grantorId = grantorId ?? string.Empty,
                accessModes = accessModes == null ? Array.Empty<InformationAccessMode>() : accessModes.ToArray(),
                detailIds = InformationAccessPolicyData.CloneArray(detailIds),
                sourceVisibility = sourceVisibility,
                permitsDisclosure = permitsDisclosure,
                permitsResharing = permitsResharing,
                effectiveStartTime = effectiveStartTime,
                expirationTime = expirationTime,
                revoked = revoked,
                reason = reason ?? string.Empty,
                provenance = provenance ?? string.Empty,
                transferable = transferable,
                auditPolicy = auditPolicy,
                revision = revision
            };
        }
    }

    public sealed class InformationAccessGrantRecord
    {
        public InformationAccessGrantRecord(InformationAccessGrantData data)
        {
            Data = data?.Clone() ?? new InformationAccessGrantData();
        }

        public InformationAccessGrantData Data { get; }
        public string GrantId => Data.grantId ?? string.Empty;
        public string PolicyId => Data.policyId ?? string.Empty;
        public InformationGranteeKind GranteeKind => Data.granteeKind;
        public string GranteeId => Data.granteeId ?? string.Empty;
        public IReadOnlyList<InformationAccessMode> AccessModes => Data.accessModes ?? Array.Empty<InformationAccessMode>();
        public IReadOnlyList<string> DetailIds => Data.detailIds ?? Array.Empty<string>();
        public bool Revoked => Data.revoked;
        public long Revision => Data.revision;
    }

    [Serializable]
    public sealed class InformationAccessDenialData
    {
        public string denialId;
        public string policyId;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public InformationGranteeKind deniedKind = InformationGranteeKind.Person;
        public string deniedId;
        public InformationAccessMode[] accessModes = Array.Empty<InformationAccessMode>();
        public string reason;
        public double effectiveStartTime;
        public double expirationTime = -1d;
        public bool revoked;
        public long revision = 1L;

        public InformationAccessDenialData Clone()
        {
            return new InformationAccessDenialData
            {
                denialId = denialId ?? string.Empty,
                policyId = policyId ?? string.Empty,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                deniedKind = deniedKind,
                deniedId = deniedId ?? string.Empty,
                accessModes = accessModes == null ? Array.Empty<InformationAccessMode>() : accessModes.ToArray(),
                reason = reason ?? string.Empty,
                effectiveStartTime = effectiveStartTime,
                expirationTime = expirationTime,
                revoked = revoked,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class InformationConcealmentData
    {
        public string concealmentId;
        public string policyId;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public string concealingEntityId;
        public InformationConcealmentKind concealmentKind = InformationConcealmentKind.Existence;
        public double startTime;
        public double endTime = -1d;
        public int strength;
        public string[] hiddenDetailIds = Array.Empty<string>();
        public string[] authorizedExceptionIds = Array.Empty<string>();
        public string[] allowedDiscoveryMethodIds = Array.Empty<string>();
        public string provenance;
        public bool active = true;
        public string revisionLinkId;
        public long revision = 1L;

        public InformationConcealmentData Clone()
        {
            return new InformationConcealmentData
            {
                concealmentId = concealmentId ?? string.Empty,
                policyId = policyId ?? string.Empty,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                concealingEntityId = concealingEntityId ?? string.Empty,
                concealmentKind = concealmentKind,
                startTime = startTime,
                endTime = endTime,
                strength = Math.Max(0, strength),
                hiddenDetailIds = InformationAccessPolicyData.CloneArray(hiddenDetailIds),
                authorizedExceptionIds = InformationAccessPolicyData.CloneArray(authorizedExceptionIds),
                allowedDiscoveryMethodIds = InformationAccessPolicyData.CloneArray(allowedDiscoveryMethodIds),
                provenance = provenance ?? string.Empty,
                active = active,
                revisionLinkId = revisionLinkId ?? string.Empty,
                revision = revision
            };
        }
    }

    public sealed class InformationConcealmentRecord
    {
        public InformationConcealmentRecord(InformationConcealmentData data)
        {
            Data = data?.Clone() ?? new InformationConcealmentData();
        }

        public InformationConcealmentData Data { get; }
        public string ConcealmentId => Data.concealmentId ?? string.Empty;
        public InformationConcealmentKind ConcealmentKind => Data.concealmentKind;
        public IReadOnlyList<string> HiddenDetailIds => Data.hiddenDetailIds ?? Array.Empty<string>();
        public bool Active => Data.active;
    }

    [Serializable]
    public sealed class InformationClassificationRevisionData
    {
        public string revisionId;
        public string policyId;
        public InformationVisibilityClassification previousClassification;
        public InformationVisibilityClassification newClassification;
        public string actorId;
        public double worldTimeSeconds;
        public string reason;
        public long revision;

        public InformationClassificationRevisionData Clone()
        {
            return new InformationClassificationRevisionData
            {
                revisionId = revisionId ?? string.Empty,
                policyId = policyId ?? string.Empty,
                previousClassification = previousClassification,
                newClassification = newClassification,
                actorId = actorId ?? string.Empty,
                worldTimeSeconds = Math.Max(0d, worldTimeSeconds),
                reason = reason ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class InformationAccessAuditData
    {
        public string auditId;
        public string policyId;
        public InformationSubjectReferenceData subject = new InformationSubjectReferenceData();
        public string requesterPersonId;
        public InformationAccessMode mode;
        public InformationAccessDecisionKind decision;
        public InformationAccessDenialCode denialCode;
        public bool unauthorized;
        public bool gameplayAudit;
        public double worldTimeSeconds;
        public string visibleReason;
        public string diagnosticReason;
        public long revision;

        public InformationAccessAuditData Clone()
        {
            return new InformationAccessAuditData
            {
                auditId = auditId ?? string.Empty,
                policyId = policyId ?? string.Empty,
                subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                requesterPersonId = requesterPersonId ?? string.Empty,
                mode = mode,
                decision = decision,
                denialCode = denialCode,
                unauthorized = unauthorized,
                gameplayAudit = gameplayAudit,
                worldTimeSeconds = Math.Max(0d, worldTimeSeconds),
                visibleReason = visibleReason ?? string.Empty,
                diagnosticReason = diagnosticReason ?? string.Empty,
                revision = revision
            };
        }
    }

    public sealed class InformationAccessContext
    {
        public string RequestingPersonId { get; set; }
        public string ActingEntityId { get; set; }
        public InformationSubjectReferenceData Subject { get; set; }
        public InformationAccessPurpose Purpose { get; set; } = InformationAccessPurpose.Gameplay;
        public double WorldTimeSeconds { get; set; }
        public InformationAccessMode AccessMode { get; set; } = InformationAccessMode.Inspect;
        public string[] RequestedDetailIds { get; set; } = Array.Empty<string>();
        public string[] AuthorizationIds { get; set; } = Array.Empty<string>();
        public string[] OrganizationIds { get; set; } = Array.Empty<string>();
        public string[] RoleIds { get; set; } = Array.Empty<string>();
        public string[] TitleOrStatusIds { get; set; } = Array.Empty<string>();
        public string[] NeedToKnowTags { get; set; } = Array.Empty<string>();
        public bool IsParticipant { get; set; }
        public bool IsWitness { get; set; }
        public bool IsRecipient { get; set; }
        public bool HasDiscoveredSubject { get; set; }
        public bool KnowsSource { get; set; }
        public InformationContextKind ContextKind { get; set; } = InformationContextKind.Gameplay;
        public bool RedactedAccessAcceptable { get; set; } = true;
        public bool RevealDenialReasons { get; set; }
        public string DeterministicPolicyId { get; set; }

        public bool IsPrivileged => ContextKind == InformationContextKind.Debug || ContextKind == InformationContextKind.Persistence || ContextKind == InformationContextKind.Validation || ContextKind == InformationContextKind.AuthoredSetup;
    }

    public sealed class InformationAccessDecision
    {
        public InformationAccessDecision(
            string requesterPersonId,
            InformationSubjectReferenceData subject,
            InformationAccessMode mode,
            InformationAccessDecisionKind decision,
            InformationAccessDenialCode denialCode,
            bool sourceVisible,
            InformationResharingPolicy resharingOutcome,
            IReadOnlyList<string> allowedDetails,
            IReadOnlyList<string> redactedDetails,
            IReadOnlyList<string> hiddenDetails,
            IReadOnlyList<string> policyIds,
            double effectiveTime,
            string visibleReason,
            string diagnosticReason,
            bool auditRequired)
        {
            RequesterPersonId = requesterPersonId ?? string.Empty;
            Subject = new InformationSubjectReference(subject);
            Mode = mode;
            Decision = decision;
            DenialCode = denialCode;
            SourceVisible = sourceVisible;
            ResharingOutcome = resharingOutcome;
            AllowedDetails = Array.AsReadOnly((allowedDetails ?? Array.Empty<string>()).ToArray());
            RedactedDetails = Array.AsReadOnly((redactedDetails ?? Array.Empty<string>()).ToArray());
            HiddenDetails = Array.AsReadOnly((hiddenDetails ?? Array.Empty<string>()).ToArray());
            PolicyIds = Array.AsReadOnly((policyIds ?? Array.Empty<string>()).ToArray());
            EffectiveTime = effectiveTime;
            VisibleReason = visibleReason ?? string.Empty;
            DiagnosticReason = diagnosticReason ?? string.Empty;
            AuditRequired = auditRequired;
        }

        public string RequesterPersonId { get; }
        public InformationSubjectReference Subject { get; }
        public InformationAccessMode Mode { get; }
        public InformationAccessDecisionKind Decision { get; }
        public InformationAccessDenialCode DenialCode { get; }
        public bool FullAccess => Decision == InformationAccessDecisionKind.FullAccess;
        public bool RedactedAccess => Decision == InformationAccessDecisionKind.RedactedAccess;
        public bool PartialAccess => Decision == InformationAccessDecisionKind.PartialAccess;
        public bool ConditionalAccess => Decision == InformationAccessDecisionKind.ConditionalAccess;
        public bool Denied => Decision == InformationAccessDecisionKind.Denied || Decision == InformationAccessDecisionKind.Expired || Decision == InformationAccessDecisionKind.Revoked || Decision == InformationAccessDecisionKind.MissingAuthorization || Decision == InformationAccessDecisionKind.NotDiscovered;
        public bool SourceVisible { get; }
        public InformationResharingPolicy ResharingOutcome { get; }
        public IReadOnlyList<string> AllowedDetails { get; }
        public IReadOnlyList<string> RedactedDetails { get; }
        public IReadOnlyList<string> HiddenDetails { get; }
        public IReadOnlyList<string> PolicyIds { get; }
        public double EffectiveTime { get; }
        public string VisibleReason { get; }
        public string DiagnosticReason { get; }
        public bool AuditRequired { get; }
    }

    public sealed class RedactedInformationProjection
    {
        public RedactedInformationProjection(InformationSubjectReferenceData subject, InformationAccessDecision decision, IReadOnlyDictionary<string, InformationRedactionState> details)
        {
            Subject = new InformationSubjectReference(subject);
            Decision = decision;
            Details = new ReadOnlyDictionary<string, InformationRedactionState>(new Dictionary<string, InformationRedactionState>(details ?? new Dictionary<string, InformationRedactionState>(), StringComparer.Ordinal));
        }

        public InformationSubjectReference Subject { get; }
        public InformationAccessDecision Decision { get; }
        public IReadOnlyDictionary<string, InformationRedactionState> Details { get; }
    }

    public sealed class InformationAccessOperationResult
    {
        private InformationAccessOperationResult(bool succeeded, InformationAccessResultCode code, string message, string transactionId, bool preview, bool duplicate, long priorRevision, long resultingRevision, InformationAccessDecision decision)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            TransactionId = transactionId ?? string.Empty;
            Preview = preview;
            Duplicate = duplicate;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Decision = decision;
        }

        public bool Succeeded { get; }
        public InformationAccessResultCode Code { get; }
        public string Message { get; }
        public string TransactionId { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public InformationAccessDecision Decision { get; }

        public static InformationAccessOperationResult Success(string message, string transactionId, long priorRevision, long resultingRevision, InformationAccessDecision decision = null, bool preview = false, bool duplicate = false)
        {
            return new InformationAccessOperationResult(true, preview ? InformationAccessResultCode.Preview : duplicate ? InformationAccessResultCode.Duplicate : InformationAccessResultCode.Success, message, transactionId, preview, duplicate, priorRevision, resultingRevision, decision);
        }

        public static InformationAccessOperationResult Failure(InformationAccessResultCode code, string message, string transactionId = "", bool preview = false, long revision = 0, InformationAccessDecision decision = null)
        {
            return new InformationAccessOperationResult(false, code, message, transactionId, preview, false, revision, revision, decision);
        }
    }

    [Serializable]
    public sealed class InformationAccessProcessedTransactionData
    {
        public string transactionId;
        public string operation;
        public string recordId;
        public long revision;
    }

    [Serializable]
    public sealed class InformationAccessSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string ownerId;
        public long accessRevision;
        public InformationAccessPolicyData[] policies = Array.Empty<InformationAccessPolicyData>();
        public InformationAccessGrantData[] grants = Array.Empty<InformationAccessGrantData>();
        public InformationAccessDenialData[] denials = Array.Empty<InformationAccessDenialData>();
        public InformationConcealmentData[] concealments = Array.Empty<InformationConcealmentData>();
        public InformationClassificationRevisionData[] classificationRevisions = Array.Empty<InformationClassificationRevisionData>();
        public InformationAccessAuditData[] audits = Array.Empty<InformationAccessAuditData>();
        public InformationAccessProcessedTransactionData[] processedTransactions = Array.Empty<InformationAccessProcessedTransactionData>();
    }

    public sealed class InformationAccessSnapshot
    {
        public InformationAccessSnapshot(string ownerId, long revision, IReadOnlyList<InformationAccessPolicyRecord> policies, IReadOnlyList<InformationAccessGrantRecord> grants, IReadOnlyList<InformationConcealmentRecord> concealments, IReadOnlyList<InformationAccessAuditData> audits)
        {
            OwnerId = ownerId ?? string.Empty;
            Revision = revision;
            Policies = Array.AsReadOnly((policies ?? Array.Empty<InformationAccessPolicyRecord>()).ToArray());
            Grants = Array.AsReadOnly((grants ?? Array.Empty<InformationAccessGrantRecord>()).ToArray());
            Concealments = Array.AsReadOnly((concealments ?? Array.Empty<InformationConcealmentRecord>()).ToArray());
            Audits = Array.AsReadOnly((audits ?? Array.Empty<InformationAccessAuditData>()).Select(audit => audit.Clone()).ToArray());
        }

        public string OwnerId { get; }
        public long Revision { get; }
        public IReadOnlyList<InformationAccessPolicyRecord> Policies { get; }
        public IReadOnlyList<InformationAccessGrantRecord> Grants { get; }
        public IReadOnlyList<InformationConcealmentRecord> Concealments { get; }
        public IReadOnlyList<InformationAccessAuditData> Audits { get; }
    }
}
