using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Professions
{
    [Serializable]
    public sealed class PersonProfessionRelationshipData
    {
        public string relationshipId;
        public string personId;
        public string professionId;
        public ProfessionRelationshipState state = ProfessionRelationshipState.Interested;
        public bool formalPractice;
        public bool informalPractice = true;
        public bool selfDeclared;
        public bool recognized;
        public bool primary;
        public bool active = true;
        public string startWorldTime;
        public string endWorldTime;
        public string[] specializationIds = Array.Empty<string>();
        public string recognizingAuthorityId;
        public string recognitionReferenceId;
        public string accessPolicyId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;
        public bool disputed;

        public PersonProfessionRelationshipData Clone()
        {
            return new PersonProfessionRelationshipData
            {
                relationshipId = relationshipId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                state = state,
                formalPractice = formalPractice,
                informalPractice = informalPractice,
                selfDeclared = selfDeclared,
                recognized = recognized,
                primary = primary,
                active = active,
                startWorldTime = startWorldTime ?? string.Empty,
                endWorldTime = endWorldTime ?? string.Empty,
                specializationIds = Clean(specializationIds),
                recognizingAuthorityId = recognizingAuthorityId ?? string.Empty,
                recognitionReferenceId = recognitionReferenceId ?? string.Empty,
                accessPolicyId = accessPolicyId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                tags = Clean(tags),
                revision = revision,
                disputed = disputed
            };
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
    }

    [Serializable]
    public sealed class PersonProfessionRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<PersonProfessionRelationshipData> relationships = new List<PersonProfessionRelationshipData>();

        public PersonProfessionRuntimeSaveData Clone()
        {
            return new PersonProfessionRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                relationships = relationships == null
                    ? new List<PersonProfessionRelationshipData>()
                    : relationships.Select(relationship => relationship?.Clone()).Where(relationship => relationship != null).ToList()
            };
        }
    }

    public sealed class PersonProfessionSnapshot
    {
        public PersonProfessionSnapshot(PersonProfessionRelationshipData data)
        {
            Data = data?.Clone() ?? new PersonProfessionRelationshipData();
        }

        public PersonProfessionRelationshipData Data { get; }
        public string RelationshipId => Data.relationshipId ?? string.Empty;
        public string PersonId => Data.personId ?? string.Empty;
        public string ProfessionId => Data.professionId ?? string.Empty;
        public ProfessionRelationshipState State => Data.state;
        public bool FormalPractice => Data.formalPractice;
        public bool InformalPractice => Data.informalPractice;
        public bool SelfDeclared => Data.selfDeclared;
        public bool Recognized => Data.recognized;
        public bool Primary => Data.primary;
        public bool Active => Data.active;
        public bool Secret => Data.state == ProfessionRelationshipState.Secret || Tags.Contains("profession.secret");
        public bool Disputed => Data.disputed || Data.state == ProfessionRelationshipState.Disputed;
        public IReadOnlyList<string> SpecializationIds => Data.specializationIds ?? Array.Empty<string>();
        public IReadOnlyList<string> Tags => Data.tags ?? Array.Empty<string>();
        public string RecognizingAuthorityId => Data.recognizingAuthorityId ?? string.Empty;
        public string AccessPolicyId => Data.accessPolicyId ?? string.Empty;
        public long Revision => Data.revision;

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return ProfessionInformationSubject.Relationship(RelationshipId, PersonId, ProfessionId, Tags);
        }
    }

    public sealed class PersonProfessionProjection
    {
        public PersonProfessionProjection(PersonProfessionSnapshot snapshot, ProfessionProjectionAudience audience, InformationAccessDecision decision, bool redacted, bool denied, IReadOnlyList<string> visibleFields, IReadOnlyList<string> redactedFields)
        {
            Snapshot = snapshot;
            Audience = audience;
            Decision = decision;
            Redacted = redacted;
            Denied = denied;
            VisibleFields = (visibleFields ?? Array.Empty<string>()).ToArray();
            RedactedFields = (redactedFields ?? Array.Empty<string>()).ToArray();
        }

        public PersonProfessionSnapshot Snapshot { get; }
        public ProfessionProjectionAudience Audience { get; }
        public InformationAccessDecision Decision { get; }
        public bool Redacted { get; }
        public bool Denied { get; }
        public IReadOnlyList<string> VisibleFields { get; }
        public IReadOnlyList<string> RedactedFields { get; }
    }

    public sealed class ProfessionHistoryHookData
    {
        public ProfessionHistoryHookKind kind;
        public string relationshipId;
        public string personId;
        public string professionId;
        public string specializationId;
        public string authorityId;
        public string worldTime;
        public string transactionId;

        public ProfessionHistoryHookData Clone()
        {
            return new ProfessionHistoryHookData
            {
                kind = kind,
                relationshipId = relationshipId ?? string.Empty,
                personId = personId ?? string.Empty,
                professionId = professionId ?? string.Empty,
                specializationId = specializationId ?? string.Empty,
                authorityId = authorityId ?? string.Empty,
                worldTime = worldTime ?? string.Empty,
                transactionId = transactionId ?? string.Empty
            };
        }
    }

    public sealed class ProfessionOperationResult
    {
        private ProfessionOperationResult(bool succeeded, bool preview, bool duplicate, ProfessionOperationStatus status, string message, long priorRevision, long resultingRevision, PersonProfessionSnapshot snapshot)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Status = status;
            Message = message ?? string.Empty;
            PriorRevision = priorRevision;
            ResultingRevision = resultingRevision;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public ProfessionOperationStatus Status { get; }
        public string Message { get; }
        public long PriorRevision { get; }
        public long ResultingRevision { get; }
        public PersonProfessionSnapshot Snapshot { get; }

        public static ProfessionOperationResult Success(PersonProfessionSnapshot snapshot, string message, long priorRevision, long resultingRevision, bool preview = false, bool duplicate = false)
        {
            return new ProfessionOperationResult(true, preview, duplicate, preview ? ProfessionOperationStatus.Preview : duplicate ? ProfessionOperationStatus.Duplicate : ProfessionOperationStatus.Succeeded, message, priorRevision, resultingRevision, snapshot);
        }

        public static ProfessionOperationResult Failure(ProfessionOperationStatus status, string message, long revision = 0L)
        {
            return new ProfessionOperationResult(false, false, false, status, message, revision, revision, null);
        }
    }

    public sealed class AddProfessionRelationshipRequest
    {
        public string transactionId;
        public string relationshipId;
        public string personId;
        public string professionId;
        public ProfessionRelationshipState state = ProfessionRelationshipState.Practicing;
        public bool formalPractice;
        public bool informalPractice = true;
        public bool selfDeclared = true;
        public bool recognized;
        public bool primary;
        public bool active = true;
        public string startWorldTime;
        public string endWorldTime;
        public string[] specializationIds = Array.Empty<string>();
        public string recognizingAuthorityId;
        public string recognitionReferenceId;
        public string accessPolicyId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public bool preview;
    }

    public static class ProfessionInformationSubject
    {
        public const string ProfessionDefinitionTag = "subject-type:profession-definition";
        public const string ProfessionSpecializationTag = "subject-type:profession-specialization";
        public const string ProfessionRelationshipTag = "subject-type:profession-relationship";
        public const string ProfessionFormalRecognitionTag = "profession.formal-recognition";
        public const string ProfessionSelfDeclaredTag = "profession.self-declared";
        public const string ProfessionPrimaryTag = "profession.primary";
        public const string ProfessionActiveTag = "profession.active";

        public static readonly string[] ProtectedFields =
        {
            "relationship-id",
            "person-id",
            "recognition-authority",
            "recognition-reference",
            "secret-practice",
            "specializations",
            "access-policy",
            "provenance"
        };

        public static InformationSubjectReferenceData Definition(string professionId, IEnumerable<string> tags = null)
        {
            return Create(ProfessionDefinitionTag, professionId, string.Empty, string.Empty, tags);
        }

        public static InformationSubjectReferenceData Specialization(string specializationId, string professionId, IEnumerable<string> tags = null)
        {
            return Create(ProfessionSpecializationTag, specializationId, professionId, string.Empty, tags);
        }

        public static InformationSubjectReferenceData Relationship(string relationshipId, string personId, string professionId, IEnumerable<string> tags = null)
        {
            return Create(ProfessionRelationshipTag, relationshipId, professionId, personId, tags);
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
