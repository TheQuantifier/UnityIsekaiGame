using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace UnityIsekaiGame.Knowledge.Access
{
    public sealed class InformationAccessProjection<TRecord>
    {
        public InformationAccessProjection(
            TRecord record,
            InformationAccessDecision decision,
            IReadOnlyDictionary<string, InformationRedactionState> detailStates,
            string visibleSubjectId,
            string message)
        {
            Record = record;
            Decision = decision;
            DetailStates = new ReadOnlyDictionary<string, InformationRedactionState>(
                new Dictionary<string, InformationRedactionState>(detailStates ?? new Dictionary<string, InformationRedactionState>(), StringComparer.Ordinal));
            VisibleSubjectId = visibleSubjectId ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public TRecord Record { get; }
        public InformationAccessDecision Decision { get; }
        public IReadOnlyDictionary<string, InformationRedactionState> DetailStates { get; }
        public string VisibleSubjectId { get; }
        public string Message { get; }
        public bool Succeeded => Decision != null && !Decision.Denied && Record != null;
        public bool Denied => Decision == null || Decision.Denied || Record == null;
        public bool FullAccess => Decision != null && Decision.FullAccess;
        public bool Redacted => Decision != null && (Decision.RedactedAccess || Decision.PartialAccess || DetailStates.Any(pair => pair.Value != InformationRedactionState.Visible));
    }

    public static class InformationAccessProjectionUtility
    {
        public static InformationAccessContext BuildContext(
            InformationAccessContext source,
            InformationSubjectReferenceData subject,
            InformationAccessMode mode,
            InformationAccessPurpose purpose,
            IEnumerable<string> detailIds,
            string policyId = "")
        {
            return new InformationAccessContext
            {
                RequestingPersonId = source?.RequestingPersonId ?? string.Empty,
                ActingEntityId = source?.ActingEntityId ?? string.Empty,
                Subject = subject?.Clone() ?? new InformationSubjectReferenceData(),
                Purpose = source == null ? purpose : source.Purpose,
                WorldTimeSeconds = source == null ? 0d : source.WorldTimeSeconds,
                AccessMode = mode,
                RequestedDetailIds = (detailIds ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                AuthorizationIds = source?.AuthorizationIds ?? Array.Empty<string>(),
                OrganizationIds = source?.OrganizationIds ?? Array.Empty<string>(),
                RoleIds = source?.RoleIds ?? Array.Empty<string>(),
                TitleOrStatusIds = source?.TitleOrStatusIds ?? Array.Empty<string>(),
                NeedToKnowTags = source?.NeedToKnowTags ?? Array.Empty<string>(),
                IsParticipant = source?.IsParticipant ?? false,
                IsWitness = source?.IsWitness ?? false,
                IsRecipient = source?.IsRecipient ?? false,
                HasDiscoveredSubject = source?.HasDiscoveredSubject ?? false,
                KnowsSource = source?.KnowsSource ?? false,
                ContextKind = source?.ContextKind ?? InformationContextKind.Gameplay,
                RedactedAccessAcceptable = source?.RedactedAccessAcceptable ?? true,
                RevealDenialReasons = source?.RevealDenialReasons ?? false,
                DeterministicPolicyId = string.IsNullOrWhiteSpace(policyId) ? source?.DeterministicPolicyId ?? string.Empty : policyId
            };
        }

        public static bool IsVisible(IReadOnlyDictionary<string, InformationRedactionState> states, string detailId)
        {
            return states != null && states.TryGetValue(detailId ?? string.Empty, out InformationRedactionState state) && state == InformationRedactionState.Visible;
        }
    }
}
