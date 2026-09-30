using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.Businesses;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Inventory.Production;
using UnityIsekaiGame.Inventory.Recipes;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class BusinessesProductionOwnershipTests
    {
        [Test]
        public void BusinessDefinitionsValidateAndLifecycleSeparatesOwnershipFromControl()
        {
            Fixture fixture = Fixture.Create();
            DefinitionValidationReport report = new DefinitionValidationReport();
            fixture.BusinessDefinition.ValidateCatalogDefinition(fixture.Registry.DefinitionsById, report);

            BusinessOperationResult create = fixture.CreateBusiness("shop");
            BusinessOperationResult owner = fixture.AddOwner("shop", "person.owner");
            BusinessOperationResult controller = fixture.Businesses.AssignController(new BusinessControlRecordData
            {
                controlRecordId = "business-control.shop-manager",
                businessId = fixture.BusinessId("shop"),
                controllerSubjectId = "position.shop-manager",
                authorityKinds = new[] { BusinessAuthorityKind.ManageInventory, BusinessAuthorityKind.SellStock },
                effectiveStartWorldTime = 1d
            }, 1d);
            BusinessOperationResult active = fixture.Businesses.TransitionBusiness(fixture.BusinessId("shop"), BusinessState.Active, 2d);

            Assert.That(report.HasErrors, Is.False, report.ToString());
            Assert.That(create.Succeeded, Is.True, create.Message);
            Assert.That(owner.Succeeded, Is.True, owner.Message);
            Assert.That(controller.Succeeded, Is.True, controller.Message);
            Assert.That(active.Succeeded, Is.True, active.Message);
            Assert.That(fixture.Businesses.TryGetBusiness(fixture.BusinessId("shop"), out BusinessInstanceData business), Is.True);
            Assert.That(business.state, Is.EqualTo(BusinessState.Active));
            Assert.That(business.controllerSubjectId, Is.EqualTo("position.shop-manager"));
            Assert.That(fixture.Businesses.OwnershipRecords.Single().owner.subjectId, Is.EqualTo("person.owner"));
        }

        [Test]
        public void EstablishmentsAccountsInventoriesAndStockDoNotMutateItemOwnership()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareActiveBusiness("stock");
            fixture.Economy.CreateAccount("account.business.stock", fixture.Gold, fixture.BusinessId("stock"), EconomyAccountKind.OrganizationAccount, 100L, "tx.account.stock");
            string itemId = "11111111-1111-1111-1111-111111111111";
            ItemInstanceOperationResult item = fixture.Items.CreateItem(fixture.Sword, itemInstanceId: itemId, ownerPersonId: "person.owner", custodianPersonId: "person.custodian");

            BusinessOperationResult establishment = fixture.Businesses.AddEstablishment(new BusinessEstablishmentData
            {
                establishmentId = "business-establishment.stock",
                businessId = fixture.BusinessId("stock"),
                type = BusinessEstablishmentType.Stall,
                state = BusinessEstablishmentState.Open,
                openedWorldTime = 2d
            });
            BusinessOperationResult account = fixture.Businesses.AssignAccount(new BusinessAccountAssignmentData
            {
                assignmentId = "business-account.stock",
                businessId = fixture.BusinessId("stock"),
                accountId = "account.business.stock",
                establishmentId = "business-establishment.stock",
                purpose = BusinessAccountPurpose.OperatingFunds,
                effectiveStartWorldTime = 2d
            }, fixture.Economy);
            BusinessOperationResult inventory = fixture.Businesses.AssignInventory(new BusinessInventoryAssignmentData
            {
                assignmentId = "business-inventory.stock",
                businessId = fixture.BusinessId("stock"),
                inventoryId = "inventory.business.stock",
                establishmentId = "business-establishment.stock",
                purpose = BusinessInventoryPurpose.RetailStock,
                effectiveStartWorldTime = 2d
            });
            BusinessOperationResult stock = fixture.Businesses.ClassifyStock(new BusinessStockClassificationData
            {
                stockClassificationId = "business-stock.sword",
                businessId = fixture.BusinessId("stock"),
                inventoryId = "inventory.business.stock",
                itemInstanceId = itemId,
                category = BusinessStockCategory.ForSale,
                saleEligible = true
            }, fixture.Items);

            Assert.That(item.Succeeded, Is.True, item.Message);
            Assert.That(establishment.Succeeded, Is.True, establishment.Message);
            Assert.That(account.Succeeded, Is.True, account.Message);
            Assert.That(inventory.Succeeded, Is.True, inventory.Message);
            Assert.That(stock.Succeeded, Is.True, stock.Message);
            Assert.That(fixture.Items.TryGetSnapshot(itemId, out ItemInstanceSnapshot snapshot), Is.True);
            Assert.That(snapshot.OwnerPersonId, Is.EqualTo("person.owner"));
            Assert.That(snapshot.CustodianPersonId, Is.EqualTo("person.custodian"));
        }

        [Test]
        public void ProductionOwnershipIsSeparateFromProducerAndOutputOwnership()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareActiveBusiness("production");
            fixture.Economy.CreateAccount("account.business.production", fixture.Gold, fixture.BusinessId("production"), EconomyAccountKind.OrganizationAccount, 100L, "tx.account.production");
            ProductionWorkflowResult order = fixture.Production.CreateWorkOrder(new ProductionWorkOrderData
            {
                workOrderId = "work-order.business.production",
                requesterPersonId = "person.customer",
                recipeDefinitionId = fixture.Recipe.Id,
                requestedQuantity = 1,
                ownerPersonId = "person.customer",
                custodianPersonId = "person.customer",
                state = ProductionWorkOrderState.Approved
            }, fixture.Registry);
            ProductionWorkflowResult job = fixture.Production.CreateJobFromWorkOrder("production-job.business.production", "work-order.business.production", fixture.Registry);

            BusinessOperationResult sponsor = fixture.Businesses.SponsorProduction(new BusinessProductionOwnershipData
            {
                productionOwnershipId = "business-production.sponsor",
                businessId = fixture.BusinessId("production"),
                productionJobId = "production-job.business.production",
                productionSponsorSubjectId = fixture.BusinessId("production"),
                responsibleProducerSubjectId = "person.employee",
                outputOwnerPolicy = ProductionOutputOwnerPolicy.CustomerOwnsSuppliedInputsAndOutput,
                fundingAccountId = "account.business.production",
                outputInventoryIds = new[] { "inventory.customer.outputs" }
            }, fixture.Production, fixture.Economy);

            Assert.That(order.Succeeded, Is.True, order.Message);
            Assert.That(job.Succeeded, Is.True, job.Message);
            Assert.That(sponsor.Succeeded, Is.True, sponsor.Message);
            Assert.That(sponsor.ProductionOwnership.productionSponsorSubjectId, Is.EqualTo(fixture.BusinessId("production")));
            Assert.That(sponsor.ProductionOwnership.responsibleProducerSubjectId, Is.EqualTo("person.employee"));
            Assert.That(sponsor.ProductionOwnership.outputOwnerPolicy, Is.EqualTo(ProductionOutputOwnerPolicy.CustomerOwnsSuppliedInputsAndOutput));
        }

        [Test]
        public void RevenueExpensesCapitalWithdrawalsAndStatementsRemainDistinct()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareActiveBusiness("accounting");
            fixture.Economy.CreateAccount("account.business.accounting", fixture.Gold, fixture.BusinessId("accounting"), EconomyAccountKind.OrganizationAccount, 0L, "tx.account.business");
            fixture.Economy.CreateAccount("account.customer", fixture.Gold, "person.customer", EconomyAccountKind.PersonWallet, 200L, "tx.account.customer");
            fixture.Economy.CreateAccount("account.vendor", fixture.Gold, "person.vendor", EconomyAccountKind.PersonWallet, 0L, "tx.account.vendor");
            fixture.Economy.CreateAccount("account.owner", fixture.Gold, "person.owner", EconomyAccountKind.PersonWallet, 50L, "tx.account.owner");
            EconomyOperationResult sale = fixture.Economy.Transfer("tx.sale", "account.customer", "account.business.accounting", new MoneyAmount(fixture.Gold.Id, 120L), EconomyTransactionKind.Payment);
            EconomyOperationResult material = fixture.Economy.Transfer("tx.material", "account.business.accounting", "account.vendor", new MoneyAmount(fixture.Gold.Id, 30L), EconomyTransactionKind.Payment);
            EconomyOperationResult capitalTransfer = fixture.Economy.Transfer("tx.capital", "account.owner", "account.business.accounting", new MoneyAmount(fixture.Gold.Id, 50L), EconomyTransactionKind.Transfer);

            BusinessOperationResult revenue = fixture.Businesses.RecordRevenue(new BusinessRevenueRecordData
            {
                revenueRecordId = "business-revenue.sale",
                businessId = fixture.BusinessId("accounting"),
                category = BusinessRevenueCategory.RetailSale,
                amount = BusinessModelHelpers.Money(fixture.Gold.Id, 120L),
                transactionId = sale.Transaction.TransactionId,
                recognitionWorldTime = 3d
            }, fixture.Economy);
            BusinessOperationResult expense = fixture.Businesses.RecordExpense(new BusinessExpenseRecordData
            {
                expenseRecordId = "business-expense.material",
                businessId = fixture.BusinessId("accounting"),
                category = BusinessExpenseCategory.MaterialPurchase,
                amount = BusinessModelHelpers.Money(fixture.Gold.Id, 30L),
                transactionId = material.Transaction.TransactionId,
                recognitionWorldTime = 4d
            }, fixture.Economy);
            BusinessOperationResult capital = fixture.Businesses.AddCapitalContribution(new BusinessCapitalContributionData
            {
                contributionId = "business-capital.owner",
                businessId = fixture.BusinessId("accounting"),
                contributingSubjectId = "person.owner",
                monetaryValue = BusinessModelHelpers.Money(fixture.Gold.Id, 50L),
                transactionOrTransferReferenceId = capitalTransfer.Transaction.TransactionId,
                worldTime = 2d
            }, fixture.Economy);
            BusinessOperationResult period = fixture.Businesses.OpenAccountingPeriod(new BusinessAccountingPeriodData
            {
                accountingPeriodId = "business-period.accounting",
                businessId = fixture.BusinessId("accounting"),
                currencyId = fixture.Gold.Id,
                startWorldTime = 0d,
                endWorldTime = 10d
            });
            BusinessOperationResult close = fixture.Businesses.CloseAccountingPeriod("business-period.accounting", "business-pnl.accounting", "business-cashflow.accounting", 10d);

            Assert.That(revenue.Succeeded, Is.True, revenue.Message);
            Assert.That(expense.Succeeded, Is.True, expense.Message);
            Assert.That(capital.Succeeded, Is.True, capital.Message);
            Assert.That(period.Succeeded, Is.True, period.Message);
            Assert.That(close.Succeeded, Is.True, close.Message);
            Assert.That(close.ProfitAndLossStatement.netOperatingResult.units, Is.EqualTo(90L));
            Assert.That(close.CashFlowSummary.netCashChange.units, Is.EqualTo(140L));
        }

        [Test]
        public void BusinessAccessProjectionRedactsWithoutMutatingAuthoritativeRecord()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareActiveBusiness("secret", accessPolicyId: "policy.business.secret");
            fixture.Access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = "policy.business.secret",
                subject = fixture.Businesses.Businesses.Single().CreateInformationSubject(),
                classification = InformationVisibilityClassification.Secret,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                defaultVisibleDetails = new[] { "business.public" },
                defaultRedactedDetails = new[] { "business.owners", "business.control", "business.accounts", "business.financials" },
                redactedAccessAcceptable = true
            }, "tx.business.policy");
            fixture.Access.GrantAccess(new InformationAccessGrantData
            {
                grantId = "grant.business.viewer",
                policyId = "policy.business.secret",
                subject = fixture.Businesses.Businesses.Single().CreateInformationSubject(),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.viewer",
                grantorId = "person.owner",
                accessModes = new[] { InformationAccessMode.Query },
                detailIds = new[] { "business.public" }
            }, "tx.business.grant");

            BusinessProjection projection = fixture.Businesses.ProjectBusiness(fixture.BusinessId("secret"), fixture.Access, new InformationAccessContext
            {
                RequestingPersonId = "person.viewer",
                HasDiscoveredSubject = true,
                RevealDenialReasons = true
            }, BusinessProjectionKind.Public);

            Assert.That(projection.Denied, Is.False);
            Assert.That(projection.Redacted, Is.True);
            Assert.That(projection.Business.founderSubjectIds, Is.Empty);
            Assert.That(fixture.Businesses.TryGetBusiness(fixture.BusinessId("secret"), out BusinessInstanceData live), Is.True);
            Assert.That(live.founderSubjectIds, Does.Contain("person.owner"));
        }

        [Test]
        public void BusinessPersistenceRejectsBrokenReferencesBeforeCommit()
        {
            Fixture fixture = Fixture.Create();
            fixture.PrepareActiveBusiness("persist");
            BusinessPersistenceParticipant participant = new BusinessPersistenceParticipant(fixture.Businesses, () => fixture.Registry);
            BusinessRuntimeSaveData corrupt = fixture.Businesses.CreateSaveData();
            corrupt.ownershipRecords[0].businessId = "business.missing";

            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(corrupt), BusinessPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(prepared.Succeeded, Is.False);
            Assert.That(fixture.Businesses.TryGetBusiness(fixture.BusinessId("persist"), out BusinessInstanceData live), Is.True);
            Assert.That(live.businessId, Is.EqualTo(fixture.BusinessId("persist")));
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, CurrencyDefinition gold, BusinessDefinition businessDefinition, ItemDefinition sword, RecipeDefinition recipe)
            {
                Registry = registry;
                Gold = gold;
                BusinessDefinition = businessDefinition;
                Sword = sword;
                Recipe = recipe;
                Economy = new EconomyRuntime();
                Businesses = new BusinessRuntime();
                Items = new ItemInstanceIdentityRuntime();
                Production = new ProductionWorkflowRuntime();
                Access = new InformationAccessRuntime();
                Economy.Configure(registry, PersistenceService.LocalWorldId);
                Businesses.Configure(registry, PersistenceService.LocalWorldId);
                Access.Configure(registry, "person.viewer");
            }

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Gold { get; }
            public BusinessDefinition BusinessDefinition { get; }
            public ItemDefinition Sword { get; }
            public RecipeDefinition Recipe { get; }
            public EconomyRuntime Economy { get; }
            public BusinessRuntime Businesses { get; }
            public ItemInstanceIdentityRuntime Items { get; }
            public ProductionWorkflowRuntime Production { get; }
            public InformationAccessRuntime Access { get; }

            public static Fixture Create()
            {
                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G");
                BusinessDefinition business = ScriptableObject.CreateInstance<BusinessDefinition>();
                business.Initialize("business.test.merchant-shop", "Test Merchant Shop", BusinessCategory.MerchantShop);
                ItemDefinition sword = Item("item.test.sword", "Test Sword");
                RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
                SetPrivate(recipe, "recipeId", "recipe.test.business-output");
                SetPrivate(recipe, "displayName", "Test Business Output");
                SetPrivate(recipe, "category", RecipeCategory.Crafting);
                SetPrivate(recipe, "currentVersionId", "v1");
                DefinitionRegistry registry = new DefinitionRegistry(new IGameDefinition[] { gold, business, sword, recipe });
                return new Fixture(registry, gold, business, sword, recipe);
            }

            public string BusinessId(string slug)
            {
                return $"business.test.{slug}";
            }

            public void PrepareActiveBusiness(string slug, string accessPolicyId = "")
            {
                BusinessOperationResult create = CreateBusiness(slug, accessPolicyId);
                BusinessOperationResult owner = AddOwner(slug, "person.owner");
                BusinessOperationResult active = Businesses.TransitionBusiness(BusinessId(slug), BusinessState.Active, 2d);
                Assert.That(create.Succeeded, Is.True, create.Message);
                Assert.That(owner.Succeeded, Is.True, owner.Message);
                Assert.That(active.Succeeded, Is.True, active.Message);
            }

            public BusinessOperationResult CreateBusiness(string slug, string accessPolicyId = "")
            {
                return Businesses.CreateBusiness(new BusinessInstanceData
                {
                    businessId = BusinessId(slug),
                    businessDefinitionId = BusinessDefinition.Id,
                    displayName = $"Business {slug}",
                    linkedOrganizationId = "organization.test.business",
                    founderSubjectIds = new[] { "person.owner" },
                    operatingCurrencyIds = new[] { Gold.Id },
                    accessPolicyId = accessPolicyId,
                    state = BusinessState.Planned,
                    createdWorldTime = 1d
                });
            }

            public BusinessOperationResult AddOwner(string slug, string ownerId)
            {
                return Businesses.AddOwnership(new BusinessOwnershipRecordData
                {
                    ownershipRecordId = $"business-ownership.{slug}.{ownerId}",
                    businessId = BusinessId(slug),
                    owner = new BusinessSubjectReferenceData { kind = BusinessOwnerSubjectKind.Person, subjectId = ownerId },
                    category = BusinessOwnershipCategory.SoleOwner,
                    economicShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                    votingShare = new BusinessRationalData { numerator = 10000L, denominator = 10000L },
                    effectiveStartWorldTime = 1d
                }, 1d);
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
