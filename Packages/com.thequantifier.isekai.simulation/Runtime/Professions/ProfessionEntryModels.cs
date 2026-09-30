using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Requirements;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class ProfessionEntrySkillStateData
    {
        public string skillId;
        public int grade;

        public ProfessionEntrySkillStateData Clone()
        {
            return new ProfessionEntrySkillStateData { skillId = skillId ?? string.Empty, grade = grade };
        }
    }

    [Serializable]
    public sealed class ProfessionEntryRuntimeTokenData
    {
        public long professionRevision;
        public long knowledgeRevision;
        public long organizationRevision;
        public long accessRevision;
        public long statusRevision;
        public long bodyRevision;
        public long activityRevision;
        public string contextHash;

        public ProfessionEntryRuntimeTokenData Clone()
        {
            return new ProfessionEntryRuntimeTokenData
            {
                professionRevision = professionRevision,
                knowledgeRevision = knowledgeRevision,
                organizationRevision = organizationRevision,
                accessRevision = accessRevision,
                statusRevision = statusRevision,
                bodyRevision = bodyRevision,
                activityRevision = activityRevision,
                contextHash = contextHash ?? string.Empty
            };
        }

        public bool SemanticallyEquals(ProfessionEntryRuntimeTokenData other)
        {
            return other != null
                && professionRevision == other.professionRevision
                && knowledgeRevision == other.knowledgeRevision
                && organizationRevision == other.organizationRevision
                && accessRevision == other.accessRevision
                && statusRevision == other.statusRevision
                && bodyRevision == other.bodyRevision
                && activityRevision == other.activityRevision
                && string.Equals(contextHash ?? string.Empty, other.contextHash ?? string.Empty, StringComparison.Ordinal);
        }
    }

    public sealed class ProfessionEligibilityContext
    {
        private readonly string[] knowledgeSubjectIds;
        private readonly string[] capabilityIds;
        private readonly string[] traitIds;
        private readonly string[] statusIds;
        private readonly string[] organizationIds;
        private readonly string[] accessKeys;
        private readonly string[] lifeStageIds;
        private readonly string[] activeActivityIds;
        private readonly ProfessionEntrySkillStateData[] skillStates;

        public ProfessionEligibilityContext(
            string personId,
            string professionId,
            string entryPathId,
            string specializationId = "",
            bool formal = false,
            bool selfDeclared = false,
            string authorityId = "",
            string sponsorPersonId = "",
            double worldTime = 0d,
            int age = 0,
            string correlationId = "",
            bool preview = true,
            RequirementEvaluationContext requirementContext = null,
            IEnumerable<ProfessionEntrySkillStateData> skills = null,
            IEnumerable<string> knowledgeSubjects = null,
            IEnumerable<string> capabilities = null,
            IEnumerable<string> traits = null,
            IEnumerable<string> statuses = null,
            IEnumerable<string> organizations = null,
            IEnumerable<string> access = null,
            IEnumerable<string> lifeStages = null,
            IEnumerable<string> activeActivities = null,
            ProfessionEntryRuntimeTokenData expectedRuntimeToken = null)
        {
            PersonId = personId ?? string.Empty;
            ProfessionId = professionId ?? string.Empty;
            EntryPathId = entryPathId ?? string.Empty;
            SpecializationId = specializationId ?? string.Empty;
            Formal = formal;
            SelfDeclared = selfDeclared;
            AuthorityId = authorityId ?? string.Empty;
            SponsorPersonId = sponsorPersonId ?? string.Empty;
            WorldTime = worldTime;
            Age = Math.Max(0, age);
            CorrelationId = correlationId ?? string.Empty;
            Preview = preview;
            RequirementContext = requirementContext;
            skillStates = (skills ?? Array.Empty<ProfessionEntrySkillStateData>())
                .Where(skill => skill != null && !string.IsNullOrWhiteSpace(skill.skillId))
                .Select(skill => skill.Clone())
                .OrderBy(skill => skill.skillId, StringComparer.Ordinal)
                .ToArray();
            knowledgeSubjectIds = Clean(knowledgeSubjects);
            capabilityIds = Clean(capabilities);
            traitIds = Clean(traits);
            statusIds = Clean(statuses);
            organizationIds = Clean(organizations);
            accessKeys = Clean(access);
            lifeStageIds = Clean(lifeStages);
            activeActivityIds = Clean(activeActivities);
            ExpectedRuntimeToken = expectedRuntimeToken?.Clone();
        }

        public string PersonId { get; }
        public string ProfessionId { get; }
        public string EntryPathId { get; }
        public string SpecializationId { get; }
        public bool Formal { get; }
        public bool SelfDeclared { get; }
        public string AuthorityId { get; }
        public string SponsorPersonId { get; }
        public double WorldTime { get; }
        public int Age { get; }
        public string CorrelationId { get; }
        public bool Preview { get; }
        public RequirementEvaluationContext RequirementContext { get; }
        public ProfessionEntryRuntimeTokenData ExpectedRuntimeToken { get; }
        public IReadOnlyList<ProfessionEntrySkillStateData> SkillStates => skillStates.Select(skill => skill.Clone()).ToArray();
        public IReadOnlyList<string> KnowledgeSubjectIds => knowledgeSubjectIds;
        public IReadOnlyList<string> CapabilityIds => capabilityIds;
        public IReadOnlyList<string> TraitIds => traitIds;
        public IReadOnlyList<string> StatusIds => statusIds;
        public IReadOnlyList<string> OrganizationIds => organizationIds;
        public IReadOnlyList<string> AccessKeys => accessKeys;
        public IReadOnlyList<string> LifeStageIds => lifeStageIds;
        public IReadOnlyList<string> ActiveActivityIds => activeActivityIds;

        internal string CreateContextHash()
        {
            return string.Join("|",
                PersonId,
                ProfessionId,
                EntryPathId,
                SpecializationId,
                Formal ? "formal" : "informal",
                SelfDeclared ? "self" : "sponsored",
                AuthorityId,
                SponsorPersonId,
                WorldTime.ToString("R"),
                Age.ToString(),
                Join(skillStates.Select(skill => $"{skill.skillId}:{skill.grade}")),
                Join(knowledgeSubjectIds),
                Join(capabilityIds),
                Join(traitIds),
                Join(statusIds),
                Join(organizationIds),
                Join(accessKeys),
                Join(lifeStageIds),
                Join(activeActivityIds));
        }

        private static string Join(IEnumerable<string> values)
        {
            return string.Join(",", (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).OrderBy(value => value, StringComparer.Ordinal));
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

    public sealed class ProfessionEligibilityRequirementResult
    {
        public ProfessionEligibilityRequirementResult(string requirementId, bool passed, ProfessionEligibilityStatus status, string message, bool hidden = false)
        {
            RequirementId = requirementId ?? string.Empty;
            Passed = passed;
            Status = status;
            Message = message ?? string.Empty;
            Hidden = hidden;
        }

        public string RequirementId { get; }
        public bool Passed { get; }
        public ProfessionEligibilityStatus Status { get; }
        public string Message { get; }
        public bool Hidden { get; }
    }

    public sealed class ProfessionEligibilityResult
    {
        private readonly ProfessionEligibilityRequirementResult[] requirementResults;
        private readonly string[] failures;
        private readonly string[] optionalUnmet;
        private readonly string[] alternatives;
        private readonly string[] conflicts;
        private readonly string[] warnings;

        private ProfessionEligibilityResult(
            bool succeeded,
            bool preview,
            bool perceived,
            ProfessionEligibilityStatus status,
            string message,
            string personId,
            string professionId,
            string entryPathId,
            string specializationId,
            string authorityId,
            ProfessionEntryRuntimeTokenData runtimeToken,
            IEnumerable<ProfessionEligibilityRequirementResult> requirements,
            IEnumerable<string> failures,
            IEnumerable<string> optionalUnmet,
            IEnumerable<string> alternatives,
            IEnumerable<string> conflicts,
            IEnumerable<string> warnings,
            long priorRevision,
            long resultingRevision)
        {
            Succeeded = succeeded;
            Preview = preview;
            Perceived = perceived;
            Status = status;
            Message = message ?? string.Empty;
            PersonId = personId ?? string.Empty;
            ProfessionId = professionId ?? string.Empty;
            EntryPathId = entryPathId ?? string.Empty;
            SpecializationId = specializationId ?? string.Empty;
            AuthorityId = authorityId ?? string.Empty;
            RuntimeToken = runtimeToken?.Clone();
            requirementResults = (requirements ?? Array.Empty<ProfessionEligibilityRequirementResult>()).ToArray();
            this.failures = Clean(failures);
            this.optionalUnmet = Clean(optionalUnmet);
            this.alternatives = Clean(alternatives);
            this.conflicts = Clean(conflicts);
            this.warnings = Clean(warnings);
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Perceived { get; }
        public ProfessionEligibilityStatus Status { get; }
        public string Message { get; }
        public string PersonId { get; }
        public string ProfessionId { get; }
        public string EntryPathId { get; }
        public string SpecializationId { get; }
        public string AuthorityId { get; }
        public ProfessionEntryRuntimeTokenData RuntimeToken { get; }
        public IReadOnlyList<ProfessionEligibilityRequirementResult> RequirementResults => requirementResults.ToArray();
        public IReadOnlyList<string> Failures => failures;
        public IReadOnlyList<string> OptionalUnmet => optionalUnmet;
        public IReadOnlyList<string> Alternatives => alternatives;
        public IReadOnlyList<string> Conflicts => conflicts;
        public IReadOnlyList<string> Warnings => warnings;
        public long PriorRevision { get; }
        public long ResultingRevision { get; }

        public static ProfessionEligibilityResult Success(ProfessionEligibilityContext context, ProfessionEntryRuntimeTokenData token, IEnumerable<ProfessionEligibilityRequirementResult> requirements, long revision, bool perceived = false)
        {
            return new ProfessionEligibilityResult(true, context?.Preview ?? true, perceived, context?.Preview == true ? ProfessionEligibilityStatus.Preview : ProfessionEligibilityStatus.Succeeded, "Profession entry eligibility satisfied.", context?.PersonId, context?.ProfessionId, context?.EntryPathId, context?.SpecializationId, context?.AuthorityId, token, requirements, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), revision, revision);
        }

        public static ProfessionEligibilityResult Failure(ProfessionEligibilityContext context, ProfessionEligibilityStatus status, string message, ProfessionEntryRuntimeTokenData token, IEnumerable<ProfessionEligibilityRequirementResult> requirements, IEnumerable<string> failures, IEnumerable<string> conflicts, long revision, bool perceived = false)
        {
            return new ProfessionEligibilityResult(false, context?.Preview ?? true, perceived, status, message, context?.PersonId, context?.ProfessionId, context?.EntryPathId, context?.SpecializationId, context?.AuthorityId, token, requirements, failures, Array.Empty<string>(), Array.Empty<string>(), conflicts, Array.Empty<string>(), revision, revision);
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

    [Serializable]
    public sealed class ProfessionEntryRequestData
    {
        public string requestId;
        public string applicantPersonId;
        public string professionId;
        public string entryPathId;
        public string specializationId;
        public string authorityId;
        public string sponsorPersonId;
        public string submittedWorldTime;
        public ProfessionEntryRequestState state = ProfessionEntryRequestState.Draft;
        public string evaluationSummary;
        public ProfessionEntryRuntimeTokenData evaluationToken;
        public List<ProfessionEntrySkillStateData> skillStates = new List<ProfessionEntrySkillStateData>();
        public string[] knowledgeSubjectIds = Array.Empty<string>();
        public string[] capabilityIds = Array.Empty<string>();
        public string[] traitIds = Array.Empty<string>();
        public string[] statusIds = Array.Empty<string>();
        public string[] organizationIds = Array.Empty<string>();
        public string[] accessKeys = Array.Empty<string>();
        public string[] lifeStageIds = Array.Empty<string>();
        public string[] activeActivityIds = Array.Empty<string>();
        public int age;
        public string relationshipId;
        public string accessPolicyId;
        public string provenanceId;
        public long revision = 1L;

        public ProfessionEntryRequestData Clone()
        {
            return new ProfessionEntryRequestData
            {
                requestId = requestId ?? string.Empty,
                applicantPersonId = applicantPersonId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                entryPathId = entryPathId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                sponsorPersonId = sponsorPersonId ?? string.Empty,
                submittedWorldTime = submittedWorldTime ?? string.Empty,
                state = state,
                evaluationSummary = evaluationSummary ?? string.Empty,
                evaluationToken = evaluationToken?.Clone(),
                skillStates = skillStates == null ? new List<ProfessionEntrySkillStateData>() : skillStates.Select(skill => skill?.Clone()).Where(skill => skill != null).ToList(),
                knowledgeSubjectIds = Clean(knowledgeSubjectIds),
                capabilityIds = Clean(capabilityIds),
                traitIds = Clean(traitIds),
                statusIds = Clean(statusIds),
                organizationIds = Clean(organizationIds),
                accessKeys = Clean(accessKeys),
                lifeStageIds = Clean(lifeStageIds),
                activeActivityIds = Clean(activeActivityIds),
                age = age,
                relationshipId = relationshipId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
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

    [Serializable]
    public sealed class ProfessionEntryRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<ProfessionEntryRequestData> requests = new List<ProfessionEntryRequestData>();

        public ProfessionEntryRuntimeSaveData Clone()
        {
            return new ProfessionEntryRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                requests = requests == null
                    ? new List<ProfessionEntryRequestData>()
                    : requests.Select(request => request?.Clone()).Where(request => request != null).ToList()
            };
        }
    }

    public sealed class ProfessionEntryRequestSnapshot
    {
        public ProfessionEntryRequestSnapshot(ProfessionEntryRequestData data)
        {
            Data = data?.Clone() ?? new ProfessionEntryRequestData();
        }

        public ProfessionEntryRequestData Data { get; }
        public string RequestId => Data.requestId ?? string.Empty;
        public string ApplicantPersonId => Data.applicantPersonId ?? string.Empty;
        public string ProfessionId => Data.professionId ?? string.Empty;
        public string EntryPathId => Data.entryPathId ?? string.Empty;
        public string SpecializationId => Data.specializationId ?? string.Empty;
        public string AuthorityId => Data.authorityId ?? string.Empty;
        public ProfessionEntryRequestState State => Data.state;
        public long Revision => Data.revision;
    }

    public sealed class ProfessionEntryProjection<TRecord>
    {
        public ProfessionEntryProjection(TRecord record, ProfessionEntryProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
        {
            Record = record;
            Audience = audience;
            Decision = decision;
            Redacted = redacted;
            Denied = denied;
            VisibleFields = (visibleFields ?? Array.Empty<string>()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
        }

        public TRecord Record { get; }
        public ProfessionEntryProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public sealed class ProfessionEntryHistoryHookData
    {
        public ProfessionEntryHistoryHookKind kind;
        public string personId;
        public string professionId;
        public string entryPathId;
        public string specializationId;
        public string authorityId;
        public string requestId;
        public string relationshipId;
        public string worldTime;
        public string transactionId;

        public ProfessionEntryHistoryHookData Clone()
        {
            return new ProfessionEntryHistoryHookData
            {
                kind = kind,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                entryPathId = entryPathId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                requestId = requestId ?? string.Empty,
                relationshipId = relationshipId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public sealed class ProfessionEntryOperationResult
    {
        private ProfessionEntryOperationResult(bool succeeded, bool preview, bool duplicate, ProfessionEntryOperationStatus status, string message, long priorRevision, long resultingRevision, ProfessionEntryRequestSnapshot request, PersonProfessionSnapshot relationship)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Request = request;
            Relationship = relationship;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public ProfessionEntryOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public ProfessionEntryRequestSnapshot Request { get; }
        public PersonProfessionSnapshot Relationship { get; }

        public static ProfessionEntryOperationResult Success(string message, long priorRevision, long resultingRevision, ProfessionEntryRequestSnapshot request = null, PersonProfessionSnapshot relationship = null, bool preview = false, bool duplicate = false)
        {
            return new ProfessionEntryOperationResult(true, preview, duplicate, preview ? ProfessionEntryOperationStatus.Preview : duplicate ? ProfessionEntryOperationStatus.Duplicate : ProfessionEntryOperationStatus.Succeeded, message, priorRevision, resultingRevision, request, relationship);
        }

        public static ProfessionEntryOperationResult Failure(ProfessionEntryOperationStatus status, string message, long revision = 0L)
        {
            return new ProfessionEntryOperationResult(false, false, false, status, message, revision, revision, null, null);
        }
    }

    public static class ProfessionEntryInformationSubject
    {
        public const string EntryPathTag = "subject-type:profession-entry-path";
        public const string EligibilityTag = "subject-type:profession-eligibility";
        public const string RequestTag = "subject-type:profession-entry-request";
        public const string RecognitionDecisionTag = "subject-type:profession-recognition-decision";
        public const string ReentryTag = "subject-type:profession-reentry";

        public static readonly string[] ProtectedFields =
        {
            "applicant-person-id",
            "authority-id",
            "sponsor-person-id",
            "eligibility-token",
            "hidden-requirements",
            "failure-details",
            "provenance"
        };

        public static InformationSubjectReferenceData EntryPath(string entryPathId, string professionId, IEnumerable<string> tags = null)
        {
            return Create(EntryPathTag, entryPathId, professionId, string.Empty, tags);
        }

        public static InformationSubjectReferenceData Eligibility(string correlationId, string personId, string professionId, IEnumerable<string> tags = null)
        {
            return Create(EligibilityTag, correlationId, professionId, personId, tags);
        }

        public static InformationSubjectReferenceData Request(string requestId, string personId, string professionId, IEnumerable<string> tags = null)
        {
            return Create(RequestTag, requestId, professionId, personId, tags);
        }

        private static InformationSubjectReferenceData Create(string subjectTag, string subjectId, string parentSubjectId, string ownerPersonId, IEnumerable<string> tags)
        {
            string[] subjectTags = (tags ?? Array.Empty<string>())
                .Concat(new[] { "domain.profession", subjectTag })
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray();

            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Custom,
                subjectId = subjectId ?? string.Empty,
                parentSubjectId = parentSubjectId ?? string.Empty,
                ownerPersonId = ownerPersonId ?? string.Empty,
                tags = subjectTags
            };
        }
    }
}
