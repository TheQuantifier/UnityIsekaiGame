#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Organizations;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class OrganizationResourcesTreasuriesPropertyTests
    {
        private const string GuildId = "organization.prototype.adventurers-guild";
        private const string ActorId = PersistenceService.LocalPlayerId;

        [Test]
        public void PrototypeResourceDefinitionsValidateAndAuthorityRolesExposeRequiredPermissions()
        {
            Fixture fixture = Fixture.Create();
            DefinitionValidationReport report = new DefinitionValidationReport();

            foreach (OrganizationResourceTypeDefinition definition in fixture.Registry.DefinitionsById.Values.OfType<OrganizationResourceTypeDefinition>())
            {
                definition.ValidateCatalogDefinition(fixture.Registry.DefinitionsById, report);
            }

            Assert.That(report.ErrorCount, Is.Zero, report.ToString());
            Assert.That(fixture.Registry.TryGet(PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId, out OrganizationResourceTypeDefinition currency), Is.True);
            Assert.That(currency.Category, Is.EqualTo(OrganizationResourceCategory.Currency));
            Assert.That(fixture.Registry.TryGet(PrototypeOrganizationAuthorityDefinitionFactory.TreasurerRoleId, out OrganizationAuthorityRoleDefinition treasurer), Is.True);
            Assert.That(treasurer.GrantedPermissionIds, Does.Contain(PrototypeOrganizationAuthorityDefinitionFactory.TransferOrganizationFundsPermissionId));
            Assert.That(treasurer.GrantedPermissionIds, Does.Contain(PrototypeOrganizationAuthorityDefinitionFactory.ManageResourceReservationsPermissionId));
        }

        [Test]
        public void TreasuryAccountsDelegateBalancesAndTransactionsToEconomyRuntime()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(1000L);

            OrganizationResourceOperationResult preview = fixture.Resources.TransferFunds(fixture.Transfer("tx.resources.transfer", 125L, preview: true));
            OrganizationResourceOperationResult execute = fixture.Resources.TransferFunds(fixture.Transfer("tx.resources.transfer", 125L));
            OrganizationResourceOperationResult duplicate = fixture.Resources.TransferFunds(fixture.Transfer("tx.resources.transfer", 125L));

            Assert.That(preview.Succeeded, Is.True, preview.Message);
            Assert.That(preview.Preview, Is.True);
            Assert.That(preview.RevisionBefore, Is.EqualTo(preview.RevisionAfter));
            Assert.That(execute.Succeeded, Is.True, execute.Message);
            Assert.That(duplicate.Duplicate, Is.True, duplicate.Message);
            Assert.That(fixture.Resources.GetBalance("organization-account.test.operating", 10d).BalanceUnits, Is.EqualTo(875L));
            Assert.That(fixture.Resources.GetBalance("organization-account.test.reserve", 10d).BalanceUnits, Is.EqualTo(125L));
            Assert.That(fixture.Economy.TryGetAccount("economy.organization.test.operating", out EconomyAccountSnapshot operating), Is.True);
            Assert.That(operating.BalanceUnits, Is.EqualTo(875L));
            Assert.That(fixture.Resources.Reconcile(GuildId, 10d).IsReconciled, Is.True);
        }

        [Test]
        public void RestrictionsReservationsAndBudgetsKeepAvailableFundsDistinct()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(1000L);

            OrganizationResourceOperationResult restriction = fixture.Resources.AddFundRestriction(new OrganizationFundRestrictionRequest
            {
                transactionId = "tx.resources.restriction",
                restrictionId = "organization-restriction.test.donation",
                organizationId = GuildId,
                accountId = "organization-account.test.operating",
                currencyDefinitionId = fixture.Currency.Id,
                units = 300L,
                allowedPurpose = "healing",
                sourceReferenceId = "donation.test.healing",
                actorPersonId = ActorId,
                startWorldTime = 10d
            });
            OrganizationResourceOperationResult budget = fixture.Resources.CreateBudget(new OrganizationBudgetRequest
            {
                transactionId = "tx.resources.budget",
                budgetId = "organization-budget.test.procurement",
                organizationId = GuildId,
                treasuryId = "organization-treasury.test.guild",
                accountId = "organization-account.test.operating",
                category = OrganizationBudgetCategory.Procurement,
                enforcementPolicy = OrganizationBudgetEnforcementPolicy.HardMaximum,
                currencyDefinitionId = fixture.Currency.Id,
                authorizedUnits = 250L,
                purpose = "procurement",
                actorPersonId = ActorId,
                startWorldTime = 10d
            });
            OrganizationResourceOperationResult reservation = fixture.Resources.ReserveResource(new OrganizationReservationRequest
            {
                transactionId = "tx.resources.reservation",
                reservationId = "organization-reservation.test.contract",
                organizationId = GuildId,
                accountId = "organization-account.test.operating",
                currencyDefinitionId = fixture.Currency.Id,
                amountUnits = 200L,
                category = OrganizationReservationCategory.Contract,
                purpose = "contract",
                requestingOperationId = "contract.test.procurement",
                actorPersonId = ActorId,
                startWorldTime = 10d,
                expirationWorldTime = 50d
            });
            OrganizationAccountBalanceSnapshot balance = fixture.Resources.GetBalance("organization-account.test.operating", 10d);

            Assert.That(restriction.Succeeded, Is.True, restriction.Message);
            Assert.That(budget.Succeeded, Is.True, budget.Message);
            Assert.That(reservation.Succeeded, Is.True, reservation.Message);
            Assert.That(balance.BalanceUnits, Is.EqualTo(1000L));
            Assert.That(balance.RestrictedUnits, Is.EqualTo(300L));
            Assert.That(balance.EncumberedUnits, Is.EqualTo(200L));
            Assert.That(balance.ReservedUnits, Is.Zero);
            Assert.That(balance.AvailableUnits, Is.EqualTo(500L));
        }

        [Test]
        public void FrozenAndClosedAccountLifecyclesMatchEconomyAndPreventUnsafeSpending()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(100L);

            OrganizationResourceOperationResult frozen = fixture.Resources.ChangeAccountLifecycle(fixture.Lifecycle("tx.resources.freeze", OrganizationAccountLifecycleState.Frozen, 20d));
            OrganizationResourceOperationResult blocked = fixture.Resources.TransferFunds(fixture.Transfer("tx.resources.frozen-transfer", 10L, worldTime: 21d));
            OrganizationResourceOperationResult active = fixture.Resources.ChangeAccountLifecycle(fixture.Lifecycle("tx.resources.activate", OrganizationAccountLifecycleState.Active, 22d));
            OrganizationResourceOperationResult transfer = fixture.Resources.TransferFunds(fixture.Transfer("tx.resources.empty", 100L, worldTime: 23d));
            OrganizationResourceOperationResult closed = fixture.Resources.ChangeAccountLifecycle(fixture.Lifecycle("tx.resources.close", OrganizationAccountLifecycleState.Closed, 24d));

            Assert.That(frozen.Succeeded, Is.True, frozen.Message);
            Assert.That(blocked.Code, Is.EqualTo(OrganizationResourceOperationCode.AccountFrozen));
            Assert.That(active.Succeeded, Is.True, active.Message);
            Assert.That(transfer.Succeeded, Is.True, transfer.Message);
            Assert.That(closed.Succeeded, Is.True, closed.Message);
            Assert.That(fixture.Economy.TryGetAccount("economy.organization.test.operating", out EconomyAccountSnapshot economyAccount), Is.True);
            Assert.That(economyAccount.Data.state, Is.EqualTo(EconomyAccountState.Closed));
        }

        [Test]
        public void PropertyAndBusinessAssociationsRequireMatchingAuthoritativeOwnership()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateOwnedPropertyAndBusiness();

            OrganizationResourceOperationResult property = fixture.Resources.AssociateProperty(new OrganizationAssociationRequest
            {
                transactionId = "tx.resources.property",
                associationId = "organization-property-association.test.hall",
                organizationId = GuildId,
                resourceId = "property.test.guild-hall",
                sourceRecordId = "property-ownership.test.guild-hall",
                category = (int)OrganizationPropertyAssociationCategory.Owner,
                actorPersonId = ActorId,
                startWorldTime = 10d
            });
            OrganizationResourceOperationResult business = fixture.Resources.AssociateBusiness(new OrganizationAssociationRequest
            {
                transactionId = "tx.resources.business",
                associationId = "organization-business-association.test.shop",
                organizationId = GuildId,
                resourceId = "business.test.guild-shop",
                sourceRecordId = "business-ownership.test.guild-shop",
                category = (int)OrganizationBusinessAssociationCategory.Owner,
                shareBasisPoints = 10000L,
                actorPersonId = ActorId,
                startWorldTime = 10d
            });
            OrganizationResourceOperationResult fabricated = fixture.Resources.AssociateProperty(new OrganizationAssociationRequest
            {
                transactionId = "tx.resources.property.fabricated",
                associationId = "organization-property-association.test.fabricated",
                organizationId = GuildId,
                resourceId = "property.test.guild-hall",
                sourceRecordId = "property-ownership.missing",
                category = (int)OrganizationPropertyAssociationCategory.Owner,
                actorPersonId = ActorId,
                startWorldTime = 10d
            });

            Assert.That(property.Succeeded, Is.True, property.Message);
            Assert.That(business.Succeeded, Is.True, business.Message);
            Assert.That(fabricated.Succeeded, Is.False);
            Assert.That(fixture.Properties.OwnershipInterests.Single().owner.kind, Is.EqualTo(PropertySubjectKind.Organization));
            Assert.That(fixture.Businesses.OwnershipRecords.Single().owner.kind, Is.EqualTo(BusinessOwnerSubjectKind.Organization));
        }

        [Test]
        public void CustodyRecordsNeverMutateStep9ItemOwnership()
        {
            Fixture fixture = Fixture.Create();
            string itemId = fixture.Items.CreateItem(fixture.Item, itemInstanceId: "6d909342-b419-4de6-9772-df047fe2a0c4", ownerPersonId: "person.prototype.friend", custodianPersonId: "person.prototype.friend").Snapshot.ItemInstanceId;

            OrganizationResourceOperationResult assigned = fixture.Resources.AssignCustody(new OrganizationCustodyRequest
            {
                transactionId = "tx.resources.custody",
                custodyId = "organization-custody.test.sword",
                organizationId = GuildId,
                asset = new OrganizationAssetReferenceData { kind = OrganizationAssetReferenceKind.ItemInstance, resourceId = itemId, definitionId = fixture.Item.Id, worldId = PersistenceService.LocalWorldId },
                custodianPersonId = "person.prototype.student",
                actorPersonId = ActorId,
                sourceOperationId = "checkout.test.sword",
                startWorldTime = 10d
            });
            OrganizationResourceOperationResult returned = fixture.Resources.ReturnCustody("organization-custody.test.sword", "tx.resources.custody.return", ActorId, 20d);

            Assert.That(assigned.Succeeded, Is.True, assigned.Message);
            Assert.That(returned.Succeeded, Is.True, returned.Message);
            Assert.That(fixture.Items.TryGetSnapshot(itemId, out ItemInstanceSnapshot item), Is.True);
            Assert.That(item.OwnerPersonId, Is.EqualTo("person.prototype.friend"));
            Assert.That(item.CustodianPersonId, Is.EqualTo("person.prototype.friend"));
            Assert.That(fixture.Resources.CustodyRecords.Single().lifecycleState, Is.EqualTo(OrganizationCustodyLifecycleState.Returned));
        }

        [Test]
        public void PersistencePrepareRejectsCrossRuntimeDriftWithoutMutatingLiveState()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(200L);
            OrganizationResourcePersistenceParticipant participant = fixture.Participant();
            PersistenceParticipantSaveResult captured = participant.CapturePayload();
            OrganizationResourceRuntimeSaveData corrupt = JsonUtility.FromJson<OrganizationResourceRuntimeSaveData>(captured.PayloadJson);
            corrupt.accounts[0].currencyDefinitionId = "currency.missing";
            long revisionBefore = fixture.Resources.Revision;
            long balanceBefore = fixture.Resources.GetBalance("organization-account.test.operating", 10d).BalanceUnits;

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), OrganizationResourcePersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(captured.Succeeded, Is.True, captured.Message);
            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Resources.Revision, Is.EqualTo(revisionBefore));
            Assert.That(fixture.Resources.GetBalance("organization-account.test.operating", 10d).BalanceUnits, Is.EqualTo(balanceBefore));
            Assert.That(fixture.Resources.AccountCount, Is.EqualTo(2));
        }

        [Test]
        public void SnapshotsProjectionsAndRoundTripRemainImmutableAndDoNotReplayMoney()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(300L);
            OrganizationResourceRuntimeSaveData before = fixture.Resources.CreateSaveData();
            OrganizationResourceProjection redacted = fixture.Resources.ProjectAccount("organization-account.test.operating", OrganizationResourceProjectionAccess.Redacted, 10d);
            OrganizationResourceProjection full = fixture.Resources.ProjectAccount("organization-account.test.operating", OrganizationResourceProjectionAccess.Full, 10d);
            long economyRevision = fixture.Economy.Revision;
            before.accounts[0].officialName = "mutated snapshot";
            OrganizationResourcePersistenceParticipant participant = fixture.Participant();
            PersistenceParticipantSaveResult save = participant.CapturePayload();
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(save.PayloadJson, participant.ParticipantSchemaVersion);
            PersistenceParticipantCommitResult restored = participant.CommitPreparedPayload(prepared.PreparedPayload);

            Assert.That(redacted.Redacted, Is.True);
            Assert.That(redacted.Balance.BalanceUnits, Is.Zero);
            Assert.That(full.Balance.BalanceUnits, Is.EqualTo(300L));
            Assert.That(fixture.Resources.Accounts.First().officialName, Is.Not.EqualTo("mutated snapshot"));
            Assert.That(restored.Succeeded, Is.True, restored.Message);
            Assert.That(fixture.Economy.Revision, Is.EqualTo(economyRevision), "Resource metadata restore must not replay Economy mutations.");
            Assert.That(fixture.Resources.GetBalance("organization-account.test.operating", 10d).BalanceUnits, Is.EqualTo(300L));
        }

        [Test]
        public void StableTransactionsRejectAliasReplaysAndPublishOnlyAfterCommit()
        {
            Fixture fixture = Fixture.Create();
            int events = 0;
            fixture.Resources.OperationCommitted += _ => events++;
            fixture.CreateTreasuryAndAccounts(100L);
            int setupEvents = events;

            OrganizationResourceOperationResult committed = fixture.Resources.TransferFunds(fixture.Transfer("tx.resources.event", 10L));
            OrganizationResourceOperationResult duplicate = fixture.Resources.TransferFunds(fixture.Transfer("tx.resources.event", 10L));
            OrganizationResourceOperationResult alias = fixture.Resources.TransferFunds(fixture.Transfer("tx.resources.alias", 10L));
            OrganizationResourceOperationResult conflictingTreasury = fixture.Resources.CreateTreasury(new OrganizationTreasuryRequest
            {
                transactionId = "tx.resources.treasury.alias", treasuryId = "organization-treasury.test.guild", organizationId = GuildId,
                resourceTypeDefinitionId = PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId, officialName = "Guild Treasury", actorPersonId = ActorId, worldTime = 12d
            });

            Assert.That(committed.Succeeded, Is.True, committed.Message);
            Assert.That(duplicate.Duplicate, Is.True, duplicate.Message);
            Assert.That(alias.Succeeded, Is.True, "A distinct financial transfer is a valid new operation, not a stable-record alias.");
            Assert.That(conflictingTreasury.Code, Is.EqualTo(OrganizationResourceOperationCode.InvalidRequest));
            Assert.That(events, Is.EqualTo(setupEvents + 2), "Preview, duplicate, and rejected operations must not publish committed events.");
        }

        [Test]
        public void RevenueRoutingMovesRealEconomyFundsDeterministically()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(1000L);
            OrganizationResourceOperationResult rule = fixture.Resources.CreateRevenueRoutingRule(new OrganizationRevenueRoutingRequest
            {
                transactionId = "tx.resources.route.rule", routingRuleId = "organization-routing.test.reserve", organizationId = GuildId,
                revenueSourceId = "business-revenue.test.guild", destinationAccountId = "organization-account.test.reserve",
                percentageBasisPoints = 2500L, priority = 10, purpose = "reserve allocation", actorPersonId = ActorId, startWorldTime = 10d
            });
            OrganizationResourceOperationResult routed = fixture.Resources.ApplyRevenueRouting(new OrganizationRevenueRoutingExecutionRequest
            {
                transactionId = "tx.resources.route.execute", organizationId = GuildId, revenueSourceId = "business-revenue.test.guild",
                sourceAccountId = "organization-account.test.operating", currencyDefinitionId = fixture.Currency.Id, grossUnits = 400L,
                actorPersonId = ActorId, worldTime = 20d
            });

            Assert.That(rule.Succeeded, Is.True, rule.Message);
            Assert.That(routed.Succeeded, Is.True, routed.Message);
            Assert.That(fixture.Resources.GetBalance("organization-account.test.operating", 20d).BalanceUnits, Is.EqualTo(900L));
            Assert.That(fixture.Resources.GetBalance("organization-account.test.reserve", 20d).BalanceUnits, Is.EqualTo(100L));
        }

        [Test]
        public void DissolutionPlanFreezesAccountsWithoutInventingBeneficiariesAndPersists()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(250L);
            OrganizationResourceOperationResult created = fixture.Resources.CreateDissolutionResourcePlan(new OrganizationDissolutionResourcePlanRequest
            {
                transactionId = "tx.resources.dissolution.create", planId = "organization-dissolution-plan.test.guild", organizationId = GuildId,
                accountIdsToFreeze = new[] { "organization-account.test.operating", "organization-account.test.reserve" },
                preservedObligationIds = new[] { "contract-obligation.test.unresolved" }, actorPersonId = ActorId, worldTime = 30d
            });
            OrganizationResourceOperationResult executed = fixture.Resources.ExecuteDissolutionResourcePlan("organization-dissolution-plan.test.guild", "tx.resources.dissolution.execute", ActorId, Array.Empty<string>(), 31d);
            OrganizationResourceRuntimeSaveData save = fixture.Resources.CreateSaveData();

            Assert.That(created.Succeeded, Is.True, created.Message);
            Assert.That(executed.Succeeded, Is.True, executed.Message);
            Assert.That(fixture.Resources.Accounts.All(item => item.lifecycleState == OrganizationAccountLifecycleState.Frozen), Is.True);
            Assert.That(save.dissolutionPlans.Single().preservedObligationIds, Does.Contain("contract-obligation.test.unresolved"));
            Assert.That(save.dissolutionPlans.Single().assetInstructions, Is.Empty);
            Assert.That(OrganizationResourceRuntime.ValidateSaveData(save, fixture.Registry, fixture.Organizations, fixture.Economy, PersistenceService.LocalWorldId, fixture.Properties, fixture.Businesses, fixture.Items, out string failure), Is.True, failure);
        }

        [Test]
        public void ConsolidatedAndValuationViewsAreReadOnlyAndDoNotFabricateAssetPrices()
        {
            Fixture fixture = Fixture.Create();
            fixture.CreateTreasuryAndAccounts(500L);
            fixture.CreateOwnedPropertyAndBusiness();
            long revision = fixture.Resources.Revision;
            fixture.Resources.AssociateProperty(new OrganizationAssociationRequest { transactionId = "tx.resources.valuation.property", associationId = "association.valuation.property", organizationId = GuildId, resourceId = "property.test.guild-hall", sourceRecordId = "property-ownership.test.guild-hall", category = (int)OrganizationPropertyAssociationCategory.Owner, actorPersonId = ActorId, startWorldTime = 10d });
            fixture.Resources.AssociateBusiness(new OrganizationAssociationRequest { transactionId = "tx.resources.valuation.business", associationId = "association.valuation.business", organizationId = GuildId, resourceId = "business.test.guild-shop", sourceRecordId = "business-ownership.test.guild-shop", category = (int)OrganizationBusinessAssociationCategory.Owner, shareBasisPoints = 10000L, actorPersonId = ActorId, startWorldTime = 10d });
            long queryRevision = fixture.Resources.Revision;

            OrganizationResourceValuationSnapshot valuation = fixture.Resources.GetKnownValuation(GuildId, fixture.Currency.Id, 10d);
            OrganizationConsolidatedResourceSnapshot consolidated = fixture.Resources.GetConsolidatedView(GuildId, 10d);

            Assert.That(revision, Is.LessThan(queryRevision));
            Assert.That(valuation.CashUnits, Is.EqualTo(500L));
            Assert.That(valuation.UnvaluedAssetIds, Is.EquivalentTo(new[] { "property.test.guild-hall", "business.test.guild-shop" }));
            Assert.That(consolidated.OrganizationIds, Does.Contain(GuildId));
            Assert.That(consolidated.Total(fixture.Currency.Id), Is.EqualTo(500L));
            Assert.That(fixture.Resources.Revision, Is.EqualTo(queryRevision));
        }

        [Test]
        public void ResetAndDisposeClearOwnedStateAndDependencyReadiness()
        {
            Fixture fixture = Fixture.Create(); fixture.CreateTreasuryAndAccounts(10L);
            fixture.Resources.Reset();
            Assert.That(fixture.Resources.IsReady, Is.True);
            Assert.That(fixture.Resources.TreasuryCount, Is.Zero);
            Assert.That(fixture.Resources.Revision, Is.Zero);
            fixture.Resources.Dispose();
            Assert.That(fixture.Resources.IsReady, Is.False);
            Assert.That(fixture.Resources.CreateTreasury(new OrganizationTreasuryRequest()).Code, Is.EqualTo(OrganizationResourceOperationCode.Disposed));
        }

        private sealed class Fixture
        {
            private static readonly string[] Persons = { ActorId, "person.prototype.friend", "person.prototype.student" };

            private Fixture(DefinitionRegistry registry, CurrencyDefinition currency, PropertyDefinition propertyDefinition, BusinessDefinition businessDefinition, ItemDefinition item)
            {
                Registry = registry;
                Currency = currency;
                PropertyDefinition = propertyDefinition;
                BusinessDefinition = businessDefinition;
                Item = item;
                Organizations = new OrganizationRuntime();
                PrototypeOrganizationDefinitionFactory.SeedPrototypeOrganizations(Organizations, registry, PersistenceService.LocalWorldId);
                Organizations.Configure(registry, PersistenceService.LocalWorldId, Persons, Array.Empty<string>());
                Memberships = new OrganizationMembershipRuntime();
                Memberships.Configure(registry, Organizations, PersistenceService.LocalWorldId, Persons, Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
                Authority = new OrganizationAuthorityRuntime();
                Authority.Configure(registry, Organizations, Memberships, PersistenceService.LocalWorldId, Persons, Organizations.Snapshots.Select(snapshot => snapshot.OrganizationId));
                Economy = new EconomyRuntime();
                Economy.Configure(registry, PersistenceService.LocalWorldId);
                Properties = new PropertyRuntime();
                Properties.Configure(registry, PersistenceService.LocalWorldId);
                Businesses = new BusinessRuntime();
                Businesses.Configure(registry, PersistenceService.LocalWorldId);
                Items = new ItemInstanceIdentityRuntime();
                Resources = new OrganizationResourceRuntime();
                Resources.Configure(registry, Organizations, Authority, Economy, PersistenceService.LocalWorldId, Properties, Businesses, Items);
                CreateGuildmaster();
            }

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Currency { get; }
            public PropertyDefinition PropertyDefinition { get; }
            public BusinessDefinition BusinessDefinition { get; }
            public ItemDefinition Item { get; }
            public OrganizationRuntime Organizations { get; }
            public OrganizationMembershipRuntime Memberships { get; }
            public OrganizationAuthorityRuntime Authority { get; }
            public EconomyRuntime Economy { get; }
            public PropertyRuntime Properties { get; }
            public BusinessRuntime Businesses { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public OrganizationResourceRuntime Resources { get; }

            public static Fixture Create()
            {
                CurrencyDefinition currency = ScriptableObject.CreateInstance<CurrencyDefinition>();
                currency.Initialize("currency.test.organization-gold", "Organization Gold", "OG");
                PropertyDefinition property = ScriptableObject.CreateInstance<PropertyDefinition>();
                property.Initialize("property.test.organization-hall", "Organization Hall", PropertyCategory.CommercialBuilding);
                property.SetPolicies(Array.Empty<PropertyCategory>(), new[] { PropertyOwnershipModel.Sole, PropertyOwnershipModel.SharedFractional }, new[] { PropertyUseCategory.Commercial, PropertyUseCategory.Office }, currency.Id);
                BusinessDefinition business = ScriptableObject.CreateInstance<BusinessDefinition>();
                business.Initialize("business.test.organization-shop", "Organization Shop", BusinessCategory.MerchantShop);
                ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                SetPrivate(item, "itemId", "item.test.organization-sword");
                SetPrivate(item, "displayName", "Organization Sword");
                SetPrivate(item, "stackable", false);
                SetPrivate(item, "maximumStackSize", 1);
                SetPrivate(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
                DefinitionRegistry baseRegistry = new DefinitionRegistry(new IGameDefinition[] { currency, property, business, item });
                DefinitionRegistry registry = PrototypeOrganizationResourceDefinitionFactory.AddMissingPrototypeOrganizationResourceDefinitions(
                    PrototypeOrganizationAuthorityDefinitionFactory.AddMissingPrototypeOrganizationAuthorityDefinitions(
                        PrototypeOrganizationMembershipDefinitionFactory.AddMissingPrototypeOrganizationMembershipDefinitions(
                            PrototypeOrganizationDefinitionFactory.AddMissingPrototypeOrganizationDefinitions(baseRegistry))));
                return new Fixture(registry, currency, property, business, item);
            }

            public void CreateTreasuryAndAccounts(long openingBalance)
            {
                Assert.That(Resources.CreateTreasury(new OrganizationTreasuryRequest
                {
                    transactionId = "tx.resources.treasury",
                    treasuryId = "organization-treasury.test.guild",
                    organizationId = GuildId,
                    resourceTypeDefinitionId = PrototypeOrganizationResourceDefinitionFactory.CurrencyResourceTypeId,
                    officialName = "Guild Treasury",
                    actorPersonId = ActorId,
                    worldTime = 1d
                }).Succeeded, Is.True);
                Assert.That(Resources.CreateAccount(Account("tx.resources.account.operating", "organization-account.test.operating", "economy.organization.test.operating", openingBalance)).Succeeded, Is.True);
                Assert.That(Resources.CreateAccount(Account("tx.resources.account.reserve", "organization-account.test.reserve", "economy.organization.test.reserve", 0L)).Succeeded, Is.True);
            }

            public OrganizationFinancialTransactionRequest Transfer(string transactionId, long units, bool preview = false, double worldTime = 10d) => new OrganizationFinancialTransactionRequest
            {
                transactionId = transactionId,
                organizationId = GuildId,
                sourceAccountId = "organization-account.test.operating",
                destinationAccountId = "organization-account.test.reserve",
                currencyDefinitionId = Currency.Id,
                units = units,
                transactionKind = EconomyTransactionKind.Transfer,
                actorPersonId = ActorId,
                purpose = "reserve allocation",
                worldTime = worldTime,
                preview = preview
            };

            public OrganizationAccountLifecycleRequest Lifecycle(string transactionId, OrganizationAccountLifecycleState state, double worldTime) => new OrganizationAccountLifecycleRequest
            {
                transactionId = transactionId,
                accountId = "organization-account.test.operating",
                targetState = state,
                actorPersonId = ActorId,
                worldTime = worldTime
            };

            public void CreateOwnedPropertyAndBusiness()
            {
                Assert.That(Properties.RegisterProperty(new PropertyInstanceData
                {
                    propertyId = "property.test.guild-hall",
                    propertyDefinitionId = PropertyDefinition.Id,
                    displayName = "Guild Hall",
                    spatialReferenceId = "place.test.guild-hall",
                    currentUses = new[] { PropertyUseCategory.Office },
                    creationWorldTime = 1d
                }).Succeeded, Is.True);
                Assert.That(Properties.CreateOwnership(new PropertyOwnershipInterestData
                {
                    ownershipInterestId = "property-ownership.test.guild-hall",
                    propertyId = "property.test.guild-hall",
                    owner = new PropertySubjectReferenceData { kind = PropertySubjectKind.Organization, subjectId = GuildId },
                    ownershipModel = PropertyOwnershipModel.Sole,
                    ownershipShare = PropertyShareData.Full(),
                    votingShare = PropertyShareData.Full(),
                    economicBenefitShare = PropertyShareData.Full(),
                    effectiveStartWorldTime = 1d
                }, 1d).Succeeded, Is.True);
                Assert.That(Businesses.CreateBusiness(new BusinessInstanceData
                {
                    businessId = "business.test.guild-shop",
                    businessDefinitionId = BusinessDefinition.Id,
                    displayName = "Guild Shop",
                    linkedOrganizationId = GuildId,
                    founderSubjectIds = new[] { ActorId },
                    operatingCurrencyIds = new[] { Currency.Id },
                    state = BusinessState.Active,
                    createdWorldTime = 1d
                }).Succeeded, Is.True);
                Assert.That(Businesses.AddOwnership(new BusinessOwnershipRecordData
                {
                    ownershipRecordId = "business-ownership.test.guild-shop",
                    businessId = "business.test.guild-shop",
                    owner = new BusinessSubjectReferenceData { kind = BusinessOwnerSubjectKind.Organization, subjectId = GuildId },
                    category = BusinessOwnershipCategory.SoleOwner,
                    economicShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                    votingShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                    effectiveStartWorldTime = 1d
                }, 1d).Succeeded, Is.True);
            }

            public OrganizationResourcePersistenceParticipant Participant() => new OrganizationResourcePersistenceParticipant(Resources, () => Registry, () => Organizations, () => Authority, () => Economy, PersistenceService.LocalWorldId, () => Properties, () => Businesses, () => Items);

            private OrganizationAccountRequest Account(string transactionId, string accountId, string economyAccountId, long openingBalance) => new OrganizationAccountRequest
            {
                transactionId = transactionId,
                accountId = accountId,
                treasuryId = "organization-treasury.test.guild",
                organizationId = GuildId,
                economyAccountId = economyAccountId,
                officialName = accountId,
                currencyDefinitionId = Currency.Id,
                openingBalanceUnits = openingBalance,
                actorPersonId = ActorId,
                worldTime = 2d
            };

            private void CreateGuildmaster()
            {
                OrganizationMembershipOperationResult member = Memberships.ApplyMembership(new OrganizationMembershipRequest
                {
                    membershipId = "organization-membership.test.resources.guildmaster",
                    organizationId = GuildId,
                    personId = ActorId,
                    membershipDefinitionId = PrototypeOrganizationMembershipDefinitionFactory.GuildFullMemberId,
                    targetStatus = OrganizationMembershipStatus.Active,
                    sourceKind = OrganizationMembershipSourceKind.WorldSetup,
                    explicitConsent = true,
                    worldTime = 0d,
                    transactionId = "tx.resources.guildmaster"
                });
                string[] ranks = PrototypeOrganizationMembershipDefinitionFactory.GuildRatingRankIds.ToArray();
                for (int index = 0; index < ranks.Length; index++) Memberships.AssignRank(new OrganizationRankAssignmentRequest
                {
                    rankAssignmentId = $"organization-rank-assignment.test.resources.{index}", membershipId = member.Membership.MembershipId, rankDefinitionId = ranks[index], worldTime = index + 1d, assignedById = ActorId, transactionId = $"tx.resources.rank.{index}"
                });
                OrganizationMembershipOperationResult office = Memberships.CreateOffice(new OrganizationOfficeRequest
                {
                    officeId = "organization-office-record.test.resources.guildmaster", organizationId = GuildId, officeDefinitionId = PrototypeOrganizationMembershipDefinitionFactory.GuildmasterOfficeId, worldTime = 4d, transactionId = "tx.resources.office"
                });
                Memberships.AssignOffice(new OrganizationOfficeAssignmentRequest
                {
                    officeAssignmentId = "organization-office-assignment.test.resources.guildmaster", officeId = office.Office.OfficeId, membershipId = member.Membership.MembershipId, worldTime = 5d, appointedById = ActorId, transactionId = "tx.resources.office.assign"
                });
            }

            private static void SetPrivate(object target, string fieldName, object value) => target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }
    }
}
#endif
