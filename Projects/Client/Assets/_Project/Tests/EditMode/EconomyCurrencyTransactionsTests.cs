using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.Economy;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Persistence;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Tests
{
    public sealed class EconomyCurrencyTransactionsTests
    {
        [Test]
        public void MoneyAmountsAreExactAndRejectMixedCurrencies()
        {
            MoneyAmount gold = new MoneyAmount("currency.gold", 100L);
            MoneyAmount total = gold.Add(new MoneyAmount("currency.gold", 25L)).Subtract(new MoneyAmount("currency.gold", 5L));

            Assert.That(total.Units, Is.EqualTo(120L));
            Assert.Throws<InvalidOperationException>(() => total.Add(new MoneyAmount("currency.silver", 1L)));
        }

        [Test]
        public void AccountsTransfersReservationsAndRefundsAreAtomicAndConserved()
        {
            Fixture fixture = Fixture.Create();
            EconomyRuntime runtime = fixture.Economy;
            runtime.CreateAccount("account.buyer", fixture.Gold, "person.buyer", EconomyAccountKind.PersonWallet, 100L, "tx.open.buyer");
            runtime.CreateAccount("account.seller", fixture.Gold, "person.seller", EconomyAccountKind.PersonWallet, 0L, "tx.open.seller");

            EconomyOperationResult reserve = runtime.Reserve("reservation.order", "account.buyer", new MoneyAmount(fixture.Gold.Id, 40L), "order.prototype");
            EconomyOperationResult payment = runtime.Transfer("tx.payment", "account.buyer", "account.seller", new MoneyAmount(fixture.Gold.Id, 40L), EconomyTransactionKind.Payment, reservationId: "reservation.order");
            EconomyOperationResult duplicate = runtime.Transfer("tx.payment", "account.buyer", "account.seller", new MoneyAmount(fixture.Gold.Id, 40L), EconomyTransactionKind.Payment, reservationId: "reservation.order");
            EconomyOperationResult refund = runtime.Refund("tx.refund", "tx.payment");

            Assert.That(reserve.Succeeded, Is.True, reserve.Message);
            Assert.That(payment.Succeeded, Is.True, payment.Message);
            Assert.That(duplicate.Succeeded, Is.True, duplicate.Message);
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(refund.Succeeded, Is.True, refund.Message);
            Assert.That(runtime.TryGetAccount("account.buyer", out EconomyAccountSnapshot buyer), Is.True);
            Assert.That(runtime.TryGetAccount("account.seller", out EconomyAccountSnapshot seller), Is.True);
            Assert.That(buyer.BalanceUnits, Is.EqualTo(100L));
            Assert.That(seller.BalanceUnits, Is.EqualTo(0L));
            Assert.That(runtime.LedgerEntries.Where(entry => entry.transactionId == "tx.payment").Sum(entry => entry.kind == EconomyLedgerEntryKind.Credit ? entry.units : -entry.units), Is.Zero);
            Assert.That(runtime.TryGetTransaction("tx.payment", out EconomyTransactionSnapshot original), Is.True);
            Assert.That(original.State, Is.EqualTo(EconomyTransactionState.Refunded));
        }

        [Test]
        public void PhysicalAndAbstractCurrencyConversionsUseItemIdentityRuntime()
        {
            Fixture fixture = Fixture.Create(withPhysicalCoin: true);
            EconomyRuntime runtime = fixture.Economy;
            runtime.CreateAccount("account.wallet", fixture.Gold, "person.owner", EconomyAccountKind.PersonWallet, 0L, "tx.wallet");
            ItemInstanceOperationResult coin = fixture.Items.CreateItem(fixture.Coin, ItemInstanceClassification.Fungible, "11111111-1111-1111-1111-111111111111", ownerPersonId: "person.owner", custodianPersonId: "person.owner");

            EconomyOperationResult toAbstract = runtime.ConvertPhysicalToAbstract("tx.coin.deposit", "account.wallet", fixture.Gold, fixture.Items, coin.Snapshot.ItemInstanceId, 5, "person.owner");
            EconomyOperationResult toPhysical = runtime.ConvertAbstractToPhysical("tx.coin.withdraw", "account.wallet", fixture.Gold, fixture.Items, 2, "person.owner", "22222222-2222-2222-2222-222222222222");

            Assert.That(coin.Succeeded, Is.True, coin.Message);
            Assert.That(toAbstract.Succeeded, Is.True, toAbstract.Message);
            Assert.That(toPhysical.Succeeded, Is.True, toPhysical.Message);
            Assert.That(fixture.Items.TryGetSnapshot(coin.Snapshot.ItemInstanceId, out ItemInstanceSnapshot spentCoin), Is.True);
            Assert.That(spentCoin.LifecycleState, Is.EqualTo(ItemLifecycleState.Consumed));
            Assert.That(fixture.Items.TryGetSnapshot("22222222-2222-2222-2222-222222222222", out ItemInstanceSnapshot withdrawnCoin), Is.True);
            Assert.That(withdrawnCoin.ItemDefinitionId, Is.EqualTo(fixture.Coin.Id));
            Assert.That(runtime.TryGetAccount("account.wallet", out EconomyAccountSnapshot wallet), Is.True);
            Assert.That(wallet.BalanceUnits, Is.EqualTo(3L));
        }

        [Test]
        public void SaveRestoreValidatesLedgerSnapshotsAndRejectsCorruptionWithoutMutation()
        {
            Fixture fixture = Fixture.Create();
            EconomyRuntime runtime = fixture.Economy;
            runtime.CreateAccount("account.a", fixture.Gold, "person.a", EconomyAccountKind.PersonWallet, 50L, "tx.open.a");
            runtime.CreateAccount("account.b", fixture.Gold, "person.b", EconomyAccountKind.PersonWallet, 0L, "tx.open.b");
            runtime.Transfer("tx.transfer", "account.a", "account.b", new MoneyAmount(fixture.Gold.Id, 15L), EconomyTransactionKind.Payment);
            runtime.CaptureFixedPrice("price.sword", "item.prototype-sword", "account.b", new MoneyAmount(fixture.Gold.Id, 25L), "price-list.prototype", "person.b");
            EconomyRuntimeSaveData save = runtime.CreateSaveData();

            EconomyRuntime restored = new EconomyRuntime();
            EconomyOperationResult restore = restored.RestoreFromSaveData(save, fixture.Registry);
            EconomyRuntimeSaveData corrupt = save.Clone();
            corrupt.ledgerEntries.First(entry => entry.transactionId == "tx.transfer" && entry.kind == EconomyLedgerEntryKind.Credit).units = 999L;
            bool rejected = !EconomyRuntime.ValidateSaveData(corrupt, fixture.Registry, out string failure);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(restored.TryGetAccount("account.a", out EconomyAccountSnapshot restoredA), Is.True);
            Assert.That(restoredA.BalanceUnits, Is.EqualTo(35L));
            Assert.That(rejected, Is.True);
            Assert.That(failure, Does.Contain("ledger"));
            Assert.That(runtime.TryGetAccount("account.a", out EconomyAccountSnapshot liveA), Is.True);
            Assert.That(liveA.BalanceUnits, Is.EqualTo(35L));
        }

        [Test]
        public void PersistenceParticipantRejectsInvalidPayloadBeforeCommit()
        {
            Fixture fixture = Fixture.Create();
            fixture.Economy.CreateAccount("account.live", fixture.Gold, "person.live", EconomyAccountKind.PersonWallet, 10L, "tx.live");
            EconomyPersistenceParticipant participant = new EconomyPersistenceParticipant(fixture.Economy, () => fixture.Registry);
            EconomyRuntimeSaveData save = fixture.Economy.CreateSaveData();
            save.accounts[0].currencyId = "currency.missing";

            PersistenceParticipantPrepareResult prepared = participant.PreparePayload(JsonUtility.ToJson(save), EconomyPersistenceParticipant.CurrentParticipantSchemaVersion);

            Assert.That(prepared.Succeeded, Is.False);
            Assert.That(fixture.Economy.TryGetAccount("account.live", out EconomyAccountSnapshot live), Is.True);
            Assert.That(live.BalanceUnits, Is.EqualTo(10L));
        }

        [Test]
        public void AccountProjectionUsesInformationAccessWithoutMutatingBalances()
        {
            Fixture fixture = Fixture.Create();
            fixture.Economy.CreateAccount("account.secret", fixture.Gold, "person.owner", EconomyAccountKind.PersonWallet, 88L, "tx.secret");
            Assert.That(fixture.Economy.TryGetAccount("account.secret", out EconomyAccountSnapshot account), Is.True);
            InformationAccessRuntime access = new InformationAccessRuntime();
            access.Configure(fixture.Registry, "person.viewer");
            access.RegisterPolicy(new InformationAccessPolicyData
            {
                policyId = "policy.account.secret",
                subject = account.CreateInformationSubject(),
                classification = InformationVisibilityClassification.Secret,
                detailVisibilityPolicy = InformationDetailVisibilityPolicy.Selected,
                defaultVisibleDetails = new[] { "detail.account", "detail.currency" },
                defaultRedactedDetails = new[] { "detail.owner", "detail.balance", "detail.reserved" },
                redactedAccessAcceptable = true
            }, "tx.policy");
            InformationAccessOperationResult grant = access.GrantAccess(new InformationAccessGrantData
            {
                grantId = "grant.account.viewer",
                policyId = "policy.account.secret",
                subject = account.CreateInformationSubject(),
                granteeKind = InformationGranteeKind.Person,
                granteeId = "person.viewer",
                grantorId = "person.owner",
                accessModes = new[] { InformationAccessMode.Inspect },
                detailIds = new[] { "detail.account", "detail.currency" }
            }, "tx.grant");

            InformationAccessProjection<EconomyAccountSnapshot> projected = fixture.Economy.GetAccountProjection("account.secret", access, new InformationAccessContext
            {
                RequestingPersonId = "person.viewer",
                HasDiscoveredSubject = true,
                RevealDenialReasons = true
            }, "policy.account.secret");

            Assert.That(grant.Succeeded, Is.True, grant.Message);
            Assert.That(projected.Succeeded, Is.True, projected.Message);
            Assert.That(projected.Redacted, Is.True);
            Assert.That(projected.Record.BalanceUnits, Is.Zero);
            Assert.That(fixture.Economy.TryGetAccount("account.secret", out EconomyAccountSnapshot live), Is.True);
            Assert.That(live.BalanceUnits, Is.EqualTo(88L));
        }

        private sealed class Fixture
        {
            private Fixture(DefinitionRegistry registry, CurrencyDefinition gold, ItemDefinition coin, EconomyRuntime economy, ItemInstanceIdentityRuntime items)
            {
                Registry = registry;
                Gold = gold;
                Coin = coin;
                Economy = economy;
                Items = items;
            }

            public DefinitionRegistry Registry { get; }
            public CurrencyDefinition Gold { get; }
            public ItemDefinition Coin { get; }
            public EconomyRuntime Economy { get; }
            public ItemInstanceIdentityRuntime Items { get; }

            public static Fixture Create(bool withPhysicalCoin = false)
            {
                ItemDefinition coin = Item("item.prototype-gold-coin", "Prototype Gold Coin");
                CurrencyDefinition gold = ScriptableObject.CreateInstance<CurrencyDefinition>();
                gold.Initialize("currency.gold", "Gold", "G", physicalItem: withPhysicalCoin ? coin : null, physicalUnits: 1L);
                DefinitionRegistry registry = withPhysicalCoin
                    ? new DefinitionRegistry(new IGameDefinition[] { gold, coin })
                    : new DefinitionRegistry(new IGameDefinition[] { gold });
                EconomyRuntime economy = new EconomyRuntime();
                economy.Configure(registry, PersistenceService.LocalWorldId);
                return new Fixture(registry, gold, coin, economy, new ItemInstanceIdentityRuntime());
            }

            private static ItemDefinition Item(string id, string display)
            {
                ItemDefinition item = ScriptableObject.CreateInstance<ItemDefinition>();
                SetPrivate(item, "itemId", id);
                SetPrivate(item, "displayName", display);
                SetPrivate(item, "stackable", true);
                SetPrivate(item, "maximumStackSize", 999);
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
