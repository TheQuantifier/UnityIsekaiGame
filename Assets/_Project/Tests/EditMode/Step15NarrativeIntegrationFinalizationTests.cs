using System;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.Narrative;
using UnityIsekaiGame.Quests;

namespace UnityIsekaiGame.Tests
{
    public sealed class Step15NarrativeIntegrationFinalizationTests
    {
        [Test]
        public void Readiness_ReportsEveryOwnerWithoutSceneDependency()
        {
            Step15NarrativeIntegrationService service = new Step15NarrativeIntegrationService();
            Step15NarrativePersistenceSnapshot snapshot = CoherentCompletedSnapshot();

            Step15NarrativeReadinessSnapshot readiness = service.BuildReadiness(snapshot, new Step15NarrativeSceneBindingSummary
            {
                SceneId = "scene.prototype",
                LocationBindingCount = 2,
                InteractionPointBindingCount = 6,
                QuestSourceBindingCount = 2,
                PresentationOnlyBindingCount = 10
            });

            Assert.That(readiness.Ready, Is.True, string.Join(Environment.NewLine, readiness.Validation.Issues.Select(issue => issue.Message)));
            Assert.That(readiness.Runtimes.Count(item => item.Requirement == Step15NarrativeRuntimeRequirement.Required), Is.EqualTo(10));
            Assert.That(readiness.Runtimes.Where(item => item.Requirement == Step15NarrativeRuntimeRequirement.Required).All(item => item.State == Step15NarrativeReadinessState.Ready), Is.True);
            Assert.That(readiness.Runtimes.Single(item => item.ComponentId == "SceneBindingState").Requirement, Is.EqualTo(Step15NarrativeRuntimeRequirement.Optional));
            Assert.That(readiness.Manifest.Ownership.Where(item => !item.Derived).Select(item => item.Category), Is.Unique);
        }

        [Test]
        public void FinalizationValidation_CatchesCrossRuntimeCoherenceFailures()
        {
            Step15NarrativeIntegrationService service = new Step15NarrativeIntegrationService();
            Step15NarrativePersistenceSnapshot corrupt = Step15NarrativePersistenceHistoricalTests.SampleSnapshot();
            corrupt.Outcomes.rewardEntitlements.Add(corrupt.Outcomes.rewardEntitlements[0].Clone());
            corrupt.NarrativeEvents.events[0].cascadeDepth = Step15NarrativeIntegrationService.DefaultCascadeBudget + 1;
            corrupt.NarrativeEvents.events[0].actionExecutions[0].targetOwnerRuntime = "reflection:UnsafeInvoke";

            Step15NarrativeIntegrationValidationReport report = service.ValidateFinalization(corrupt, new Step15NarrativeSceneBindingSummary
            {
                SceneId = "scene.prototype",
                AuthoritativeMutationBindingCount = 1
            });

            Assert.That(report.Succeeded, Is.False);
            Assert.That(report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.QuestParticipation && issue.Message.Contains("terminal outcome", StringComparison.Ordinal)), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.Rewards && issue.Message.Contains("Duplicate reward", StringComparison.Ordinal)), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.Performance && issue.Message.Contains("cascade", StringComparison.Ordinal)), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.NarrativeEvents && issue.Message.Contains("arbitrary execution", StringComparison.Ordinal)), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Category == Step15NarrativeValidationCategory.SceneBinding), Is.True);
        }

        [Test]
        public void NarrativeContext_IsImmutableBoundedAndProjectionOnly()
        {
            Step15NarrativeIntegrationService service = new Step15NarrativeIntegrationService();
            Step15NarrativePersistenceSnapshot snapshot = ActiveContextSnapshot();

            Step15NarrativeContextSnapshot context = service.BuildNarrativeContext(snapshot, new Step15NarrativeContextQuery
            {
                RequesterPersonId = "person.prototype.hero",
                PersonId = "person.prototype.hero",
                LocationId = "location.prototype.guild",
                OrganizationId = "organization.prototype.adventurers-guild",
                WorldTime = 9d,
                Limit = 25
            });

            Assert.That(context.ActiveAssignments.Single().PrimaryId, Is.EqualTo("assignment.prototype.guild-posting"));
            Assert.That(context.ActiveObjectives.Single().PrimaryId, Is.EqualTo("objective.prototype.guild-report"));
            Assert.That(context.TurnInReadyQuests.Single().RelatedId, Is.EqualTo("quest.prototype.guild-posting"));
            Assert.That(context.AvailableQuestSources.Single().PrimaryId, Is.EqualTo("quest-source.prototype.guild-board"));
            Assert.That(context.ActiveConversations.Single().PrimaryId, Is.EqualTo("conversation.prototype.guild-counter"));
            Assert.That(context.CurrentDialogueNodes.Single().PrimaryId, Is.EqualTo("node.report"));
            Assert.That(context.RecentVisibleEvents.Any(entry => entry.State == NarrativeTimelineCategory.NarrativeEventTriggered.ToString()), Is.True);

            snapshot.Participation.assignments[0].assignmentId = "assignment.mutated";
            snapshot.Objectives.objectives[0].objectiveId = "objective.mutated";

            Assert.That(context.ActiveAssignments.Single().PrimaryId, Is.EqualTo("assignment.prototype.guild-posting"));
            Assert.That(context.ActiveObjectives.Single().PrimaryId, Is.EqualTo("objective.prototype.guild-report"));
            Assert.That(context.TotalEntries, Is.LessThanOrEqualTo(25 * 12));
        }

        [Test]
        public void Step16AuthoringContract_DeclaresAuthoringAndValidationBoundaries()
        {
            Step15NarrativeIntegrationService service = new Step15NarrativeIntegrationService();

            Step15NarrativeAuthoringContract contract = service.BuildStep16AuthoringContract();

            Assert.That(contract.Sections.Select(section => section.SectionId), Does.Contain("quests"));
            Assert.That(contract.Sections.Select(section => section.SectionId), Does.Contain("quest-sources"));
            Assert.That(contract.Sections.Select(section => section.SectionId), Does.Contain("conversation-dialogue"));
            Assert.That(contract.Sections.Select(section => section.SectionId), Does.Contain("narrative-events"));
            Assert.That(contract.Sections.Select(section => section.SectionId), Does.Contain("narrative-state"));
            Assert.That(contract.Sections.Select(section => section.SectionId), Does.Contain("narrative-arcs"));
            Assert.That(contract.Sections.All(section => section.AuthorableConcepts.Count > 0 && section.ValidationGuarantees.Count > 0), Is.True);
        }

        private static Step15NarrativePersistenceSnapshot CoherentCompletedSnapshot()
        {
            Step15NarrativePersistenceSnapshot snapshot = Step15NarrativePersistenceHistoricalTests.SampleSnapshot();
            snapshot.Participation.assignments[0].lifecycleState = QuestAssignmentLifecycleState.Historical;
            snapshot.Participation.assignments[0].endedWorldTime = 10d;
            snapshot.Objectives.objectives[0].lifecycleState = QuestObjectiveLifecycleState.Satisfied;
            snapshot.Objectives.objectives[0].satisfied = true;
            snapshot.Objectives.objectives[0].satisfiedWorldTime = 9d;
            snapshot.Sources.sources[0].hostLocationId = "location.prototype.guild";
            snapshot.Sources.sources[0].operatingOrganizationId = "organization.prototype.adventurers-guild";
            snapshot.Sources.sources[0].interactionPointId = "interaction-point.prototype.guild-board";
            snapshot.Sources.listings[0].claimedAssignmentId = "assignment.prototype.guild-posting";
            snapshot.Conversations.conversations[0].hostLocationId = "location.prototype.guild";
            snapshot.Conversations.conversations[0].operatingOrganizationId = "organization.prototype.adventurers-guild";
            return snapshot.Clone();
        }

        private static Step15NarrativePersistenceSnapshot ActiveContextSnapshot()
        {
            Step15NarrativePersistenceSnapshot snapshot = CoherentCompletedSnapshot();
            snapshot.Participation.assignments[0].lifecycleState = QuestAssignmentLifecycleState.Active;
            snapshot.Participation.assignments[0].endedWorldTime = -1d;
            snapshot.Outcomes.terminalOutcomes.Clear();
            snapshot.Outcomes.rewardEntitlements.Clear();
            snapshot.Outcomes.events.Clear();
            snapshot.Sources.listings[0].lifecycleState = QuestListingLifecycleState.Published;
            snapshot.Sources.listings[0].endedWorldTime = -1d;
            snapshot.Sources.listings[0].claimedAssignmentId = string.Empty;
            return snapshot.Clone();
        }
    }
}
