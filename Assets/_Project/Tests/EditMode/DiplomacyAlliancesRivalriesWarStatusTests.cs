using System;
using System.Linq;
using NUnit.Framework;
using UnityIsekaiGame.Development.Automation;
using UnityIsekaiGame.Diplomacy;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;
using UnityEngine;

namespace UnityIsekaiGame.Tests
{
    public sealed class DiplomacyAlliancesRivalriesWarStatusTests
    {
        private const string PersonId = "person.prototype.player";
        private const string FriendId = "person.prototype.friend";

        [Test]
        public void PrototypeCatalog_ResolvesDiplomacyDefinitionsAndValidates()
        {
            DefinitionRegistry registry = CreateRegistry();
            DefinitionCatalog catalog = ClassificationTestFactory.CreateCatalog(registry.DefinitionsById.Values.OfType<ScriptableObject>().ToArray());
            DefinitionValidationReport report = DefinitionCatalogValidator.Validate(catalog);

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(report.WarningCount, Is.Zero, report.ToString());
            Assert.That(registry.TryGet(PrototypeDiplomacyDefinitionFactory.AllianceRelationId, out DiplomaticRelationDefinition relation), Is.True);
            Assert.That(registry.TryGet(PrototypeDiplomacyDefinitionFactory.MutualDefenseAgreementId, out DiplomaticAgreementDefinition agreement), Is.True);
            Assert.That(registry.TryGet(PrototypeDiplomacyDefinitionFactory.FormalWarDefinitionId, out DiplomaticWarDefinition war), Is.True);
            Assert.That(relation.Category, Is.EqualTo(DiplomaticRelationCategory.Allied));
            Assert.That(agreement.Category, Is.EqualTo(DiplomaticAgreementCategory.MutualDefense));
            Assert.That(war.SupportsFactionalParticipants, Is.False);
        }

        [Test]
        public void RelationsMirrorAndRejectInternalFactionDiplomaticActors()
        {
            RuntimeFixture fixture = CreateFixture();
            FactionOperationResult internalFaction = fixture.Factions.CreateFaction(FactionCreate("internal", PrototypeFactionDefinitionFactory.ReformFactionId, FactionHostContextData.ForOrganization("organization.prototype.adventurers-guild")));
            FactionOperationResult crossFaction = fixture.Factions.CreateFaction(FactionCreate("cross", PrototypeFactionDefinitionFactory.CrossOrgMovementFactionId, new FactionHostContextData { contextKind = FactionHostContextKind.MultipleOrganizations, organizationIds = new[] { "organization.prototype.adventurers-guild", "organization.prototype.royal-forge" } }));

            DiplomacyOperationResult recognition = fixture.Diplomacy.CreateRelation(Relation("recognition", PrototypeDiplomacyDefinitionFactory.RecognitionRelationId, Org("organization.prototype.adventurers-guild"), Org("organization.prototype.royal-forge")));
            DiplomacyOperationResult duplicate = fixture.Diplomacy.CreateRelation(Relation("recognition", PrototypeDiplomacyDefinitionFactory.RecognitionRelationId, Org("organization.prototype.adventurers-guild"), Org("organization.prototype.royal-forge")));
            DiplomacyOperationResult rivalry = fixture.Diplomacy.CreateRelation(Relation("rivalry", PrototypeDiplomacyDefinitionFactory.RivalryRelationId, Org("organization.prototype.adventurers-guild"), ActorFaction(crossFaction.Faction?.factionId)));
            DiplomacyOperationResult rejected = fixture.Diplomacy.CreateRelation(Relation("internal", PrototypeDiplomacyDefinitionFactory.AllianceRelationId, Org("organization.prototype.adventurers-guild"), ActorFaction(internalFaction.Faction?.factionId)));

            Assert.That(internalFaction.Succeeded, Is.True, internalFaction.Message);
            Assert.That(crossFaction.Succeeded, Is.True, crossFaction.Message);
            Assert.That(recognition.Succeeded, Is.True, recognition.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(rivalry.Succeeded, Is.True, rivalry.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(DiplomaticOperationCode.ActorIneligible));
            Assert.That(fixture.Diplomacy.QueryRelationsForActor(Org("organization.prototype.royal-forge"), activeOnly: true).Any(item => item.relationId.EndsWith(".reciprocal", StringComparison.Ordinal)), Is.True);
        }

        [Test]
        public void AgreementLifecycleBreachWarAndPersistenceValidateWithoutReplay()
        {
            RuntimeFixture fixture = CreateFixture();
            string agreementId = "diplomatic-agreement.test.mutual-defense";
            string clauseId = "diplomatic-clause-record.test.defense";
            DiplomacyOperationResult agreement = fixture.Diplomacy.CreateAgreement(new DiplomaticAgreementRequest
            {
                transactionId = "tx.diplomacy.agreement",
                agreementId = agreementId,
                agreementDefinitionId = PrototypeDiplomacyDefinitionFactory.MutualDefenseAgreementId,
                title = "Test Defense Pact",
                parties = new[] { Party($"{agreementId}.guild", Org("organization.prototype.adventurers-guild")), Party($"{agreementId}.forge", Org("organization.prototype.royal-forge")) },
                clauses = new[] { Clause(clauseId, PrototypeDiplomacyDefinitionFactory.DefenseAssistanceClauseId) },
                worldTime = 1d
            });
            DiplomacyOperationResult sign = fixture.Diplomacy.SignAgreement(new DiplomaticSignatureRequest { transactionId = "tx.diplomacy.sign", agreementId = agreementId, partyId = $"{agreementId}.guild", signerPersonId = PersonId, worldTime = 2d });
            DiplomacyOperationResult activate = fixture.Diplomacy.ActivateAgreement("tx.diplomacy.activate", agreementId, 3d);
            DiplomacyOperationResult breach = fixture.Diplomacy.RecordBreach(new DiplomaticBreachRequest { transactionId = "tx.diplomacy.breach", breachId = "diplomatic-breach.test.defense", agreementId = agreementId, clauseId = clauseId, allegedActor = Org("organization.prototype.royal-forge"), state = DiplomaticBreachState.Confirmed, worldTime = 4d });
            DiplomacyOperationResult war = fixture.Diplomacy.DeclareWar(new DiplomaticWarDeclarationRequest { transactionId = "tx.diplomacy.war", warId = "diplomatic-war.test.guild-forge", warDefinitionId = PrototypeDiplomacyDefinitionFactory.FormalWarDefinitionId, sideA = new[] { Org("organization.prototype.adventurers-guild") }, sideB = new[] { Org("organization.prototype.royal-forge") }, worldTime = 5d });
            DiplomacyOperationResult peace = fixture.Diplomacy.TransitionWar("tx.diplomacy.peace", "diplomatic-war.test.guild-forge", DiplomaticWarLifecycleState.Ended, 6d, "diplomatic-agreement.test.peace");

            DiplomacyRuntimeSaveData save = fixture.Diplomacy.CreateSaveData();
            DiplomacyRuntime restored = new DiplomacyRuntime();
            DiplomacyOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry, fixture.Organizations, fixture.Factions, fixture.Authority, fixture.Decisions, fixture.Resources, PersistenceService.LocalWorldId, new[] { PersonId, FriendId }, restoring: true);
            DiplomacyRuntimeSaveData corrupt = save.Clone();
            corrupt.clauses[0].agreementId = "diplomatic-agreement.missing";
            bool rejected = !DiplomacyRuntime.ValidateSaveData(corrupt, fixture.Registry, fixture.Organizations, fixture.Factions, PersistenceService.LocalWorldId, new[] { PersonId, FriendId }, out string failure);

            Assert.That(agreement.Succeeded, Is.True, agreement.Message);
            Assert.That(sign.Succeeded, Is.True, sign.Message);
            Assert.That(activate.Succeeded, Is.True, activate.Message);
            Assert.That(breach.Succeeded, Is.True, breach.Message);
            Assert.That(war.Succeeded, Is.True, war.Message);
            Assert.That(peace.Succeeded, Is.True, peace.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.AgreementCount, Is.EqualTo(fixture.Diplomacy.AgreementCount));
            Assert.That(restored.WarCount, Is.EqualTo(fixture.Diplomacy.WarCount));
            Assert.That(rejected, Is.True, failure);
            Assert.That(fixture.Diplomacy.AgreementCount, Is.EqualTo(1));
            restored.Dispose();
        }

        [Test]
        public void PersistenceParticipantRejectsCorruptPayloadBeforeCommitWithoutMutation()
        {
            RuntimeFixture fixture = CreateFixture();
            fixture.Diplomacy.CreateRelation(Relation("secret", PrototypeDiplomacyDefinitionFactory.CooperativeRelationId, Org("organization.prototype.adventurers-guild"), Org("organization.prototype.royal-forge"), DiplomaticVisibility.Secret));
            DiplomacyPersistenceParticipant participant = new DiplomacyPersistenceParticipant(fixture.Diplomacy, () => fixture.Registry, () => fixture.Organizations, () => fixture.Factions, () => fixture.Authority, () => fixture.Decisions, () => fixture.Resources, PersistenceService.LocalWorldId, () => new[] { PersonId, FriendId });
            string goodPayload = participant.CapturePayload().PayloadJson;
            DiplomacyRuntimeSaveData corrupt = fixture.Diplomacy.CreateSaveData();
            corrupt.relations[0].targetActor.actorId = "organization.prototype.missing";
            string badPayload = UnityEngine.JsonUtility.ToJson(corrupt);
            long beforeRevision = fixture.Diplomacy.Revision;

            PersistenceParticipantPrepareResult badPrepare = participant.PreparePayload(badPayload, DiplomacyPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantPrepareResult goodPrepare = participant.PreparePayload(goodPayload, DiplomacyPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(badPrepare.Succeeded, Is.False);
            Assert.That(fixture.Diplomacy.Revision, Is.EqualTo(beforeRevision));
            Assert.That(goodPrepare.Succeeded, Is.True, goodPrepare.Message);
        }

        private static RuntimeFixture CreateFixture()
        {
            DefinitionRegistry registry = CreateRegistry();
            OrganizationRuntime organizations = new OrganizationRuntime();
            PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(organizations, registry, PersistenceService.LocalWorldId);
            organizations.Configure(registry, PersistenceService.LocalWorldId, new[] { PersonId, FriendId }, Array.Empty<string>());
            OrganizationMembershipRuntime memberships = new OrganizationMembershipRuntime();
            memberships.Configure(registry, organizations, PersistenceService.LocalWorldId, new[] { PersonId, FriendId }, PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds);
            OrganizationAuthorityRuntime authority = new OrganizationAuthorityRuntime();
            authority.Configure(registry, organizations, memberships, PersistenceService.LocalWorldId, new[] { PersonId, FriendId }, PrototypeOrganizationDefinitionFactory.PrototypeOrganizationIds);
            OrganizationResourceRuntime resources = new OrganizationResourceRuntime();
            resources.Configure(registry, organizations, authority, null, PersistenceService.LocalWorldId);
            OrganizationDecisionRuntime decisions = new OrganizationDecisionRuntime();
            decisions.Configure(registry, organizations, memberships, authority, resources, PersistenceService.LocalWorldId, new[] { PersonId, FriendId }, null);
            FactionRuntime factions = new FactionRuntime();
            factions.Configure(registry, organizations, memberships, authority, resources, decisions, PersistenceService.LocalWorldId, new[] { PersonId, FriendId });
            DiplomacyRuntime diplomacy = new DiplomacyRuntime();
            diplomacy.Configure(registry, organizations, factions, authority, decisions, resources, PersistenceService.LocalWorldId, new[] { PersonId, FriendId });
            return new RuntimeFixture(registry, organizations, memberships, authority, resources, decisions, factions, diplomacy);
        }

        private static DefinitionRegistry CreateRegistry()
        {
            return PrototypeDiplomacyDefinitionFactory.AddMissingPrototypeDiplomacyDefinitions(
                PrototypeFactionDefinitionFactory.AddMissingPrototypeFactionDefinitions(
                    PrototypeOrganizationDecisionDefinitionFactory.AddMissingPrototypeOrganizationDecisionDefinitions(
                        PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(
                            PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(
                                PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(
                                    PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(new DefinitionRegistry(Array.Empty<IGameDefinition>()))))))));
        }

        private static DiplomaticRelationRequest Relation(string suffix, string definitionId, DiplomaticActorReferenceData source, DiplomaticActorReferenceData target, DiplomaticVisibility visibility = DiplomaticVisibility.Public)
        {
            return new DiplomaticRelationRequest
            {
                transactionId = $"tx.diplomacy.relation.{suffix}",
                relationId = $"diplomatic-relation.test.{suffix}",
                relationDefinitionId = definitionId,
                sourceActor = source,
                targetActor = target,
                visibility = visibility,
                worldTime = 1d
            };
        }

        private static DiplomaticAgreementPartyRecordData Party(string partyId, DiplomaticActorReferenceData actor)
        {
            return new DiplomaticAgreementPartyRecordData { partyId = partyId, actor = actor, role = DiplomaticPartyRole.Principal, joinedWorldTime = 1d, active = true };
        }

        private static DiplomaticClauseRecordData Clause(string clauseId, string definitionId)
        {
            return new DiplomaticClauseRecordData { clauseId = clauseId, clauseDefinitionId = definitionId, category = DiplomaticClauseCategory.DefenseAssistance, lifecycleState = DiplomaticClauseLifecycleState.Draft, visibility = DiplomaticVisibility.Restricted, effectiveWorldTime = 1d };
        }

        private static FactionCreateRequest FactionCreate(string suffix, string definitionId, FactionHostContextData host)
        {
            return new FactionCreateRequest
            {
                transactionId = $"tx.faction.{suffix}",
                factionId = $"faction.test.{suffix}",
                factionDefinitionId = definitionId,
                officialName = $"Faction {suffix}",
                hostContext = host,
                founderPersonId = PersonId,
                founderOrganizationId = host?.primaryOrganizationId ?? string.Empty,
                worldTime = 1d,
                initialState = FactionLifecycleState.Active,
                visibility = FactionVisibility.Public
            };
        }

        private static DiplomaticActorReferenceData Org(string id) => DiplomaticActorReferenceData.Organization(id, PersistenceService.LocalWorldId);
        private static DiplomaticActorReferenceData ActorFaction(string id) => DiplomaticActorReferenceData.Faction(id, PersistenceService.LocalWorldId);

        private sealed class RuntimeFixture
        {
            public RuntimeFixture(DefinitionRegistry registry, OrganizationRuntime organizations, OrganizationMembershipRuntime memberships, OrganizationAuthorityRuntime authority, OrganizationResourceRuntime resources, OrganizationDecisionRuntime decisions, FactionRuntime factions, DiplomacyRuntime diplomacy)
            {
                Registry = registry;
                Organizations = organizations;
                Memberships = memberships;
                Authority = authority;
                Resources = resources;
                Decisions = decisions;
                Factions = factions;
                Diplomacy = diplomacy;
            }

            public DefinitionRegistry Registry { get; }
            public OrganizationRuntime Organizations { get; }
            public OrganizationMembershipRuntime Memberships { get; }
            public OrganizationAuthorityRuntime Authority { get; }
            public OrganizationResourceRuntime Resources { get; }
            public OrganizationDecisionRuntime Decisions { get; }
            public FactionRuntime Factions { get; }
            public DiplomacyRuntime Diplomacy { get; }
        }
    }
}
