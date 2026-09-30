#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Knowledge;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;

namespace UnityIsekaiGame.Tests
{
    public sealed class OrganizationIdentityRecordsTests
    {
        private const string CatalogPath = "Packages/com.thequantifier.isekai.content/Content/Prototype/GameData/PrototypeDefinitionCatalog.asset";
        private const string PersonId = PersistenceService.LocalPlayerId;
        private static readonly string[] KnownPersons = { PersonId, "person.prototype.friend", "person.prototype.founder" };
        private static readonly string[] KnownPlaces = { "place.prototype.guild-hall", "place.prototype.market", "place.prototype.arena" };

        [Test]
        public void PrototypeOrganizationDefinitionsValidateAndResolve()
        {
            DefinitionRegistry registry = CreateRegistry();

            Assert.That(registry.TryGet(PrototypeOrganizationDefinitionFactory.GuildDefinitionId, out OrganizationDefinition guild), Is.True);
            Assert.That(guild.Category, Is.EqualTo(OrganizationCategory.Guild));
            Assert.That(guild.SupportsVisibility(OrganizationVisibility.Public), Is.True);

            DefinitionValidationReport report = new DefinitionValidationReport();
            foreach (OrganizationDefinition definition in PrototypeOrganizationDefinitionFactory.CreateMissingOrganizationDefinitions(Array.Empty<string>()))
            {
                definition.ValidateCatalogDefinition(registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.EqualTo(0), report.ToString());
            Assert.That((int)CategoryDomain.Organization, Is.EqualTo(25), "Organization must remain appended so existing serialized category domains keep their numeric values.");
            Assert.That((int)KnowledgeSubjectType.Organization, Is.EqualTo(11), "Organization must remain appended so existing serialized knowledge subject types keep their numeric values.");
        }

        [Test]
        public void CreateRenameAndLifecycleUseStableIdentityAndImmutableSnapshots()
        {
            OrganizationRuntime runtime = CreateRuntime();

            OrganizationOperationResult preview = runtime.CreateOrganization(CreateGuildRequest("organization.test.guild", "Prototype Guild", preview: true));
            OrganizationOperationResult create = runtime.CreateOrganization(CreateGuildRequest("organization.test.guild", "Prototype Guild"));
            OrganizationOperationResult duplicate = runtime.CreateOrganization(CreateGuildRequest("organization.test.guild", "Prototype Guild"));
            OrganizationSnapshot beforeRename = create.Snapshot;
            beforeRename.Data.currentName = "Mutated Snapshot";

            OrganizationOperationResult rename = runtime.RenameOrganization(new OrganizationRenameRequest
            {
                organizationId = "organization.test.guild",
                newOfficialName = "Prototype Guild Hall",
                effectiveWorldTime = 10d,
                transactionId = "tx.organization.rename.guild"
            });
            OrganizationOperationResult dormant = runtime.TransitionLifecycle(new OrganizationLifecycleTransitionRequest
            {
                organizationId = "organization.test.guild",
                targetState = OrganizationLifecycleState.Dormant,
                worldTime = 15d,
                transactionId = "tx.organization.lifecycle.dormant"
            });

            Assert.That(preview.Status, Is.EqualTo(OrganizationOperationStatus.Preview));
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(rename.Succeeded, Is.True, rename.Message);
            Assert.That(dormant.Succeeded, Is.True, dormant.Message);
            Assert.That(runtime.TryGetSnapshot("organization.test.guild", out OrganizationSnapshot after), Is.True);
            Assert.That(after.OrganizationId, Is.EqualTo("organization.test.guild"));
            Assert.That(after.CurrentName, Is.EqualTo("Prototype Guild Hall"));
            Assert.That(after.LifecycleState, Is.EqualTo(OrganizationLifecycleState.Dormant));
            Assert.That(after.Names.Count(name => name.category == OrganizationNameCategory.FormerOfficial), Is.EqualTo(1));
            Assert.That(after.Names.Any(name => name.value == "Guildhouse"), Is.True);
        }

        [Test]
        public void ParentBranchLinksRejectCyclesAndUnsupportedDefinitions()
        {
            OrganizationRuntime runtime = CreateRuntime();
            runtime.CreateOrganization(CreateGuildRequest("organization.test.parent", "Parent Guild"));
            runtime.CreateOrganization(CreateGuildRequest("organization.test.branch", "Branch Guild"));
            runtime.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = "organization.test.household",
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.HouseholdDefinitionId,
                officialName = "Household",
                initialLifecycleState = OrganizationLifecycleState.Active,
                founders = new[] { new OrganizationFounderReferenceData { kind = OrganizationFounderKind.Person, subjectId = PersonId } },
                transactionId = "tx.organization.create.household"
            });

            OrganizationOperationResult link = runtime.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = "organization.test.branch",
                targetOrganizationId = "organization.test.parent",
                kind = OrganizationLinkKind.Parent,
                transactionId = "tx.organization.link.parent"
            });
            OrganizationOperationResult cycle = runtime.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = "organization.test.parent",
                targetOrganizationId = "organization.test.branch",
                kind = OrganizationLinkKind.Parent,
                transactionId = "tx.organization.link.cycle"
            });
            OrganizationOperationResult unsupported = runtime.LinkOrganizations(new OrganizationLinkRequest
            {
                sourceOrganizationId = "organization.test.household",
                targetOrganizationId = "organization.test.parent",
                kind = OrganizationLinkKind.Parent,
                transactionId = "tx.organization.link.unsupported"
            });

            Assert.That(link.Succeeded, Is.True, link.Message);
            Assert.That(runtime.QueryByParent("organization.test.parent").Single().OrganizationId, Is.EqualTo("organization.test.branch"));
            Assert.That(cycle.Status, Is.EqualTo(OrganizationOperationStatus.CycleDetected));
            Assert.That(unsupported.Status, Is.EqualTo(OrganizationOperationStatus.UnsupportedByDefinition));
        }

        [Test]
        public void ProjectionsExposeStep8SubjectAndRespectVisibility()
        {
            OrganizationRuntime runtime = CreateRuntime();
            runtime.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = "organization.test.secret",
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.SecretSocietyDefinitionId,
                officialName = "Lantern Circle",
                initialLifecycleState = OrganizationLifecycleState.Active,
                visibility = OrganizationVisibility.Secret,
                founders = new[] { new OrganizationFounderReferenceData { kind = OrganizationFounderKind.Person, subjectId = PersonId } },
                headquartersPlaceId = "place.prototype.guild-hall",
                sourceEventId = "event.organization.secret",
                transactionId = "tx.organization.create.secret"
            });
            runtime.CreateOrganization(new OrganizationCreateRequest
            {
                organizationId = "organization.test.hidden",
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.SecretSocietyDefinitionId,
                officialName = "Hidden Hand",
                initialLifecycleState = OrganizationLifecycleState.Active,
                visibility = OrganizationVisibility.Hidden,
                founders = new[] { new OrganizationFounderReferenceData { kind = OrganizationFounderKind.Person, subjectId = PersonId } },
                transactionId = "tx.organization.create.hidden"
            });

            OrganizationProjection redacted = runtime.ProjectOrganization("organization.test.secret", "person.prototype.friend");
            OrganizationProjection full = runtime.ProjectOrganization("organization.test.secret", "person.prototype.friend", privileged: true);
            OrganizationProjection hidden = runtime.ProjectOrganization("organization.test.hidden", "person.prototype.friend");

            Assert.That(redacted.Access, Is.EqualTo(OrganizationProjectionAccess.Redacted));
            Assert.That(redacted.Subject.subjectType, Is.EqualTo(InformationSubjectType.Organization));
            Assert.That(redacted.Snapshot.HeadquartersPlaceId, Is.Empty);
            Assert.That(full.Access, Is.EqualTo(OrganizationProjectionAccess.Full));
            Assert.That(full.Snapshot.HeadquartersPlaceId, Is.EqualTo("place.prototype.guild-hall"));
            Assert.That(hidden.Access, Is.EqualTo(OrganizationProjectionAccess.Concealed));
            Assert.That(hidden.Snapshot, Is.Null);
        }

        [Test]
        public void PersistenceParticipantRejectsInvalidRestoreWithoutMutation()
        {
            DefinitionRegistry registry = CreateRegistry();
            OrganizationRuntime runtime = CreateRuntime(registry);
            runtime.CreateOrganization(CreateGuildRequest("organization.test.persisted", "Persisted Guild"));
            OrganizationPersistenceParticipant participant = new OrganizationPersistenceParticipant(runtime, () => registry, PersistenceService.LocalWorldId, () => KnownPersons, () => KnownPlaces);
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            OrganizationRuntimeSaveData corrupt = JsonUtility.FromJson<OrganizationRuntimeSaveData>(save.PayloadJson);
            corrupt.records[0].organizationDefinitionId = "organization-definition.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), OrganizationPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(save.Succeeded, Is.True, save.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(runtime.Count, Is.EqualTo(1));
            Assert.That(runtime.TryGetSnapshot("organization.test.persisted", out OrganizationSnapshot snapshot), Is.True);
            Assert.That(snapshot.CurrentName, Is.EqualTo("Persisted Guild"));
        }

        private static OrganizationCreateRequest CreateGuildRequest(string organizationId, string name, bool preview = false)
        {
            return new OrganizationCreateRequest
            {
                organizationId = organizationId,
                organizationDefinitionId = PrototypeOrganizationDefinitionFactory.GuildDefinitionId,
                officialName = name,
                shortName = "Guild",
                aliases = new[] { "Guildhouse" },
                initialLifecycleState = OrganizationLifecycleState.Active,
                founders = new[] { new OrganizationFounderReferenceData { kind = OrganizationFounderKind.Person, subjectId = PersonId } },
                headquartersPlaceId = "place.prototype.guild-hall",
                operatingAreaPlaceIds = new[] { "place.prototype.market" },
                transactionId = $"tx.organization.create.{organizationId}",
                preview = preview
            };
        }

        private static OrganizationRuntime CreateRuntime(DefinitionRegistry registry = null)
        {
            OrganizationRuntime runtime = new OrganizationRuntime();
            runtime.Configure(registry ?? CreateRegistry(), PersistenceService.LocalWorldId, KnownPersons, KnownPlaces);
            return runtime;
        }

        private static DefinitionRegistry CreateRegistry()
        {
            DefinitionCatalog catalog = AssetDatabase.LoadAssetAtPath<DefinitionCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            return PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(catalog.CreateRegistry());
        }
    }
}
#endif
