using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.Economy.InstitutionalRevenue;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Governments;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class GovernmentFiscalHierarchyTests
    {
        [Test]
        public void RecognizedRevenue_RemitsUpImmediateParentChainWithoutDoubleTaxing()
        {
            CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
            gold.Initialize("currency.test-gold", "Test Gold");
            InstitutionalRevenueDefinition levy = ScriptableObject.CreateInstance<InstitutionalRevenueDefinition>();
            levy.Initialize("revenue.test-levy", "Test Levy", InstitutionalRevenueCategory.SalesTaxFoundation,
                InstitutionKind.SettlementFoundation, InstitutionalRevenueAuthorityCategory.Assess, gold,
                TaxBaseKind.TransactionGrossAmount,
                new RevenueRatePolicyData { ratePolicyId = "revenue-rate.test", rateKind = RevenueRateKind.FlatProportional, currencyOrUnitId = gold.Id, rate = new RevenueRationalData { numerator = 1L, denominator = 1L }, smallestChargeableUnit = 1L },
                AssessmentPeriodKind.PerTransaction,
                new[] { RevenueSubjectKind.Buyer }, new[] { TaxableEventCategory.CompletedTrade });

            DefinitionRegistry definitions = PrototypeGovernmentDefinitionFactory.AddMissingPrototypeGovernmentDefinitions(new DefinitionRegistry(new IGameDefinition[] { gold, levy }));
            EconomyRuntime economy = new EconomyRuntime(); economy.Configure(definitions, PersistenceService.LocalWorldId);
            Assert.That(economy.CreateAccount("account.payer", gold, "person.payer", EconomyAccountKind.PersonWallet, 100L, "tx.account.payer").Succeeded, Is.True);
            foreach (string account in new[] { "account.town", "account.manor", "account.duchy", "account.kingdom" }) Assert.That(economy.CreateAccount(account, gold, account, EconomyAccountKind.OrganizationAccount, 0L, $"tx.{account}").Succeeded, Is.True);

            InstitutionalRevenueRuntime revenue = new InstitutionalRevenueRuntime(); revenue.Configure(definitions);
            Assert.That(revenue.RegisterAuthority(new InstitutionalRevenueAuthorityData { authorityId = "authority.test", institutionId = "institution.test-town", institutionKind = InstitutionKind.SettlementFoundation, authorityCategory = InstitutionalRevenueAuthorityCategory.Assess, sourceReferenceId = "law.test.sales-tax", sourceRuntime = "GovernmentFiscalHierarchyTests", permittedRevenueDefinitionIds = new[] { levy.Id }, permittedRevenueCategories = new[] { InstitutionalRevenueCategory.SalesTaxFoundation }, permittedSubjectKinds = new[] { RevenueSubjectKind.Buyer }, permittedCurrencyIds = new[] { gold.Id }, canAssess = true, canCollect = true, canReceiveRemittance = true, canAllocateRevenue = true }, "tx.authority").Succeeded, Is.True);
            Assert.That(revenue.AssignRevenueAccount(new InstitutionalRevenueAccountAssignmentData { assignmentId = "assignment.test", institutionId = "institution.test-town", institutionKind = InstitutionKind.SettlementFoundation, accountId = "account.town", purpose = RevenueAccountPurpose.TaxCollection, currencyId = gold.Id, receivingAuthorityId = "authority.test" }, "tx.assignment").Succeeded, Is.True);

            GovernmentRuntime governments = new GovernmentRuntime();
            governments.Configure(definitions, null, null, null, null, null, null, null, null, PersistenceService.LocalWorldId, Array.Empty<string>(), Array.Empty<string>(), null, economy, revenue);
            Assert.That(governments.CreatePolity(new PolityCreateRequest { transactionId = "tx.polity", polityId = "polity.test", polityDefinitionId = PrototypeGovernmentDefinitionFactory.KingdomPolityDefinitionId, officialName = "Test Kingdom" }).Succeeded, Is.True);
            Register(governments, "government.kingdom", PrototypeGovernmentDefinitionFactory.RoyalGovernmentDefinitionId, string.Empty, GovernmentLevel.Central);
            Register(governments, "government.duchy", PrototypeGovernmentDefinitionFactory.DucalAdministrationDefinitionId, "government.kingdom", GovernmentLevel.Provincial);
            Register(governments, "government.manor", PrototypeGovernmentDefinitionFactory.ManorAdministrationDefinitionId, "government.duchy", GovernmentLevel.County);
            Register(governments, "government.town", PrototypeGovernmentDefinitionFactory.TownAdministrationDefinitionId, "government.manor", GovernmentLevel.Municipal);
            Policy(governments, "town-manor", "government.town", "government.manor", "account.town", "account.manor", gold.Id, new[] { levy.Id });
            Policy(governments, "manor-duchy", "government.manor", "government.duchy", "account.manor", "account.duchy", gold.Id, Array.Empty<string>());
            Policy(governments, "duchy-kingdom", "government.duchy", "government.kingdom", "account.duchy", "account.kingdom", gold.Id, Array.Empty<string>());

            Assert.That(revenue.RegisterTaxableEvent(new TaxableEventData { taxableEventId = "event.test", revenueDefinitionId = levy.Id, eligibleCategory = InstitutionalRevenueCategory.SalesTaxFoundation, eventCategory = TaxableEventCategory.CompletedTrade, assessedSubject = new RevenueSubjectReferenceData { subjectKind = RevenueSubjectKind.Buyer, role = RevenueSubjectRole.AssessedParty, subjectId = "person.payer", accountId = "account.payer" }, institutionId = "institution.test-town", currencyId = gold.Id, monetaryValueUnits = 80L, sourceRuntime = "GovernmentFiscalHierarchyTests", sourceRecordId = "trade.test" }, "authority.test", "tx.event").Succeeded, Is.True);
            InstitutionalRevenueOperationResult assessment = revenue.GenerateAssessment("assessment.test", levy.Id, new[] { "event.test" }, "authority.test", "period.test", approve: true, transactionId: "tx.assess");
            Assert.That(assessment.Succeeded, Is.True, assessment.Message);
            InstitutionalRevenueOperationResult payment = revenue.PayObligation(assessment.Obligation.obligationId, economy, "tx.pay", 80L);
            Assert.That(payment.Succeeded, Is.True, payment.Message);
            Assert.That(revenue.RecognizeRevenue(payment.Payment.paymentId, "revenue-record.test", "test", "tx.recognize").Succeeded, Is.True);

            PoliticalOperationResult processed = governments.ProcessWorldTime(new PoliticalTimeEvaluationRequest { transactionId = "tx.time", boundaryId = "boundary.test", worldTime = 1d });
            Assert.That(processed.Succeeded, Is.True, processed.Message);
            AssertBalance(economy, "account.town", 40L);
            AssertBalance(economy, "account.manor", 20L);
            AssertBalance(economy, "account.duchy", 10L);
            AssertBalance(economy, "account.kingdom", 10L);
            Assert.That(governments.RemittanceSettlements.Count, Is.EqualTo(3));
            Assert.That(governments.RemittanceSettlements, Has.All.Matches<GovernmentRemittanceSettlementRecordData>(value => value.state == GovernmentRemittanceSettlementState.Paid));
            Assert.That(governments.CreateSaveData().remittanceSettlements.Length, Is.EqualTo(3));
        }

        [Test]
        public void FiscalRelief_ReducesOnlyTheConfiguredSettlement()
        {
            // Relief is persisted as an explicit, time-bounded policy modifier rather than rewriting the base policy.
            GovernmentRuntimeSaveData save = new GovernmentRuntimeSaveData
            {
                fiscalReliefs = new[] { new GovernmentFiscalReliefRecordData { reliefId = "relief.test", remittancePolicyId = "policy.test", reductionBasisPoints = 5000, effectiveWorldTime = 10d, expirationWorldTime = 20d } }
            };
            GovernmentRuntimeSaveData clone = save.Clone();
            clone.fiscalReliefs[0].reductionBasisPoints = 10000;
            Assert.That(save.fiscalReliefs[0].reductionBasisPoints, Is.EqualTo(5000));
            Assert.That(save.fiscalReliefs[0].IsActiveAt(15d), Is.True);
            Assert.That(save.fiscalReliefs[0].IsActiveAt(20d), Is.False);
        }

        private static void Register(GovernmentRuntime runtime, string id, string definition, string parent, GovernmentLevel level)
        {
            PoliticalOperationResult result = runtime.RegisterGovernment(new GovernmentRegisterRequest { transactionId = $"tx.{id}", governmentId = id, governmentDefinitionId = definition, polityId = "polity.test", officialName = id, primaryGoverningOrganizationId = "organization.test", governingOrganizationIds = new[] { "organization.test" }, parentGovernmentId = parent, level = level });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void Policy(GovernmentRuntime runtime, string suffix, string source, string destination, string from, string to, string currency, string[] revenueIds)
        {
            PoliticalOperationResult result = runtime.EnactRemittancePolicy(new GovernmentRemittancePolicyRequest { transactionId = $"tx.policy.{suffix}", policyId = $"policy.{suffix}", sourceGovernmentId = source, destinationGovernmentId = destination, sourceEconomyAccountId = from, destinationEconomyAccountId = to, currencyDefinitionId = currency, eligibleRevenueDefinitionIds = revenueIds, remittanceBasisPoints = 5000, settlementIntervalWorldTime = 10d, firstSettlementWorldTime = 1d });
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void AssertBalance(EconomyRuntime economy, string accountId, long expected)
        {
            Assert.That(economy.TryGetAccount(accountId, out EconomyAccountSnapshot account), Is.True);
            Assert.That(account.BalanceUnits, Is.EqualTo(expected), accountId);
        }
    }
}
