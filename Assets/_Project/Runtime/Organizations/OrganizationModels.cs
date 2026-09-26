using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Organizations
{
    [Serializable]
    public sealed class OrganizationFounderReferenceData
    {
        public OrganizationFounderKind kind = OrganizationFounderKind.Person;
        public string subjectId;
        public string provenanceId;

        public OrganizationFounderReferenceData Clone()
        {
            return new OrganizationFounderReferenceData
            {
                kind = kind,
                subjectId = subjectId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class OrganizationExternalReferenceData
    {
        public OrganizationReferenceKind kind = OrganizationReferenceKind.Other;
        public string referenceId;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;

        public OrganizationExternalReferenceData Clone()
        {
            return new OrganizationExternalReferenceData
            {
                kind = kind,
                referenceId = referenceId ?? string.Empty,
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty
            };
        }
    }

    [Serializable]
    public sealed class OrganizationNameRecordData
    {
        public string nameRecordId;
        public string organizationId;
        public string value;
        public OrganizationNameCategory category = OrganizationNameCategory.Official;
        public double effectiveStartWorldTime;
        public double effectiveEndWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string languageId;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public long revision = 1L;

        public bool IsActive => effectiveEndWorldTime < 0d;

        public OrganizationNameRecordData Clone()
        {
            return new OrganizationNameRecordData
            {
                nameRecordId = nameRecordId ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                value = value ?? string.Empty,
                category = category,
                effectiveStartWorldTime = effectiveStartWorldTime,
                effectiveEndWorldTime = effectiveEndWorldTime,
                visibility = visibility,
                languageId = languageId ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class OrganizationLinkRecordData
    {
        public string linkRecordId;
        public string sourceOrganizationId;
        public string targetOrganizationId;
        public OrganizationLinkKind kind = OrganizationLinkKind.Parent;
        public double startWorldTime;
        public double endWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public bool IsActive => endWorldTime < 0d;

        public OrganizationLinkRecordData Clone()
        {
            return new OrganizationLinkRecordData
            {
                linkRecordId = linkRecordId ?? string.Empty,
                sourceOrganizationId = sourceOrganizationId ?? string.Empty,
                targetOrganizationId = targetOrganizationId ?? string.Empty,
                kind = kind,
                startWorldTime = startWorldTime,
                endWorldTime = endWorldTime,
                visibility = visibility,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                tags = OrganizationModelUtility.Clean(tags),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class OrganizationRecordData
    {
        public string organizationId;
        public string organizationDefinitionId;
        public string currentOfficialNameRecordId;
        public string currentName;
        public string shortName;
        public string abbreviation;
        public OrganizationLifecycleState lifecycleState = OrganizationLifecycleState.Active;
        public double foundingWorldTime;
        public double activationWorldTime = -1d;
        public double dormancyWorldTime = -1d;
        public double dissolutionWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public OrganizationFounderReferenceData[] founders = Array.Empty<OrganizationFounderReferenceData>();
        public string[] parentOrganizationIds = Array.Empty<string>();
        public string[] predecessorOrganizationIds = Array.Empty<string>();
        public string[] successorOrganizationIds = Array.Empty<string>();
        public string headquartersPlaceId;
        public string[] operatingAreaPlaceIds = Array.Empty<string>();
        public OrganizationExternalReferenceData[] externalReferences = Array.Empty<OrganizationExternalReferenceData>();
        public string publicDescription;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public long revision = 1L;

        public OrganizationRecordData Clone()
        {
            return new OrganizationRecordData
            {
                organizationId = organizationId ?? string.Empty,
                organizationDefinitionId = organizationDefinitionId ?? string.Empty,
                currentOfficialNameRecordId = currentOfficialNameRecordId ?? string.Empty,
                currentName = currentName ?? string.Empty,
                shortName = shortName ?? string.Empty,
                abbreviation = abbreviation ?? string.Empty,
                lifecycleState = lifecycleState,
                foundingWorldTime = foundingWorldTime,
                activationWorldTime = activationWorldTime,
                dormancyWorldTime = dormancyWorldTime,
                dissolutionWorldTime = dissolutionWorldTime,
                visibility = visibility,
                founders = founders == null ? Array.Empty<OrganizationFounderReferenceData>() : founders.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                parentOrganizationIds = OrganizationModelUtility.Clean(parentOrganizationIds),
                predecessorOrganizationIds = OrganizationModelUtility.Clean(predecessorOrganizationIds),
                successorOrganizationIds = OrganizationModelUtility.Clean(successorOrganizationIds),
                headquartersPlaceId = headquartersPlaceId ?? string.Empty,
                operatingAreaPlaceIds = OrganizationModelUtility.Clean(operatingAreaPlaceIds),
                externalReferences = externalReferences == null ? Array.Empty<OrganizationExternalReferenceData>() : externalReferences.Select(item => item?.Clone()).Where(item => item != null).ToArray(),
                publicDescription = publicDescription ?? string.Empty,
                sourceEventId = sourceEventId ?? string.Empty,
                sourceRecordId = sourceRecordId ?? string.Empty,
                provenanceId = provenanceId ?? string.Empty,
                tags = OrganizationModelUtility.Clean(tags),
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class OrganizationTransactionRecordData
    {
        public string transactionId;
        public string operation;
        public string organizationId;
        public long revision;

        public OrganizationTransactionRecordData Clone()
        {
            return new OrganizationTransactionRecordData
            {
                transactionId = transactionId ?? string.Empty,
                operation = operation ?? string.Empty,
                organizationId = organizationId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class OrganizationRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string worldId;
        public long revision;
        public List<OrganizationRecordData> records = new List<OrganizationRecordData>();
        public List<OrganizationNameRecordData> names = new List<OrganizationNameRecordData>();
        public List<OrganizationLinkRecordData> links = new List<OrganizationLinkRecordData>();
        public List<OrganizationTransactionRecordData> transactions = new List<OrganizationTransactionRecordData>();

        public OrganizationRuntimeSaveData Clone()
        {
            return new OrganizationRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                worldId = worldId ?? string.Empty,
                revision = revision,
                records = records == null ? new List<OrganizationRecordData>() : records.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                names = names == null ? new List<OrganizationNameRecordData>() : names.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                links = links == null ? new List<OrganizationLinkRecordData>() : links.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                transactions = transactions == null ? new List<OrganizationTransactionRecordData>() : transactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class OrganizationCreateRequest
    {
        public string organizationId;
        public string organizationDefinitionId;
        public string officialName;
        public string shortName;
        public string abbreviation;
        public OrganizationLifecycleState initialLifecycleState = OrganizationLifecycleState.Unknown;
        public double foundingWorldTime;
        public double activationWorldTime = -1d;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public OrganizationFounderReferenceData[] founders = Array.Empty<OrganizationFounderReferenceData>();
        public string headquartersPlaceId;
        public string[] operatingAreaPlaceIds = Array.Empty<string>();
        public string[] predecessorOrganizationIds = Array.Empty<string>();
        public string[] aliases = Array.Empty<string>();
        public string publicDescription;
        public string sourceEventId;
        public string sourceRecordId;
        public string provenanceId;
        public string[] tags = Array.Empty<string>();
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationRenameRequest
    {
        public string organizationId;
        public string newOfficialName;
        public double effectiveWorldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationLifecycleTransitionRequest
    {
        public string organizationId;
        public OrganizationLifecycleState targetState;
        public double worldTime;
        public string sourceEventId;
        public string sourceRecordId;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationPlaceRequest
    {
        public string organizationId;
        public OrganizationReferenceKind kind = OrganizationReferenceKind.Headquarters;
        public string placeId;
        public string sourceEventId;
        public string sourceRecordId;
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationLinkRequest
    {
        public string linkRecordId;
        public string sourceOrganizationId;
        public string targetOrganizationId;
        public OrganizationLinkKind kind = OrganizationLinkKind.Parent;
        public double startWorldTime;
        public OrganizationVisibility visibility = OrganizationVisibility.Public;
        public string sourceEventId;
        public string sourceRecordId;
        public string[] tags = Array.Empty<string>();
        public string transactionId;
        public bool preview;
    }

    public sealed class OrganizationSnapshot
    {
        public OrganizationSnapshot(OrganizationRecordData record, IEnumerable<OrganizationNameRecordData> names, IEnumerable<OrganizationLinkRecordData> links)
        {
            Data = record?.Clone() ?? new OrganizationRecordData();
            Names = OrganizationModelUtility.Ordered(names ?? Array.Empty<OrganizationNameRecordData>(), item => item.nameRecordId).Select(item => item.Clone()).ToArray();
            Links = OrganizationModelUtility.Ordered(links ?? Array.Empty<OrganizationLinkRecordData>(), item => item.linkRecordId).Select(item => item.Clone()).ToArray();
        }

        public OrganizationRecordData Data { get; }
        public IReadOnlyList<OrganizationNameRecordData> Names { get; }
        public IReadOnlyList<OrganizationLinkRecordData> Links { get; }
        public string OrganizationId => Data.organizationId ?? string.Empty;
        public string DefinitionId => Data.organizationDefinitionId ?? string.Empty;
        public string CurrentName => Data.currentName ?? string.Empty;
        public OrganizationLifecycleState LifecycleState => Data.lifecycleState;
        public OrganizationVisibility Visibility => Data.visibility;
        public string HeadquartersPlaceId => Data.headquartersPlaceId ?? string.Empty;
        public IReadOnlyList<string> OperatingAreaPlaceIds => Data.operatingAreaPlaceIds ?? Array.Empty<string>();
        public long Revision => Data.revision;
    }

    public sealed class OrganizationProjection
    {
        public OrganizationProjection(
            OrganizationProjectionAccess access,
            InformationSubjectReferenceData subject,
            OrganizationSnapshot snapshot,
            string displayName,
            string message)
        {
            Access = access;
            Subject = subject;
            Snapshot = snapshot;
            DisplayName = displayName ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public OrganizationProjectionAccess Access { get; }
        public InformationSubjectReferenceData Subject { get; }
        public OrganizationSnapshot Snapshot { get; }
        public string DisplayName { get; }
        public string Message { get; }
        public bool Succeeded => Access == OrganizationProjectionAccess.Full || Access == OrganizationProjectionAccess.Redacted;
        public bool Redacted => Access == OrganizationProjectionAccess.Redacted;
    }

    public sealed class OrganizationOperationResult
    {
        private OrganizationOperationResult(bool succeeded, OrganizationOperationStatus status, OrganizationSnapshot snapshot, string message, long beforeRevision, long afterRevision, bool preview, bool duplicate)
        {
            Succeeded = succeeded;
            Status = status;
            Snapshot = snapshot;
            Message = message ?? string.Empty;
            BeforeRevision = beforeRevision;
            AfterRevision = afterRevision;
            Preview = preview;
            Duplicate = duplicate;
        }

        public bool Succeeded { get; }
        public OrganizationOperationStatus Status { get; }
        public OrganizationSnapshot Snapshot { get; }
        public string Message { get; }
        public long BeforeRevision { get; }
        public long AfterRevision { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }

        public static OrganizationOperationResult Success(OrganizationSnapshot snapshot, string message, long beforeRevision, long afterRevision, bool preview = false, bool duplicate = false)
        {
            return new OrganizationOperationResult(true, preview ? OrganizationOperationStatus.Preview : duplicate ? OrganizationOperationStatus.Duplicate : OrganizationOperationStatus.Succeeded, snapshot, message, beforeRevision, afterRevision, preview, duplicate);
        }

        public static OrganizationOperationResult Failure(OrganizationOperationStatus status, string message, long beforeRevision)
        {
            return new OrganizationOperationResult(false, status, null, message, beforeRevision, beforeRevision, preview: false, duplicate: false);
        }
    }

    internal static class OrganizationModelUtility
    {
        public static string[] Clean(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        public static IReadOnlyList<T> Ordered<T>(IEnumerable<T> values, Func<T, string> keySelector)
        {
            return (values ?? Array.Empty<T>())
                .Where(value => value != null)
                .OrderBy(value => keySelector(value) ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
