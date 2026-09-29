using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityIsekaiGame.Narrative
{
    public enum Step15NarrativeRuntimeRequirement
    {
        Required,
        Optional,
        Derived
    }

    public enum Step15NarrativeValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum Step15NarrativeValidationCategory
    {
        Ownership,
        Dependency,
        QuestParticipation,
        Objectives,
        Outcomes,
        Rewards,
        Sources,
        Dialogue,
        NarrativeEvents,
        NarrativeState,
        NarrativeArcs,
        Visibility,
        Persistence,
        SceneBinding,
        Performance,
        Authoring
    }

    public sealed class Step15NarrativeRuntimeReadiness
    {
        public Step15NarrativeRuntimeReadiness(
            string componentId,
            string ownerRuntime,
            string participantKey,
            Step15NarrativeRuntimeRequirement requirement,
            Step15NarrativeReadinessState state,
            int schemaVersion,
            int recordCount,
            string diagnostics)
        {
            ComponentId = N(componentId);
            OwnerRuntime = N(ownerRuntime);
            ParticipantKey = N(participantKey);
            Requirement = requirement;
            State = state;
            SchemaVersion = schemaVersion;
            RecordCount = Math.Max(0, recordCount);
            Diagnostics = diagnostics ?? string.Empty;
        }

        public string ComponentId { get; }
        public string OwnerRuntime { get; }
        public string ParticipantKey { get; }
        public Step15NarrativeRuntimeRequirement Requirement { get; }
        public Step15NarrativeReadinessState State { get; }
        public int SchemaVersion { get; }
        public int RecordCount { get; }
        public string Diagnostics { get; }
        public bool RequiredAndReady => Requirement != Step15NarrativeRuntimeRequirement.Required || State == Step15NarrativeReadinessState.Ready;

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step15NarrativeValidationIssue
    {
        public Step15NarrativeValidationIssue(Step15NarrativeValidationSeverity severity, Step15NarrativeValidationCategory category, string sourceRuntime, string sourceId, string message)
        {
            Severity = severity;
            Category = category;
            SourceRuntime = N(sourceRuntime);
            SourceId = N(sourceId);
            Message = message ?? string.Empty;
        }

        public Step15NarrativeValidationSeverity Severity { get; }
        public Step15NarrativeValidationCategory Category { get; }
        public string SourceRuntime { get; }
        public string SourceId { get; }
        public string Message { get; }
        public bool IsError => Severity == Step15NarrativeValidationSeverity.Error;

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step15NarrativeIntegrationValidationReport
    {
        public Step15NarrativeIntegrationValidationReport(IEnumerable<Step15NarrativeValidationIssue> issues)
        {
            Issues = (issues ?? Array.Empty<Step15NarrativeValidationIssue>()).Where(value => value != null).ToArray();
        }

        public IReadOnlyList<Step15NarrativeValidationIssue> Issues { get; }
        public int ErrorCount => Issues.Count(value => value.Severity == Step15NarrativeValidationSeverity.Error);
        public int WarningCount => Issues.Count(value => value.Severity == Step15NarrativeValidationSeverity.Warning);
        public int InfoCount => Issues.Count(value => value.Severity == Step15NarrativeValidationSeverity.Info);
        public bool Succeeded => ErrorCount == 0;
        public string Summary => $"Step 15 integration validation finished with {ErrorCount} error(s), {WarningCount} warning(s), and {InfoCount} info message(s).";
    }

    public sealed class Step15NarrativeSceneBindingSummary
    {
        public string SceneId { get; set; } = string.Empty;
        public int LocationBindingCount { get; set; }
        public int InteractionPointBindingCount { get; set; }
        public int QuestSourceBindingCount { get; set; }
        public int PresentationOnlyBindingCount { get; set; }
        public int AuthoritativeMutationBindingCount { get; set; }
        public bool LoadedSceneRequired { get; set; }
    }

    public sealed class Step15NarrativeReadinessSnapshot
    {
        public Step15NarrativeReadinessSnapshot(
            Step15NarrativeReadinessState state,
            IEnumerable<Step15NarrativeRuntimeReadiness> runtimes,
            Step15NarrativeIntegrationValidationReport validation,
            Step15NarrativePersistenceManifest manifest,
            Step15NarrativeSceneBindingSummary sceneBindings)
        {
            State = state;
            Runtimes = (runtimes ?? Array.Empty<Step15NarrativeRuntimeReadiness>()).Where(value => value != null).ToArray();
            Validation = validation ?? new Step15NarrativeIntegrationValidationReport(Array.Empty<Step15NarrativeValidationIssue>());
            Manifest = manifest;
            SceneBindings = sceneBindings;
        }

        public Step15NarrativeReadinessState State { get; }
        public IReadOnlyList<Step15NarrativeRuntimeReadiness> Runtimes { get; }
        public Step15NarrativeIntegrationValidationReport Validation { get; }
        public Step15NarrativePersistenceManifest Manifest { get; }
        public Step15NarrativeSceneBindingSummary SceneBindings { get; }
        public bool Ready => State == Step15NarrativeReadinessState.Ready;
    }

    public sealed class Step15NarrativeContextQuery
    {
        public string RequesterPersonId { get; set; } = string.Empty;
        public string PersonId { get; set; } = string.Empty;
        public string LocationId { get; set; } = string.Empty;
        public string OrganizationId { get; set; } = string.Empty;
        public NarrativeHistoricalAccessMode AccessMode { get; set; } = NarrativeHistoricalAccessMode.PersonSafe;
        public double WorldTime { get; set; } = double.MaxValue;
        public int Limit { get; set; } = 100;
    }

    public sealed class Step15NarrativeContextEntry
    {
        public Step15NarrativeContextEntry(string category, string primaryId, string relatedId, string ownerRuntime, string state, bool redacted, string summary)
        {
            Category = N(category);
            PrimaryId = redacted ? string.Empty : N(primaryId);
            RelatedId = redacted ? string.Empty : N(relatedId);
            OwnerRuntime = N(ownerRuntime);
            State = N(state);
            Redacted = redacted;
            Summary = redacted ? "Redacted" : summary ?? string.Empty;
        }

        public string Category { get; }
        public string PrimaryId { get; }
        public string RelatedId { get; }
        public string OwnerRuntime { get; }
        public string State { get; }
        public bool Redacted { get; }
        public string Summary { get; }

        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step15NarrativeContextSnapshot
    {
        public Step15NarrativeContextSnapshot(
            string personId,
            string locationId,
            string organizationId,
            IEnumerable<Step15NarrativeContextEntry> visibleQuestOffers,
            IEnumerable<Step15NarrativeContextEntry> activeAssignments,
            IEnumerable<Step15NarrativeContextEntry> activeObjectives,
            IEnumerable<Step15NarrativeContextEntry> turnInReadyQuests,
            IEnumerable<Step15NarrativeContextEntry> claimableRewards,
            IEnumerable<Step15NarrativeContextEntry> availableQuestSources,
            IEnumerable<Step15NarrativeContextEntry> activeConversations,
            IEnumerable<Step15NarrativeContextEntry> currentDialogueNodes,
            IEnumerable<Step15NarrativeContextEntry> visibleNarrativeState,
            IEnumerable<Step15NarrativeContextEntry> activeArcStages,
            IEnumerable<Step15NarrativeContextEntry> recentVisibleEvents,
            IEnumerable<Step15NarrativeContextEntry> locationInstitutionContext)
        {
            PersonId = N(personId);
            LocationId = N(locationId);
            OrganizationId = N(organizationId);
            VisibleQuestOffers = Clean(visibleQuestOffers);
            ActiveAssignments = Clean(activeAssignments);
            ActiveObjectives = Clean(activeObjectives);
            TurnInReadyQuests = Clean(turnInReadyQuests);
            ClaimableRewards = Clean(claimableRewards);
            AvailableQuestSources = Clean(availableQuestSources);
            ActiveConversations = Clean(activeConversations);
            CurrentDialogueNodes = Clean(currentDialogueNodes);
            VisibleNarrativeState = Clean(visibleNarrativeState);
            ActiveArcStages = Clean(activeArcStages);
            RecentVisibleEvents = Clean(recentVisibleEvents);
            LocationInstitutionContext = Clean(locationInstitutionContext);
        }

        public string PersonId { get; }
        public string LocationId { get; }
        public string OrganizationId { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> VisibleQuestOffers { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> ActiveAssignments { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> ActiveObjectives { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> TurnInReadyQuests { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> ClaimableRewards { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> AvailableQuestSources { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> ActiveConversations { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> CurrentDialogueNodes { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> VisibleNarrativeState { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> ActiveArcStages { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> RecentVisibleEvents { get; }
        public IReadOnlyList<Step15NarrativeContextEntry> LocationInstitutionContext { get; }
        public int TotalEntries => VisibleQuestOffers.Count + ActiveAssignments.Count + ActiveObjectives.Count + TurnInReadyQuests.Count + ClaimableRewards.Count + AvailableQuestSources.Count + ActiveConversations.Count + CurrentDialogueNodes.Count + VisibleNarrativeState.Count + ActiveArcStages.Count + RecentVisibleEvents.Count + LocationInstitutionContext.Count;

        private static IReadOnlyList<Step15NarrativeContextEntry> Clean(IEnumerable<Step15NarrativeContextEntry> values) => (values ?? Array.Empty<Step15NarrativeContextEntry>()).Where(value => value != null).ToArray();
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step15NarrativeAuthoringContractSection
    {
        public Step15NarrativeAuthoringContractSection(string sectionId, string ownerRuntime, IEnumerable<string> authorableConcepts, IEnumerable<string> validationGuarantees)
        {
            SectionId = N(sectionId);
            OwnerRuntime = N(ownerRuntime);
            AuthorableConcepts = Clean(authorableConcepts);
            ValidationGuarantees = Clean(validationGuarantees);
        }

        public string SectionId { get; }
        public string OwnerRuntime { get; }
        public IReadOnlyList<string> AuthorableConcepts { get; }
        public IReadOnlyList<string> ValidationGuarantees { get; }

        private static string[] Clean(IEnumerable<string> values) => (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).ToArray();
        private static string N(string value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public sealed class Step15NarrativeAuthoringContract
    {
        public Step15NarrativeAuthoringContract(IEnumerable<Step15NarrativeAuthoringContractSection> sections)
        {
            Sections = (sections ?? Array.Empty<Step15NarrativeAuthoringContractSection>()).Where(value => value != null).OrderBy(value => value.SectionId, StringComparer.Ordinal).ToArray();
        }

        public IReadOnlyList<Step15NarrativeAuthoringContractSection> Sections { get; }
    }
}
