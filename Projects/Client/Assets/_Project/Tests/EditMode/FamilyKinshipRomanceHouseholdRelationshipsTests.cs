#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Social.Attitudes;
using UnityIsekaiGame.Social.Family;
using UnityIsekaiGame.Social.Interactions;
using UnityIsekaiGame.Social.Relationships;
using UnityIsekaiGame.Social.Reputation;
using UnityIsekaiGame.Social.Rumors;

namespace UnityIsekaiGame.Tests
{
    public sealed class FamilyKinshipRomanceHouseholdRelationshipsTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";
        private static readonly string[] KnownPersons =
        {
            PersistenceService.LocalPlayerId,
            "person.prototype.friend",
            "person.prototype.rival",
            "person.prototype.parent",
            "person.prototype.child",
            "person.prototype.dependent",
            "person.prototype.partner",
            "person.prototype.spouse",
            "person.prototype.sibling",
            "person.prototype.cousin",
            "person.prototype.mentor",
            "person.prototype.student"
        };

        private static readonly string[] AdultPersons = KnownPersons
            .Where(id => !id.Contains(".child", StringComparison.Ordinal) && !id.Contains(".dependent", StringComparison.Ordinal))
            .ToArray();

        [Test]
        public void PrototypeFamilyDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (IDefinitionCatalogValidationParticipant definition in PrototypeFamilyRelationshipDefinitionFactory.CreateDefinitions().OfType<IDefinitionCatalogValidationParticipant>())
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());
            Assert.That(registry.TryGet(PrototypeRelationshipDefinitionFactory.BiologicalParentChildRelationshipId, out RelationshipDefinition biological), Is.True);
            Assert.That(biological.Directionality, Is.EqualTo(RelationshipDirectionality.Directed));
            Assert.That(registry.TryGet(PrototypeAttitudeDefinitionFactory.RomanticAttractionId, out AttitudeDimensionDefinition attraction), Is.True);
            Assert.That(attraction.MinimumValue, Is.EqualTo(0));
            Assert.That(registry.TryGet(PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId, out RomanticEligibilityPolicyDefinition policy), Is.True);
            Assert.That(policy.RequireConsent, Is.True);
            Assert.That(registry.TryGet(PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId, out HouseholdDefinition household), Is.True);
            Assert.That(household.AllowsRole(HouseholdRole.Head), Is.True);
        }

        [Test]
        public void ParentageRecordsRejectSelfCyclesAndPreserveAdoptionAlongsideBiology()
        {
            using TestFixture fixture = CreateFixture();
            FamilyRelationshipMutationResult biological = fixture.Family.RecordParentage(Parentage("bio", "person.prototype.parent", "person.prototype.child", ParentageKind.Biological));
            FamilyRelationshipMutationResult adoptive = fixture.Family.RecordParentage(Parentage("adopt", "person.prototype.mentor", "person.prototype.child", ParentageKind.Adoptive));
            FamilyRelationshipMutationResult self = fixture.Family.RecordParentage(Parentage("self", "person.prototype.parent", "person.prototype.parent", ParentageKind.Biological));
            FamilyRelationshipMutationResult cycle = fixture.Family.RecordParentage(Parentage("cycle", "person.prototype.child", "person.prototype.parent", ParentageKind.Biological));

            Assert.That(biological.Succeeded, Is.True, biological.Message);
            Assert.That(adoptive.Succeeded, Is.True, adoptive.Message);
            Assert.That(self.Succeeded, Is.False);
            Assert.That(self.Status, Is.EqualTo(RomanticEligibilityStatus.InvalidRequest));
            Assert.That(cycle.Succeeded, Is.False);
            Assert.That(cycle.Status, Is.EqualTo(RomanticEligibilityStatus.ProhibitedKinship));
            Assert.That(fixture.Family.GetParents("person.prototype.child", ParentageKind.Biological, privileged: true).Count, Is.EqualTo(1));
            Assert.That(fixture.Family.GetParents("person.prototype.child", ParentageKind.Adoptive, privileged: true).Count, Is.EqualTo(1));
        }

        [Test]
        public void KinshipQueriesAreDerivedDeterministicAndVisibilityAware()
        {
            using TestFixture fixture = CreateFixture();
            fixture.Family.RecordParentage(Parentage("child-parent", "person.prototype.parent", "person.prototype.child", ParentageKind.Biological, FamilyVisibility.Public));
            fixture.Family.RecordParentage(Parentage("student-parent", "person.prototype.parent", "person.prototype.student", ParentageKind.Biological, FamilyVisibility.Public));
            fixture.Family.RecordParentage(Parentage("hidden-parent", "person.prototype.mentor", "person.prototype.rival", ParentageKind.Biological, FamilyVisibility.Hidden));

            KinshipPathResult first = fixture.Family.ResolveKinship("person.prototype.child", "person.prototype.student", privileged: false);
            KinshipPathResult second = fixture.Family.ResolveKinship("person.prototype.child", "person.prototype.student", privileged: false);
            FamilyTreeSnapshot publicTree = fixture.Family.CreateFamilyTreeSnapshot("person.prototype.rival", privileged: false);
            FamilyTreeSnapshot privilegedTree = fixture.Family.CreateFamilyTreeSnapshot("person.prototype.rival", privileged: true);
            FamilyTreeSnapshot truncated = fixture.Family.CreateFamilyTreeSnapshot("person.prototype.child", new KinshipTraversalLimits { maximumVisitedPersons = 1, maximumAncestorDepth = 1, maximumDescendantDepth = 1 }, privileged: true);

            Assert.That(first.Classification, Is.EqualTo(KinshipClassification.HalfSibling));
            Assert.That(second.Classification, Is.EqualTo(first.Classification));
            Assert.That(publicTree.Relationships, Is.Empty);
            Assert.That(privilegedTree.Relationships.Count, Is.EqualTo(1));
            Assert.That(truncated.Truncated, Is.True);
        }

        [Test]
        public void RomanticAttractionIsDirectionalAndCannotReplaceConsentOrEligibility()
        {
            using TestFixture fixture = CreateFixture();
            fixture.Attitudes.Mutate(Attitude("attraction-one-way", PersistenceService.LocalPlayerId, "person.prototype.partner", PrototypeAttitudeDefinitionFactory.RomanticAttractionId, 90));

            RomanticEligibilityResult missingConsent = fixture.Family.EvaluateRomanticEligibility(RomanceRequest(PersistenceService.LocalPlayerId, "person.prototype.partner", RomanticConsentKind.None));
            RomanticEligibilityResult compliance = fixture.Family.EvaluateRomanticEligibility(RomanceRequest(PersistenceService.LocalPlayerId, "person.prototype.partner", RomanticConsentKind.Compliance));
            RomanticEligibilityResult child = fixture.Family.EvaluateRomanticEligibility(RomanceRequest(PersistenceService.LocalPlayerId, "person.prototype.child", RomanticConsentKind.PlayerChoice));

            Assert.That(fixture.Attitudes.ResolveValue(PersistenceService.LocalPlayerId, "person.prototype.partner", PrototypeAttitudeDefinitionFactory.RomanticAttractionId).EffectiveValue, Is.EqualTo(90));
            Assert.That(fixture.Attitudes.ResolveValue("person.prototype.partner", PersistenceService.LocalPlayerId, PrototypeAttitudeDefinitionFactory.RomanticAttractionId).EffectiveValue, Is.Zero);
            Assert.That(missingConsent.Eligible, Is.False);
            Assert.That(missingConsent.Status, Is.EqualTo(RomanticEligibilityStatus.MissingConsent));
            Assert.That(compliance.Eligible, Is.False);
            Assert.That(compliance.Status, Is.EqualTo(RomanticEligibilityStatus.InvalidConsent));
            Assert.That(child.Eligible, Is.False);
            Assert.That(child.Status, Is.EqualTo(RomanticEligibilityStatus.NonAdult));
        }

        [Test]
        public void RomanticLifecycleUsesRelationshipRuntimeAndIsIdempotent()
        {
            using TestFixture fixture = CreateFixture();
            RomanticTransitionResult first = fixture.Family.ExecuteRomanticTransition(new RomanticTransitionRequest
            {
                transactionId = "test.family.romance.partner",
                relationshipRecordId = "relationship.test.family.partner",
                actorPersonId = PersistenceService.LocalPlayerId,
                targetPersonId = "person.prototype.partner",
                policyDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId,
                transitionKind = RomanticTransitionKind.EstablishPartnership,
                consentKind = RomanticConsentKind.PlayerChoice,
                worldTime = 10d
            });
            RomanticTransitionResult duplicate = fixture.Family.ExecuteRomanticTransition(new RomanticTransitionRequest
            {
                transactionId = "test.family.romance.partner",
                relationshipRecordId = "relationship.test.family.partner",
                actorPersonId = PersistenceService.LocalPlayerId,
                targetPersonId = "person.prototype.partner",
                policyDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId,
                transitionKind = RomanticTransitionKind.EstablishPartnership,
                consentKind = RomanticConsentKind.PlayerChoice,
                worldTime = 10d
            });

            Assert.That(first.Succeeded, Is.True, first.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(fixture.Relationships.QueryByDefinition(PrototypeRelationshipDefinitionFactory.DomesticPartnerRelationshipId, activeOnly: true).Count, Is.EqualTo(1));
            Assert.That(fixture.Family.HouseholdCount, Is.Zero);
        }

        [Test]
        public void HouseholdLifecycleIsPersistentAndSeparateFromRelationshipRecords()
        {
            using TestFixture fixture = CreateFixture();
            HouseholdMutationResult create = fixture.Family.CreateHousehold(new HouseholdMutationRequest { transactionId = "test.family.household.create", householdId = "household.test.family", householdDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId, personId = PersistenceService.LocalPlayerId, role = HouseholdRole.Head, residencePlaceId = "place.prototype.home", propertyReferenceId = "property.prototype.home", worldTime = 1d });
            HouseholdMutationResult add = fixture.Family.AddMember(new HouseholdMutationRequest { transactionId = "test.family.household.add", householdId = "household.test.family", personId = "person.prototype.partner", role = HouseholdRole.CoHead, worldTime = 2d });
            int relationshipsBefore = fixture.Relationships.Count;
            FamilyRelationshipRuntimeSaveData save = fixture.Family.CreateSaveData();
            FamilyRelationshipRuntime restored = CreateRuntime(fixture.Registry, fixture.Relationships, fixture.Attitudes, fixture.Interactions);
            RomanticTransitionResult restore = restored.RestoreFromSaveData(save, fixture.Registry, KnownPersons, restoringState: true);

            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(add.Succeeded, Is.True, add.Message);
            Assert.That(relationshipsBefore, Is.EqualTo(fixture.Relationships.Count));
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetHousehold("household.test.family", out HouseholdSnapshot snapshot), Is.True);
            Assert.That(snapshot.ActiveMemberships.Count, Is.EqualTo(2));
        }

        [Test]
        public void PersistenceParticipantRejectsCorruptHouseholdPayloadWithoutMutation()
        {
            using TestFixture fixture = CreateFixture();
            fixture.Family.CreateHousehold(new HouseholdMutationRequest { transactionId = "test.family.persist.create", householdId = "household.test.persist", householdDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.FamilyHouseholdDefinitionId, personId = PersistenceService.LocalPlayerId, role = HouseholdRole.Head, worldTime = 1d });
            FamilyRelationshipPersistenceParticipant participant = new FamilyRelationshipPersistenceParticipant(fixture.Family, () => fixture.Registry, () => KnownPersons);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            FamilyRelationshipRuntimeSaveData corrupt = JsonUtility.FromJson<FamilyRelationshipRuntimeSaveData>(save.PayloadJson);
            corrupt.households[0].worldId = "world.other";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), FamilyRelationshipPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Family.TryGetHousehold("household.test.persist", out HouseholdSnapshot snapshot), Is.True);
            Assert.That(snapshot.WorldId, Is.EqualTo(PersistenceService.LocalWorldId));
        }

        private static FamilyParentageRequest Parentage(string suffix, string parent, string child, ParentageKind kind, FamilyVisibility visibility = FamilyVisibility.Public)
        {
            return new FamilyParentageRequest
            {
                transactionId = $"test.family.parentage.{suffix}",
                recordId = $"relationship.test.family.{suffix}",
                parentPersonId = parent,
                childPersonId = child,
                parentageKind = kind,
                visibility = visibility,
                worldTime = 1d
            };
        }

        private static RomanticEligibilityRequest RomanceRequest(string actor, string target, RomanticConsentKind consent)
        {
            return new RomanticEligibilityRequest
            {
                actorPersonId = actor,
                targetPersonId = target,
                policyDefinitionId = PrototypeFamilyRelationshipDefinitionFactory.StrictAdultRomancePolicyId,
                transitionKind = RomanticTransitionKind.EstablishPartnership,
                consentKind = consent
            };
        }

        private static AttitudeMutationRequest Attitude(string transactionId, string observer, string subject, string dimension, int value)
        {
            return new AttitudeMutationRequest
            {
                transactionId = transactionId,
                observerPersonId = observer,
                subjectPersonId = subject,
                dimensionId = dimension,
                mutationKind = AttitudeMutationKind.SetBaseline,
                value = value,
                sourceCategory = AttitudeContributionSourceCategory.TestLab,
                worldTime = 1d
            };
        }

        private static TestFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            RelationshipRuntime relationships = new RelationshipRuntime();
            relationships.Configure(registry, KnownPersons);
            InterpersonalAttitudeRuntime attitudes = new InterpersonalAttitudeRuntime();
            attitudes.Configure(registry, KnownPersons);
            ReputationRuntime reputation = new ReputationRuntime();
            reputation.Configure(registry, KnownPersons);
            RumorRuntime rumors = new RumorRuntime();
            rumors.Configure(registry, KnownPersons, _ => null, _ => null);
            SocialInteractionRuntime interactions = new SocialInteractionRuntime();
            interactions.Configure(registry, KnownPersons, relationships, attitudes, reputation, rumors);
            FamilyRelationshipRuntime family = CreateRuntime(registry, relationships, attitudes, interactions);
            return new TestFixture(registry, relationships, attitudes, interactions, family);
        }

        private static FamilyRelationshipRuntime CreateRuntime(DefinitionRegistry registry, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, SocialInteractionRuntime interactions)
        {
            FamilyRelationshipRuntime runtime = new FamilyRelationshipRuntime();
            runtime.Configure(registry, KnownPersons, relationships, attitudes, interactions, PersistenceService.LocalWorldId, AdultPersons);
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeFamilyRelationshipDefinitionFactory.AddMissingPrototypeFamilyRelationshipDefinitions(catalog.CreateRegistry());
        }

        private sealed class TestFixture : IDisposable
        {
            public TestFixture(DefinitionRegistry registry, RelationshipRuntime relationships, InterpersonalAttitudeRuntime attitudes, SocialInteractionRuntime interactions, FamilyRelationshipRuntime family)
            {
                Registry = registry;
                Relationships = relationships;
                Attitudes = attitudes;
                Interactions = interactions;
                Family = family;
            }

            public DefinitionRegistry Registry { get; }
            public RelationshipRuntime Relationships { get; }
            public InterpersonalAttitudeRuntime Attitudes { get; }
            public SocialInteractionRuntime Interactions { get; }
            public FamilyRelationshipRuntime Family { get; }

            public void Dispose()
            {
            }
        }
    }
}
#endif
