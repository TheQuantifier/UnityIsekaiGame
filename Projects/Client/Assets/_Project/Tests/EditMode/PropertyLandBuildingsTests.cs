using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.Economy.Properties;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class PropertyLandBuildingsTests
    {
        [Test]
        public void PropertyDefinitionsHierarchyAndSnapshotsAreValidatedAndImmutable()
        {
            Fixture fixture = Fixture.Create();
            DefinitionValidationReport report = new DefinitionValidationReport();
            fixture.LandDefinition.ValidateCatalogDefinition(fixture.Registry.DefinitionsById, report);

            PropertyOperationResult land = fixture.RegisterLand("land");
            PropertyOperationResult building = fixture.RegisterBuilding("building", fixture.PropertyId("land"));
            PropertyOperationResult unit = fixture.RegisterUnit("unit", fixture.PropertyId("building"));
            PropertyOperationResult cycle = fixture.Properties.RegisterProperty(new PropertyInstanceData
            {
                propertyId = fixture.PropertyId("cycle"),
                propertyDefinitionId = fixture.UnitDefinition.Id,
                parentPropertyId = fixture.PropertyId("unit")
            });
            PropertyInstanceData snapshot = fixture.Properties.Properties.Single(item => item.propertyId == fixture.PropertyId("land"));
            snapshot.childPropertyIds = new[] { "tamper" };

            Assert.That(report.HasErrors, Is.False, report.ToString());
            Assert.That(land.Succeeded, Is.True, land.Message);
            Assert.That(building.Succeeded, Is.True, building.Message);
            Assert.That(unit.Succeeded, Is.True, unit.Message);
            Assert.That(cycle.Succeeded, Is.False, "A unit definition is not allowed beneath another unit.");
            Assert.That(fixture.Properties.TryGetProperty(fixture.PropertyId("land"), out PropertyInstanceData live), Is.True);
            Assert.That(live.childPropertyIds, Does.Contain(fixture.PropertyId("building")));
            Assert.That(live.childPropertyIds, Does.Not.Contain("tamper"));
        }

        [Test]
        public void OwnershipTitleAndSharesRemainSeparateFromPossession()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareTitledProperty("home");

            PropertyOperationResult possession = fixture.Properties.BeginPossession(new PropertyPossessionRecordData
            {
                possessionId = "possession.home.tenant",
                propertyId = fixture.PropertyId("home"),
                possessor = PropertySubjectReferenceData.Person("person.tenant"),
                category = PossessionCategory.TenantPossession,
                startWorldTime = 2d,
                exclusive = true
            });
            PropertyOperationResult splitOwner = fixture.Properties.CreateOwnership(new PropertyOwnershipInterestData
            {
                ownershipInterestId = "ownership.home.partner",
                propertyId = fixture.PropertyId("home"),
                owner = PropertySubjectReferenceData.Person("person.partner"),
                ownershipModel = PropertyOwnershipModel.SharedFractional,
                ownershipShare = new PropertyShareData { units = 5000L, totalUnits = 10000L },
                effectiveStartWorldTime = 3d
            }, 3d);
            PropertyOperationResult invalidTitle = fixture.Properties.CreateTitle("title.home.invalid", fixture.PropertyId("home"), new[] { "ownership.home.partner" }, 3d);

            Assert.That(possession.Succeeded, Is.True, possession.Message);
            Assert.That(splitOwner.Succeeded, Is.True, splitOwner.Message);
            Assert.That(invalidTitle.Succeeded, Is.False, "Title must represent exact active ownership shares.");
            Assert.That(fixture.Properties.OwnershipInterests.Count(item => item.propertyId == fixture.PropertyId("home") && item.owner.subjectId == "person.tenant"), Is.Zero);
            Assert.That(fixture.Properties.Possessions.Single().possessor.subjectId, Is.EqualTo("person.tenant"));
        }

        [Test]
        public void TenancyCreatesOccupancyPossessionAccessAndRentWithoutOwnershipTransfer()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareTitledProperty("rental");
            fixture.CreateAccounts("landlord", "tenant", tenantBalance: 80L);

            PropertyOperationResult tenancy = fixture.CreateTenancy("rental", "lease", 10L);
            PropertyOperationResult activate = fixture.Properties.ActivateTenancy("tenancy.lease", 1d);
            PropertyOperationResult rent = fixture.Properties.GenerateRentObligation("rent.lease.1", "tenancy.lease", 1d, 31d, 32d);
            PropertyOperationResult partial = fixture.Properties.PayRent("rent.lease.1", fixture.Economy, "tx.rent.partial", 6L, 10d);
            PropertyOperationResult overdue = fixture.Properties.MarkOverdueRent("rent.lease.1", 40d);
            PropertyAccessEvaluationResult access = fixture.Properties.EvaluateAccess(fixture.PropertyId("rental"), PropertySubjectReferenceData.Person("person.tenant"), PropertyAccessCategory.Enter, 2d);

            Assert.That(tenancy.Succeeded, Is.True, tenancy.Message);
            Assert.That(activate.Succeeded, Is.True, activate.Message);
            Assert.That(rent.Succeeded, Is.True, rent.Message);
            Assert.That(partial.Succeeded, Is.True, partial.Message);
            Assert.That(overdue.Succeeded, Is.True, overdue.Message);
            Assert.That(access.Allowed, Is.True, access.Message);
            Assert.That(fixture.Properties.RentObligations.Single().state, Is.EqualTo(RentObligationState.Overdue));
            Assert.That(fixture.Properties.RentObligations.Single().OutstandingUnits, Is.EqualTo(4L));
            Assert.That(fixture.Properties.OwnershipInterests.Any(item => item.owner.subjectId == "person.tenant"), Is.False);
        }

        [Test]
        public void SalesGiftsInheritanceAndInjectedFailuresPreserveAtomicState()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareTitledProperty("farm");
            fixture.CreateAccounts("seller", "buyer", tenantBalance: 0L, buyerBalance: 100L);

            PropertyOperationResult failed = fixture.Transfer("farm", "sale-fail", PropertyTransferCategory.Sale, "person.owner", "person.buyer", 4000L, inject: "title-creation");
            fixture.Economy.TryGetAccount("account.buyer", out EconomyAccountSnapshot buyerAfterFailure);
            fixture.Economy.TryGetAccount("account.seller", out EconomyAccountSnapshot sellerAfterFailure);
            PropertyOperationResult sale = fixture.Transfer("farm", "sale", PropertyTransferCategory.Sale, "person.owner", "person.buyer", 4000L);
            PropertyOperationResult gift = fixture.Transfer("farm", "gift", PropertyTransferCategory.Gift, "person.buyer", "person.recipient", 1000L);
            PropertyOperationResult inheritance = fixture.Transfer("farm", "inherit", PropertyTransferCategory.Inheritance, "person.recipient", "person.heir", 1000L);

            Assert.That(failed.Succeeded, Is.False);
            Assert.That(buyerAfterFailure.BalanceUnits, Is.EqualTo(100L));
            Assert.That(sellerAfterFailure.BalanceUnits, Is.EqualTo(0L));
            Assert.That(sale.Succeeded, Is.True, sale.Message);
            Assert.That(gift.Succeeded, Is.True, gift.Message);
            Assert.That(inheritance.Succeeded, Is.True, inheritance.Message);
            Assert.That(fixture.Properties.Records.Any(item => item.category == PropertyRecordCategory.InheritanceRecord), Is.True);
            Assert.That(fixture.Properties.OwnershipInterests.Any(item => item.owner.subjectId == "person.heir" && item.IsActiveAt(8d)), Is.True);
        }

        [Test]
        public void MaintenanceInspectionsBusinessPremisesAndInventoryBoundariesRemainReferences()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareTitledProperty("shop");
            fixture.PrepareBusiness("shop");
            string hammerId = "22222222-2222-2222-2222-222222222222";
            fixture.Items.CreateItem(fixture.ToolDefinition, itemInstanceId: hammerId, ownerPersonId: "person.worker", custodianPersonId: "person.worker");

            PropertyOperationResult business = fixture.Properties.LinkBusinessEstablishment(fixture.PropertyId("shop"), "establishment.shop", fixture.Businesses);
            PropertyOperationResult condition = fixture.Properties.RecordCondition(new PropertyConditionRecordData { conditionRecordId = "condition.shop.damaged", propertyId = fixture.PropertyId("shop"), condition = PropertyConditionState.Damaged, severity = 4, recordedWorldTime = 5d });
            PropertyOperationResult inspection = fixture.Properties.PerformInspection(new PropertyInspectionRecordData
            {
                inspectionId = "inspection.shop",
                propertyId = fixture.PropertyId("shop"),
                inspector = PropertySubjectReferenceData.Person("person.inspector"),
                inspectedWorldTime = 6d
            });
            PropertyOperationResult obligation = fixture.Properties.CreateMaintenanceObligation(new PropertyMaintenanceObligationData
            {
                maintenanceObligationId = "maintenance.shop",
                propertyId = fixture.PropertyId("shop"),
                responsibleSubject = PropertySubjectReferenceData.Person("person.owner"),
                authorizedWorker = PropertySubjectReferenceData.Person("person.worker"),
                requiredToolItemInstanceIds = new[] { hammerId },
                dueWorldTime = 10d
            });
            PropertyOperationResult failedRepair = fixture.Properties.ExecuteMaintenance("maintenance.shop", PropertySubjectReferenceData.Person("person.worker"), fixture.Items, new[] { hammerId }, System.Array.Empty<string>(), string.Empty, string.Empty, 7d, injectFailureStage: "after-tool-validation");
            PropertyOperationResult repair = fixture.Properties.ExecuteMaintenance("maintenance.shop", PropertySubjectReferenceData.Person("person.worker"), fixture.Items, new[] { hammerId }, System.Array.Empty<string>(), string.Empty, string.Empty, 8d);

            Assert.That(business.Succeeded, Is.True, business.Message);
            Assert.That(condition.Succeeded, Is.True, condition.Message);
            Assert.That(inspection.Succeeded, Is.True, inspection.Message);
            Assert.That(obligation.Succeeded, Is.True, obligation.Message);
            Assert.That(failedRepair.Succeeded, Is.False);
            Assert.That(repair.Succeeded, Is.True, repair.Message);
            Assert.That(fixture.Items.TryGetSnapshot(hammerId, out ItemInstanceSnapshot tool), Is.True);
            Assert.That(tool.OwnerPersonId, Is.EqualTo("person.worker"));
            Assert.That(fixture.Properties.MaintenanceObligations.Single().state, Is.EqualTo(MaintenanceObligationState.Completed));
        }

        [Test]
        public void PropertyPersistenceRejectsBrokenGraphBeforeCommitAndRestoresValidState()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareTitledProperty("persist");
            PropertyPersistenceParticipant participant = new PropertyPersistenceParticipant(fixture.Properties, () => fixture.Registry);
            PropertyRuntimeSaveData saved = fixture.Properties.CreateSaveData();
            PropertyRuntimeSaveData corrupt = saved.Clone();
            corrupt.titles[0].propertyId = "property.missing";

            PersistenceParticipantPrepareResult rejected = participant.PreparePayload(JsonUtility.ToJson(corrupt), PropertyPersistenceParticipant.CurrentParticipantSchemaVersion);
            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(saved), PropertyPersistenceParticipant.CurrentParticipantSchemaVersion);
            PropertyRuntime restored = new PropertyRuntime();
            restored.Configure(fixture.Registry, PersistenceService.LocalWorldId);
            PropertyOperationResult restore = restored.RestoreFromSaveData(saved, fixture.Registry);

            Assert.That(rejected.Succeeded, Is.False);
            Assert.That(fixture.Properties.TryGetProperty(fixture.PropertyId("persist"), out _), Is.True);
            Assert.That(prepared.Succeeded, Is.True, prepared.Message);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetTitle("title.persist.initial", out PropertyTitleRecordData title), Is.True);
            Assert.That(title.activeOwnershipInterestIds, Does.Contain("ownership.persist.owner"));
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, CurrencyDefinition gold, PropertyDefinition landDefinition, PropertyDefinition buildingDefinition, PropertyDefinition unitDefinition, BusinessDefinition businessDefinition, ItemDefinition toolDefinition)
            {
                Registry = registry;
                Gold = gold;
                LandDefinition = landDefinition;
                BuildingDefinition = buildingDefinition;
                UnitDefinition = unitDefinition;
                BusinessDefinition = businessDefinition;
                ToolDefinition = toolDefinition;
                Properties = new PropertyRuntime();
                Economy = new EconomyRuntime();
                Businesses = new BusinessRuntime();
                Items = new ItemInstanceIdentityRuntime();
                Access = new InformationAccessRuntime();
                Properties.Configure(registry, PersistenceService.LocalWorldId);
                Economy.Configure(registry, PersistenceService.LocalWorldId);
                Businesses.Configure(registry, PersistenceService.LocalWorldId);
            }

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Gold { get; }
            public PropertyDefinition LandDefinition { get; }
            public PropertyDefinition BuildingDefinition { get; }
            public PropertyDefinition UnitDefinition { get; }
            public BusinessDefinition BusinessDefinition { get; }
            public ItemDefinition ToolDefinition { get; }
            public PropertyRuntime Properties { get; }
            public EconomyRuntime Economy { get; }
            public BusinessRuntime Businesses { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public InformationAccessRuntime Access { get; }

            public static Fixture Create()
            {
                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G");
                PropertyDefinition land = PropertyDefinition("property.test.land", "Test Land", PropertyCategory.LandParcel, new[] { PropertyCategory.ResidentialBuilding }, new[] { PropertyUseCategory.Residential, PropertyUseCategory.Commercial, PropertyUseCategory.Agricultural });
                PropertyDefinition building = PropertyDefinition("property.test.building", "Test Building", PropertyCategory.ResidentialBuilding, new[] { PropertyCategory.ApartmentUnit }, new[] { PropertyUseCategory.Residential, PropertyUseCategory.Commercial, PropertyUseCategory.Storage });
                PropertyDefinition unit = PropertyDefinition("property.test.unit", "Test Unit", PropertyCategory.ApartmentUnit, null, new[] { PropertyUseCategory.Residential, PropertyUseCategory.Commercial, PropertyUseCategory.Storage });
                BusinessDefinition business = ScriptableObject.CreateInstance<BusinessDefinition>();
                business.Initialize("business.test.property-shop", "Property Shop", BusinessCategory.MerchantShop);
                ItemDefinition tool = Item("item.test.repair-hammer", "Repair Hammer");
                return new Fixture(new DefinitionRegistry(new IGameDefinition[] { gold, land, building, unit, business, tool }), gold, land, building, unit, business, tool);
            }

            public string PropertyId(string slug) => $"property.test.{slug}";
            public string BusinessId(string slug) => $"business.test.{slug}";

            public PropertyOperationResult RegisterLand(string slug)
            {
                return Properties.RegisterProperty(new PropertyInstanceData
                {
                    propertyId = PropertyId(slug),
                    propertyDefinitionId = LandDefinition.Id,
                    displayName = $"Land {slug}",
                    spatialReferenceId = $"place.{slug}",
                    currentUses = new[] { PropertyUseCategory.Residential },
                    creationWorldTime = 1d
                });
            }

            public PropertyOperationResult RegisterBuilding(string slug, string parentPropertyId)
            {
                return Properties.RegisterProperty(new PropertyInstanceData
                {
                    propertyId = PropertyId(slug),
                    propertyDefinitionId = BuildingDefinition.Id,
                    parentPropertyId = parentPropertyId,
                    sceneObjectReferenceId = $"scene.{slug}",
                    currentUses = new[] { PropertyUseCategory.Residential }
                });
            }

            public PropertyOperationResult RegisterUnit(string slug, string parentPropertyId)
            {
                return Properties.RegisterProperty(new PropertyInstanceData
                {
                    propertyId = PropertyId(slug),
                    propertyDefinitionId = UnitDefinition.Id,
                    parentPropertyId = parentPropertyId,
                    sceneObjectReferenceId = $"scene.{slug}",
                    currentUses = new[] { PropertyUseCategory.Residential }
                });
            }

            public void PrepareTitledProperty(string slug)
            {
                Assert.That(RegisterLand(slug).Succeeded, Is.True);
                PropertyOperationResult ownership = Properties.CreateOwnership(new PropertyOwnershipInterestData
                {
                    ownershipInterestId = $"ownership.{slug}.owner",
                    propertyId = PropertyId(slug),
                    owner = PropertySubjectReferenceData.Person("person.owner"),
                    ownershipModel = PropertyOwnershipModel.Sole,
                    ownershipShare = PropertyShareData.Full(),
                    votingShare = PropertyShareData.Full(),
                    economicBenefitShare = PropertyShareData.Full(),
                    effectiveStartWorldTime = 1d,
                    rights = new[] { PropertyAccessCategory.Manage, PropertyAccessCategory.TransferProperty }
                }, 1d);
                Assert.That(ownership.Succeeded, Is.True, ownership.Message);
                PropertyOperationResult title = Properties.CreateTitle($"title.{slug}.initial", PropertyId(slug), new[] { $"ownership.{slug}.owner" }, 1d);
                Assert.That(title.Succeeded, Is.True, title.Message);
            }

            public PropertyOperationResult CreateTenancy(string propertySlug, string tenancySlug, long rentUnits)
            {
                return Properties.CreateTenancy(new PropertyTenancyAgreementData
                {
                    tenancyId = $"tenancy.{tenancySlug}",
                    propertyId = PropertyId(propertySlug),
                    landlord = PropertySubjectReferenceData.Person("person.owner"),
                    tenant = PropertySubjectReferenceData.Person("person.tenant"),
                    propertyOwnerInterestIds = new[] { $"ownership.{propertySlug}.owner" },
                    permittedUse = PropertyUseCategory.Residential,
                    startWorldTime = 1d,
                    endWorldTime = 90d,
                    landlordAccountId = "account.landlord",
                    tenantAccountId = "account.tenant",
                    rentTerms = new PropertyRentTermsData { currencyId = Gold.Id, rentUnitsPerPeriod = rentUnits, depositUnits = 3L, periodLengthWorldTime = 30d },
                    grantedAccessCategories = new[] { PropertyAccessCategory.Enter, PropertyAccessCategory.Occupy, PropertyAccessCategory.StoreItems }
                });
            }

            public void CreateAccounts(string sellerSlug, string tenantSlug, long tenantBalance = 0L, long buyerBalance = 0L)
            {
                Economy.CreateAccount("account.landlord", Gold, "person.owner", EconomyAccountKind.PersonWallet, 0L, "tx.account.landlord");
                Economy.CreateAccount("account.tenant", Gold, "person.tenant", EconomyAccountKind.PersonWallet, tenantBalance, "tx.account.tenant");
                Economy.CreateAccount("account.seller", Gold, $"person.{sellerSlug}", EconomyAccountKind.PersonWallet, 0L, "tx.account.seller");
                Economy.CreateAccount("account.buyer", Gold, $"person.{tenantSlug}", EconomyAccountKind.PersonWallet, buyerBalance, "tx.account.buyer");
            }

            public PropertyOperationResult Transfer(string propertySlug, string transferSlug, PropertyTransferCategory category, string fromPerson, string toPerson, long shareUnits, string inject = "")
            {
                return Properties.TransferProperty(new PropertyTransferRequestData
                {
                    transferId = $"transfer.{transferSlug}",
                    propertyId = PropertyId(propertySlug),
                    transferCategory = category,
                    fromOwner = PropertySubjectReferenceData.Person(fromPerson),
                    toOwner = PropertySubjectReferenceData.Person(toPerson),
                    share = new PropertyShareData { units = shareUnits, totalUnits = 10000L },
                    buyerAccountId = "account.buyer",
                    sellerAccountId = "account.seller",
                    currencyId = Gold.Id,
                    considerationUnits = category == PropertyTransferCategory.Sale ? 40L : 0L,
                    effectiveWorldTime = 5d + Properties.Transfers.Count,
                    approvalAuthorityId = "authority.test.registry",
                    injectFailureStage = inject
                }, Economy);
            }

            public void PrepareBusiness(string slug)
            {
                BusinessOperationResult create = Businesses.CreateBusiness(new BusinessInstanceData
                {
                    businessId = BusinessId(slug),
                    businessDefinitionId = BusinessDefinition.Id,
                    displayName = $"Business {slug}",
                    founderSubjectIds = new[] { "person.owner" },
                    operatingCurrencyIds = new[] { Gold.Id },
                    state = BusinessState.Active
                });
                BusinessOperationResult establishment = Businesses.AddEstablishment(new BusinessEstablishmentData
                {
                    establishmentId = $"establishment.{slug}",
                    businessId = BusinessId(slug),
                    type = BusinessEstablishmentType.Shop,
                    state = BusinessEstablishmentState.Open
                });
                Assert.That(create.Succeeded, Is.True, create.Message);
                Assert.That(establishment.Succeeded, Is.True, establishment.Message);
            }

            private static PropertyDefinition PropertyDefinition(string id, string display, PropertyCategory category, PropertyCategory[] children, PropertyUseCategory[] uses)
            {
                PropertyDefinition definition = ScriptableObject.CreateInstance<PropertyDefinition>();
                definition.Initialize(id, display, category);
                definition.SetPolicies(children ?? System.Array.Empty<PropertyCategory>(), new[] { PropertyOwnershipModel.Sole, PropertyOwnershipModel.SharedFractional, PropertyOwnershipModel.Business }, uses, "currency.gold");
                return definition;
            }

            private static ItemDefinition Item(string id, string display)
            {
                ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                SetPrivate(item, "itemId", id);
                SetPrivate(item, "displayName", display);
                SetPrivate(item, "stackable", false);
                SetPrivate(item, "maximumStackSize", 1);
                SetPrivate(item, "instanceMode", ItemInstanceMode.AlwaysInstanced);
                return item;
            }

            private static void SetPrivate(object target, string fieldName, object value)
            {
                target.GetType().GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.SetValue(target, value);
            }
        }
    }
}
