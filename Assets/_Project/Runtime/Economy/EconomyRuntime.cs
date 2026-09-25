using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Inventory.Identity;
using UnityIsekaiGame.Knowledge.Access;
using UnityIsekaiGame.Progression;

namespace UnityIsekaiGame.Economy
{
    public sealed class EconomyRuntime
    {
        private readonly Dictionary<string, EconomyAccountData> accountsById = new Dictionary<string, EconomyAccountData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomyReservationData> reservationsById = new Dictionary<string, EconomyReservationData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomyTransactionData> transactionsById = new Dictionary<string, EconomyTransactionData>(StringComparer.Ordinal);
        private readonly Dictionary<string, FixedPriceSnapshotData> pricesById = new Dictionary<string, FixedPriceSnapshotData>(StringComparer.Ordinal);
        private readonly Dictionary<string, EconomyProcessedTransactionData> processedByTransactionId = new Dictionary<string, EconomyProcessedTransactionData>(StringComparer.Ordinal);
        private readonly List<EconomyLedgerEntryData> ledgerEntries = new List<EconomyLedgerEntryData>();
        private DefinitionRegistry registry;
        private string worldId;
        private long nextSequence = 1L;

        public long Revision { get; private set; }
        public long NextSequence => nextSequence;
        public int AccountCount => accountsById.Count;
        public int TransactionCount => transactionsById.Count;
        public int LedgerEntryCount => ledgerEntries.Count;

        public void Configure(DefinitionRegistry definitionRegistry, string world)
        {
            registry = definitionRegistry ?? registry;
            worldId = world ?? string.Empty;
        }

        public IReadOnlyList<EconomyAccountSnapshot> Accounts => accountsById.Values
            .OrderBy(account => account.accountId, StringComparer.Ordinal)
            .Select(account => Snapshot(account))
            .ToArray();

        public IReadOnlyList<EconomyReservationData> Reservations => reservationsById.Values
            .OrderBy(reservation => reservation.reservationId, StringComparer.Ordinal)
            .Select(reservation => reservation.Clone())
            .ToArray();

        public IReadOnlyList<EconomyTransactionSnapshot> Transactions => transactionsById.Values
            .OrderBy(transaction => transaction.sequence)
            .ThenBy(transaction => transaction.transactionId, StringComparer.Ordinal)
            .Select(transaction => Snapshot(transaction))
            .ToArray();

        public IReadOnlyList<EconomyLedgerEntryData> LedgerEntries => ledgerEntries
            .OrderBy(entry => entry.sequence)
            .ThenBy(entry => entry.entryId, StringComparer.Ordinal)
            .Select(entry => entry.Clone())
            .ToArray();

        public EconomyOperationResult CreateAccount(string accountId, CurrencyDefinition currency, string ownerId, EconomyAccountKind kind, long openingBalanceUnits = 0L, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(accountId))
            {
                return Fail(EconomyResultCode.InvalidRequest, "Account ID is required.", preview);
            }

            if (!ValidateCurrency(currency, out string currencyFailure))
            {
                return Fail(EconomyResultCode.MissingCurrency, currencyFailure, preview);
            }

            if (openingBalanceUnits < 0L)
            {
                return Fail(EconomyResultCode.InvalidRequest, "Opening balance cannot be negative.", preview);
            }

            if (accountsById.ContainsKey(accountId))
            {
                return Fail(EconomyResultCode.InvalidRequest, $"Account '{accountId}' already exists.", preview);
            }

            EconomyAccountData account = new EconomyAccountData
            {
                accountId = accountId.Trim(),
                ownerId = ownerId ?? string.Empty,
                kind = kind,
                state = EconomyAccountState.Active,
                currencyId = currency.Id,
                balanceUnits = openingBalanceUnits,
                revision = 1L
            };

            if (preview)
            {
                return EconomyOperationResult.Success("Account creation preview succeeded.", before, before, preview: true, toAccount: Snapshot(account));
            }

            accountsById.Add(account.accountId, account);
            Revision++;
            if (openingBalanceUnits > 0L)
            {
                CommitTransaction(new EconomyTransactionData
                {
                    transactionId = string.IsNullOrWhiteSpace(transactionId) ? $"economy.issuance.{account.accountId}.{Revision}" : transactionId,
                    kind = EconomyTransactionKind.Issuance,
                    currencyId = account.currencyId,
                    units = openingBalanceUnits,
                    toAccountId = account.accountId,
                    actorId = ownerId ?? string.Empty,
                    reason = "Opening balance",
                    worldTime = 0d
                }, mutateBalances: false);
                Remember(transactionId, "create-account", account.accountId);
            }

            return EconomyOperationResult.Success("Account created.", before, Revision, toAccount: Snapshot(account));
        }

        public EconomyOperationResult ChangeAccountState(string transactionId, string accountId, EconomyAccountState targetState, bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(accountId) || !accountsById.TryGetValue(accountId, out EconomyAccountData account))
            {
                return Fail(EconomyResultCode.MissingAccount, $"Account '{accountId}' was not found.", preview);
            }

            if (targetState != EconomyAccountState.Active && targetState != EconomyAccountState.Frozen && targetState != EconomyAccountState.Closed)
            {
                return Fail(EconomyResultCode.InvalidRequest, $"Account state '{targetState}' is not supported.", preview);
            }

            if (!preview && IsDuplicate(transactionId, "account-state", $"{accountId}:{targetState}", out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            if (account.state == targetState)
            {
                return EconomyOperationResult.Success("Account already has the requested state.", before, before, preview: preview, duplicate: !preview, toAccount: Snapshot(account));
            }

            if (account.state == EconomyAccountState.Closed)
            {
                return Fail(EconomyResultCode.AccountClosed, $"Closed account '{accountId}' cannot transition to {targetState}.", preview);
            }

            if (targetState == EconomyAccountState.Closed && account.balanceUnits != 0L)
            {
                return Fail(EconomyResultCode.ValidationFailed, $"Account '{accountId}' must have a zero balance before closure.", preview);
            }

            if (targetState == EconomyAccountState.Closed && reservationsById.Values.Any(item => item.accountId == accountId && item.state == EconomyReservationState.Active))
            {
                return Fail(EconomyResultCode.ValidationFailed, $"Account '{accountId}' has active reservations and cannot close.", preview);
            }

            if (preview)
            {
                EconomyAccountData projected = account.Clone();
                projected.state = targetState;
                return EconomyOperationResult.Success("Account state preview succeeded.", before, before, preview: true, toAccount: Snapshot(projected));
            }

            account.state = targetState;
            account.revision++;
            Revision++;
            Remember(transactionId, "account-state", $"{accountId}:{targetState}");
            return EconomyOperationResult.Success("Account state changed.", before, Revision, toAccount: Snapshot(account));
        }

        public EconomyOperationResult Issue(string transactionId, string toAccountId, MoneyAmount amount, string actorId, string reason = "", bool preview = false, double worldTime = 0d)
        {
            long before = Revision;
            if (!PrepareCredit(toAccountId, amount, out EconomyAccountData to, out EconomyOperationResult failure, preview))
            {
                return failure;
            }

            if (!preview && IsDuplicate(transactionId, "issue", toAccountId, out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            if (preview)
            {
                EconomyAccountData previewTo = to.Clone();
                previewTo.balanceUnits = checked(previewTo.balanceUnits + amount.Units);
                return EconomyOperationResult.Success("Issuance preview succeeded.", before, before, preview: true, toAccount: Snapshot(previewTo));
            }

            to.balanceUnits = checked(to.balanceUnits + amount.Units);
            to.revision++;
            EconomyTransactionSnapshot transaction = CommitTransaction(new EconomyTransactionData
            {
                transactionId = RequiredTransactionId(transactionId, "issue"),
                kind = EconomyTransactionKind.Issuance,
                currencyId = amount.CurrencyId,
                units = amount.Units,
                toAccountId = to.accountId,
                actorId = actorId ?? string.Empty,
                reason = reason ?? string.Empty,
                worldTime = Math.Max(0d, worldTime)
            }, mutateBalances: false);
            Revision++;
            Remember(transactionId, "issue", toAccountId);
            return EconomyOperationResult.Success("Currency issued.", before, Revision, toAccount: Snapshot(to), transaction: transaction);
        }

        public EconomyOperationResult Destroy(string transactionId, string fromAccountId, MoneyAmount amount, string actorId, string reason = "", bool preview = false, double worldTime = 0d)
        {
            long before = Revision;
            if (!PrepareDebit(fromAccountId, amount, string.Empty, out EconomyAccountData from, out _, out EconomyOperationResult failure, preview, requireReservation: false))
            {
                return failure;
            }

            if (!preview && IsDuplicate(transactionId, "destroy", fromAccountId, out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            if (preview)
            {
                EconomyAccountData previewFrom = from.Clone();
                previewFrom.balanceUnits -= amount.Units;
                return EconomyOperationResult.Success("Destruction preview succeeded.", before, before, preview: true, fromAccount: Snapshot(previewFrom));
            }

            from.balanceUnits -= amount.Units;
            from.revision++;
            EconomyTransactionSnapshot transaction = CommitTransaction(new EconomyTransactionData
            {
                transactionId = RequiredTransactionId(transactionId, "destroy"),
                kind = EconomyTransactionKind.Destruction,
                currencyId = amount.CurrencyId,
                units = amount.Units,
                fromAccountId = from.accountId,
                actorId = actorId ?? string.Empty,
                reason = reason ?? string.Empty,
                worldTime = Math.Max(0d, worldTime)
            });
            Revision++;
            Remember(transactionId, "destroy", fromAccountId);
            return EconomyOperationResult.Success("Currency destroyed.", before, Revision, fromAccount: Snapshot(from), transaction: transaction);
        }

        public EconomyOperationResult Reserve(string reservationId, string accountId, MoneyAmount amount, string sourceId, double worldTime = 0d, double expiresWorldTime = -1d, bool preview = false)
        {
            long before = Revision;
            if (!PrepareDebit(accountId, amount, string.Empty, out EconomyAccountData account, out _, out EconomyOperationResult failure, preview, requireReservation: false))
            {
                return failure;
            }

            if (reservationsById.ContainsKey(reservationId ?? string.Empty))
            {
                return Fail(EconomyResultCode.InvalidRequest, $"Reservation '{reservationId}' already exists.", preview);
            }

            EconomyReservationData reservation = new EconomyReservationData
            {
                reservationId = reservationId ?? string.Empty,
                accountId = account.accountId,
                currencyId = amount.CurrencyId,
                units = amount.Units,
                state = EconomyReservationState.Active,
                sourceId = sourceId ?? string.Empty,
                createdWorldTime = Math.Max(0d, worldTime),
                expiresWorldTime = expiresWorldTime,
                revision = 1L
            };

            if (!ValidateReservation(reservation, accountsById, out string reservationFailure))
            {
                return Fail(EconomyResultCode.ValidationFailed, reservationFailure, preview);
            }

            if (preview)
            {
                return EconomyOperationResult.Success("Reservation preview succeeded.", before, before, preview: true, fromAccount: Snapshot(account), reservation: reservation);
            }

            reservationsById.Add(reservation.reservationId, reservation);
            account.revision++;
            Revision++;
            return EconomyOperationResult.Success("Funds reserved.", before, Revision, fromAccount: Snapshot(account), reservation: reservation);
        }

        public EconomyOperationResult ReleaseReservation(string reservationId, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(reservationId) || !reservationsById.TryGetValue(reservationId, out EconomyReservationData reservation))
            {
                return Fail(EconomyResultCode.MissingReservation, $"Reservation '{reservationId}' was not found.", preview);
            }

            if (!preview && IsDuplicate(transactionId, "release", reservationId, out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            if (reservation.state != EconomyReservationState.Active)
            {
                return Fail(EconomyResultCode.ReservationUnavailable, $"Reservation '{reservationId}' is {reservation.state}.", preview);
            }

            if (preview)
            {
                return EconomyOperationResult.Success("Reservation release preview succeeded.", before, before, preview: true, reservation: reservation);
            }

            reservation.state = EconomyReservationState.Released;
            reservation.revision++;
            if (accountsById.TryGetValue(reservation.accountId, out EconomyAccountData account))
            {
                account.revision++;
            }

            Revision++;
            Remember(transactionId, "release", reservationId);
            return EconomyOperationResult.Success("Reservation released.", before, Revision, fromAccount: account == null ? null : Snapshot(account), reservation: reservation);
        }

        public EconomyOperationResult ExpireReservation(string reservationId, double worldTime, string transactionId = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(reservationId) || !reservationsById.TryGetValue(reservationId, out EconomyReservationData reservation))
            {
                return Fail(EconomyResultCode.MissingReservation, $"Reservation '{reservationId}' was not found.", preview);
            }

            if (!preview && IsDuplicate(transactionId, "expire-reservation", reservationId, out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            if (reservation.state != EconomyReservationState.Active)
            {
                return Fail(EconomyResultCode.ReservationUnavailable, $"Reservation '{reservationId}' is {reservation.state}.", preview);
            }

            if (reservation.expiresWorldTime < 0d || worldTime < reservation.expiresWorldTime)
            {
                return Fail(EconomyResultCode.InvalidRequest, $"Reservation '{reservationId}' has not reached its expiration boundary.", preview);
            }

            if (preview)
            {
                return EconomyOperationResult.Success("Reservation expiration preview succeeded.", before, before, preview: true, reservation: reservation);
            }

            reservation.state = EconomyReservationState.Expired;
            reservation.revision++;
            if (accountsById.TryGetValue(reservation.accountId, out EconomyAccountData account)) account.revision++;
            Revision++;
            Remember(transactionId, "expire-reservation", reservationId);
            return EconomyOperationResult.Success("Reservation expired.", before, Revision, fromAccount: account == null ? null : Snapshot(account), reservation: reservation);
        }

        public EconomyOperationResult Transfer(string transactionId, string fromAccountId, string toAccountId, MoneyAmount amount, EconomyTransactionKind kind = EconomyTransactionKind.Transfer, string reservationId = "", string actorId = "", string priceSnapshotId = "", bool preview = false, double worldTime = 0d)
        {
            long before = Revision;
            EconomyTransactionKind resolvedKind = kind == EconomyTransactionKind.Unknown ? EconomyTransactionKind.Transfer : kind;
            string transferSubject = TransferSubject(fromAccountId, toAccountId, amount, reservationId, priceSnapshotId);
            if (!preview && IsDuplicate(transactionId, resolvedKind.ToString(), transferSubject, out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            if (!PrepareDebit(fromAccountId, amount, reservationId, out EconomyAccountData from, out EconomyReservationData reservation, out EconomyOperationResult debitFailure, preview, requireReservation: false))
            {
                return debitFailure;
            }

            if (!PrepareCredit(toAccountId, amount, out EconomyAccountData to, out EconomyOperationResult creditFailure, preview))
            {
                return creditFailure;
            }

            if (string.Equals(from.accountId, to.accountId, StringComparison.Ordinal))
            {
                return Fail(EconomyResultCode.InvalidRequest, "Source and destination account cannot be the same.", preview);
            }

            if (preview)
            {
                EconomyAccountData previewFrom = from.Clone();
                EconomyAccountData previewTo = to.Clone();
                previewFrom.balanceUnits -= amount.Units;
                previewTo.balanceUnits += amount.Units;
                return EconomyOperationResult.Success("Transfer preview succeeded.", before, before, preview: true, fromAccount: Snapshot(previewFrom), toAccount: Snapshot(previewTo), reservation: reservation);
            }

            from.balanceUnits -= amount.Units;
            to.balanceUnits = checked(to.balanceUnits + amount.Units);
            from.revision++;
            to.revision++;
            if (reservation != null)
            {
                reservation.state = EconomyReservationState.Committed;
                reservation.committedTransactionId = RequiredTransactionId(transactionId, "transfer");
                reservation.revision++;
            }

            EconomyTransactionSnapshot transaction = CommitTransaction(new EconomyTransactionData
            {
                transactionId = RequiredTransactionId(transactionId, "transfer"),
                kind = resolvedKind,
                currencyId = amount.CurrencyId,
                units = amount.Units,
                fromAccountId = from.accountId,
                toAccountId = to.accountId,
                reservationId = reservationId ?? string.Empty,
                actorId = actorId ?? string.Empty,
                priceSnapshotId = priceSnapshotId ?? string.Empty,
                worldTime = Math.Max(0d, worldTime)
            }, mutateBalances: false);
            Revision++;
            Remember(transactionId, resolvedKind.ToString(), transferSubject);
            return EconomyOperationResult.Success("Transfer committed.", before, Revision, fromAccount: Snapshot(from), toAccount: Snapshot(to), reservation: reservation, transaction: transaction);
        }

        public EconomyOperationResult Refund(string transactionId, string originalTransactionId, string actorId = "", bool preview = false)
        {
            long before = Revision;
            if (string.IsNullOrWhiteSpace(originalTransactionId) || !transactionsById.TryGetValue(originalTransactionId, out EconomyTransactionData original))
            {
                return Fail(EconomyResultCode.MissingTransaction, $"Original transaction '{originalTransactionId}' was not found.", preview);
            }

            string refundSubject = TransferSubject(original.toAccountId, original.fromAccountId, new MoneyAmount(original.currencyId, original.units), string.Empty, string.Empty);
            if (!preview && IsDuplicate(transactionId, EconomyTransactionKind.Refund.ToString(), refundSubject, out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            if (original.state != EconomyTransactionState.Committed)
            {
                return Fail(EconomyResultCode.InvalidRequest, $"Original transaction '{originalTransactionId}' cannot be refunded from state {original.state}.", preview);
            }

            EconomyOperationResult result = Transfer(transactionId, original.toAccountId, original.fromAccountId, new MoneyAmount(original.currencyId, original.units), EconomyTransactionKind.Refund, actorId: actorId, preview: preview);
            if (!result.Succeeded || preview)
            {
                return result;
            }

            original.state = EconomyTransactionState.Refunded;
            original.revision++;
            transactionsById[result.Transaction.TransactionId].originalTransactionId = originalTransactionId;
            Revision++;
            return EconomyOperationResult.Success("Transaction refunded.", before, Revision, fromAccount: result.FromAccount, toAccount: result.ToAccount, transaction: result.Transaction);
        }

        public EconomyOperationResult Reverse(string transactionId, string originalTransactionId, string actorId = "", bool preview = false)
        {
            EconomyOperationResult result = Refund(transactionId, originalTransactionId, actorId, preview);
            if (!result.Succeeded || preview)
            {
                return result;
            }

            if (transactionsById.TryGetValue(result.Transaction.TransactionId, out EconomyTransactionData reversal))
            {
                reversal.kind = EconomyTransactionKind.Reversal;
            }

            if (transactionsById.TryGetValue(originalTransactionId, out EconomyTransactionData original))
            {
                original.state = EconomyTransactionState.Reversed;
            }

            return result;
        }

        public EconomyOperationResult CaptureFixedPrice(string priceSnapshotId, string pricedSubjectId, string sellerAccountId, MoneyAmount amount, string sourcePriceListId, string capturedBy, double worldTime = 0d, bool preview = false)
        {
            long before = Revision;
            if (!amount.IsPositive)
            {
                return Fail(EconomyResultCode.InvalidRequest, "Price amount must be positive.", preview);
            }

            if (!ValidateCurrency(amount.CurrencyId, out string currencyFailure))
            {
                return Fail(EconomyResultCode.MissingCurrency, currencyFailure, preview);
            }

            if (!string.IsNullOrWhiteSpace(sellerAccountId) && (!accountsById.TryGetValue(sellerAccountId, out EconomyAccountData seller) || !string.Equals(seller.currencyId, amount.CurrencyId, StringComparison.Ordinal)))
            {
                return Fail(EconomyResultCode.MissingAccount, $"Seller account '{sellerAccountId}' is missing or uses another currency.", preview);
            }

            FixedPriceSnapshotData snapshot = new FixedPriceSnapshotData
            {
                priceSnapshotId = priceSnapshotId ?? string.Empty,
                pricedSubjectId = pricedSubjectId ?? string.Empty,
                sellerAccountId = sellerAccountId ?? string.Empty,
                currencyId = amount.CurrencyId,
                units = amount.Units,
                sourcePriceListId = sourcePriceListId ?? string.Empty,
                capturedBy = capturedBy ?? string.Empty,
                capturedWorldTime = Math.Max(0d, worldTime)
            };
            if (string.IsNullOrWhiteSpace(snapshot.priceSnapshotId) || string.IsNullOrWhiteSpace(snapshot.pricedSubjectId))
            {
                return Fail(EconomyResultCode.InvalidRequest, "Price snapshot ID and priced subject ID are required.", preview);
            }

            if (preview)
            {
                return EconomyOperationResult.Success("Fixed price preview succeeded.", before, before, preview: true, priceSnapshot: snapshot);
            }

            pricesById[snapshot.priceSnapshotId] = snapshot;
            Revision++;
            return EconomyOperationResult.Success("Fixed price snapshot captured.", before, Revision, priceSnapshot: snapshot);
        }

        public EconomyOperationResult ConvertPhysicalToAbstract(string transactionId, string toAccountId, CurrencyDefinition currency, ItemInstanceIdentityRuntime items, string itemInstanceId, int quantity, string actorId = "", bool preview = false)
        {
            long before = Revision;
            if (currency == null || !currency.PhysicalCurrencyAllowed)
            {
                return Fail(EconomyResultCode.MissingCurrency, "Currency does not allow physical conversion.", preview);
            }

            if (currency.PhysicalCurrencyItem == null)
            {
                return Fail(EconomyResultCode.InvalidRequest, $"Currency '{currency.Id}' has no physical currency item definition.", preview);
            }

            if (items == null || string.IsNullOrWhiteSpace(itemInstanceId) || !items.TryGetSnapshot(itemInstanceId, out ItemInstanceSnapshot item))
            {
                return Fail(EconomyResultCode.MissingItemInstance, $"Item instance '{itemInstanceId}' was not found.", preview);
            }

            if (item.LifecycleState == ItemLifecycleState.Consumed || item.LifecycleState == ItemLifecycleState.Destroyed)
            {
                return Fail(EconomyResultCode.MissingItemInstance, $"Item instance '{itemInstanceId}' is no longer active.", preview);
            }

            string subject = $"{toAccountId}:{itemInstanceId}";
            if (!preview && IsDuplicate(transactionId, "physical-to-abstract", subject, out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            string requiredItemId = currency.PhysicalCurrencyItem.Id;
            if (!string.Equals(requiredItemId, item.ItemDefinitionId, StringComparison.Ordinal))
            {
                return Fail(EconomyResultCode.InvalidRequest, $"Item instance '{itemInstanceId}' is '{item.ItemDefinitionId}', not physical currency item '{requiredItemId}'.", preview);
            }

            long units = checked(Math.Max(1, quantity) * currency.UnitsPerPhysicalItem);
            MoneyAmount amount = new MoneyAmount(currency.Id, units);
            if (!PrepareCredit(toAccountId, amount, out EconomyAccountData to, out EconomyOperationResult failure, preview))
            {
                return failure;
            }

            if (preview)
            {
                EconomyAccountData previewTo = to.Clone();
                previewTo.balanceUnits = checked(previewTo.balanceUnits + amount.Units);
                return EconomyOperationResult.Success("Physical-to-abstract conversion preview succeeded.", before, before, preview: true, toAccount: Snapshot(previewTo));
            }

            ItemInstanceOperationResult consume = items.DestroyOrConsume(itemInstanceId, consumed: true);
            if (!consume.Succeeded)
            {
                return Fail(EconomyResultCode.MissingItemInstance, consume.Message, preview);
            }

            to.balanceUnits = checked(to.balanceUnits + amount.Units);
            to.revision++;
            EconomyTransactionSnapshot transaction = CommitTransaction(new EconomyTransactionData
            {
                transactionId = RequiredTransactionId(transactionId, "physical-to-abstract"),
                kind = EconomyTransactionKind.PhysicalToAbstract,
                currencyId = currency.Id,
                units = units,
                toAccountId = to.accountId,
                itemInstanceId = itemInstanceId,
                itemDefinitionId = item.ItemDefinitionId,
                itemQuantity = Math.Max(1, quantity),
                actorId = actorId ?? string.Empty,
                reason = "Physical currency converted to account balance."
            });
            Revision++;
            Remember(transactionId, "physical-to-abstract", subject);
            return EconomyOperationResult.Success("Physical currency converted to account balance.", before, Revision, toAccount: Snapshot(to), transaction: transaction);
        }

        public EconomyOperationResult ConvertAbstractToPhysical(string transactionId, string fromAccountId, CurrencyDefinition currency, ItemInstanceIdentityRuntime items, int quantity, string actorId = "", string createdItemInstanceId = "", bool preview = false)
        {
            long before = Revision;
            if (currency == null || !currency.PhysicalCurrencyAllowed || currency.PhysicalCurrencyItem == null)
            {
                return Fail(EconomyResultCode.MissingCurrency, "Currency does not define a physical currency item.", preview);
            }

            if (items == null)
            {
                return Fail(EconomyResultCode.MissingItemInstance, "Item instance runtime is required for physical currency conversion.", preview);
            }

            quantity = Math.Max(1, quantity);
            MoneyAmount amount = new MoneyAmount(currency.Id, checked(quantity * currency.UnitsPerPhysicalItem));
            if (!PrepareDebit(fromAccountId, amount, string.Empty, out EconomyAccountData from, out _, out EconomyOperationResult failure, preview, requireReservation: false))
            {
                return failure;
            }

            if (!preview && IsDuplicate(transactionId, "abstract-to-physical", fromAccountId, out EconomyOperationResult duplicate))
            {
                return duplicate;
            }

            if (preview)
            {
                EconomyAccountData previewFrom = from.Clone();
                previewFrom.balanceUnits -= amount.Units;
                return EconomyOperationResult.Success("Abstract-to-physical conversion preview succeeded.", before, before, preview: true, fromAccount: Snapshot(previewFrom));
            }

            ItemInstanceOperationResult created = items.CreateItem(currency.PhysicalCurrencyItem, ItemInstanceClassification.Fungible, itemInstanceId: string.IsNullOrWhiteSpace(createdItemInstanceId) ? ItemInstanceId.Generate() : createdItemInstanceId, creatorPersonId: actorId, ownerPersonId: actorId, custodianPersonId: actorId, creationSourceId: transactionId);
            if (!created.Succeeded)
            {
                return Fail(EconomyResultCode.ValidationFailed, created.Message, preview);
            }

            from.balanceUnits -= amount.Units;
            from.revision++;
            EconomyTransactionSnapshot transaction = CommitTransaction(new EconomyTransactionData
            {
                transactionId = RequiredTransactionId(transactionId, "abstract-to-physical"),
                kind = EconomyTransactionKind.AbstractToPhysical,
                currencyId = currency.Id,
                units = amount.Units,
                fromAccountId = from.accountId,
                itemInstanceId = created.Snapshot.ItemInstanceId,
                itemDefinitionId = currency.PhysicalCurrencyItem.Id,
                itemQuantity = quantity,
                actorId = actorId ?? string.Empty,
                reason = "Account balance converted to physical currency."
            });
            Revision++;
            Remember(transactionId, "abstract-to-physical", fromAccountId);
            return EconomyOperationResult.Success("Account balance converted to physical currency.", before, Revision, fromAccount: Snapshot(from), transaction: transaction);
        }

        public bool TryGetAccount(string accountId, out EconomyAccountSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(accountId) && accountsById.TryGetValue(accountId, out EconomyAccountData account))
            {
                snapshot = Snapshot(account);
                return true;
            }

            snapshot = null;
            return false;
        }

        public bool TryGetTransaction(string transactionId, out EconomyTransactionSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(transactionId) && transactionsById.TryGetValue(transactionId, out EconomyTransactionData transaction))
            {
                snapshot = Snapshot(transaction);
                return true;
            }

            snapshot = null;
            return false;
        }

        public InformationAccessProjection<EconomyAccountSnapshot> GetAccountProjection(string accountId, InformationAccessRuntime access, InformationAccessContext context, string policyId = "")
        {
            if (!TryGetAccount(accountId, out EconomyAccountSnapshot snapshot))
            {
                return new InformationAccessProjection<EconomyAccountSnapshot>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, $"Economy account '{accountId}' was not found.");
            }

            string[] details = { "detail.account", "detail.owner", "detail.balance", "detail.reserved", "detail.currency" };
            InformationAccessContext request = InformationAccessProjectionUtility.BuildContext(context, snapshot.CreateInformationSubject(), InformationAccessMode.Inspect, InformationAccessPurpose.Gameplay, details, policyId);
            RedactedInformationProjection projection = access?.Project(request, details);
            if (projection == null)
            {
                return new InformationAccessProjection<EconomyAccountSnapshot>(null, null, new Dictionary<string, InformationRedactionState>(), string.Empty, "Information access runtime is missing.");
            }

            EconomyAccountData projected = snapshot.Data.Clone();
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.owner")) projected.ownerId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.currency")) projected.currencyId = string.Empty;
            if (!InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.balance")) projected.balanceUnits = 0L;
            return new InformationAccessProjection<EconomyAccountSnapshot>(new EconomyAccountSnapshot(projected, InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.reserved") ? snapshot.ReservedUnits : 0L), projection.Decision, projection.Details, InformationAccessProjectionUtility.IsVisible(projection.Details, "detail.account") ? snapshot.AccountId : string.Empty, projection.Decision.VisibleReason);
        }

        public EconomyRuntimeSaveData CreateSaveData()
        {
            return new EconomyRuntimeSaveData
            {
                schemaVersion = EconomyRuntimeSaveData.CurrentSchemaVersion,
                revision = Revision,
                nextSequence = nextSequence,
                accounts = accountsById.Values.OrderBy(account => account.accountId, StringComparer.Ordinal).Select(account => account.Clone()).ToList(),
                reservations = reservationsById.Values.OrderBy(reservation => reservation.reservationId, StringComparer.Ordinal).Select(reservation => reservation.Clone()).ToList(),
                transactions = transactionsById.Values.OrderBy(transaction => transaction.sequence).ThenBy(transaction => transaction.transactionId, StringComparer.Ordinal).Select(transaction => transaction.Clone()).ToList(),
                ledgerEntries = ledgerEntries.OrderBy(entry => entry.sequence).ThenBy(entry => entry.entryId, StringComparer.Ordinal).Select(entry => entry.Clone()).ToList(),
                priceSnapshots = pricesById.Values.OrderBy(price => price.priceSnapshotId, StringComparer.Ordinal).Select(price => price.Clone()).ToList(),
                processedTransactions = processedByTransactionId.Values.OrderBy(item => item.transactionId, StringComparer.Ordinal).Select(item => item.Clone()).ToList()
            };
        }

        public EconomyOperationResult RestoreFromSaveData(EconomyRuntimeSaveData saveData, DefinitionRegistry definitionRegistry)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, out string failure))
            {
                return Fail(EconomyResultCode.RestoreFailed, failure, preview: false);
            }

            accountsById.Clear();
            reservationsById.Clear();
            transactionsById.Clear();
            pricesById.Clear();
            processedByTransactionId.Clear();
            ledgerEntries.Clear();
            foreach (EconomyAccountData account in saveData.accounts.Select(item => item.Clone()).OrderBy(item => item.accountId, StringComparer.Ordinal))
            {
                accountsById.Add(account.accountId, account);
            }

            foreach (EconomyReservationData reservation in saveData.reservations.Select(item => item.Clone()).OrderBy(item => item.reservationId, StringComparer.Ordinal))
            {
                reservationsById.Add(reservation.reservationId, reservation);
            }

            foreach (EconomyTransactionData transaction in saveData.transactions.Select(item => item.Clone()).OrderBy(item => item.sequence).ThenBy(item => item.transactionId, StringComparer.Ordinal))
            {
                transactionsById.Add(transaction.transactionId, transaction);
            }

            ledgerEntries.AddRange(saveData.ledgerEntries.Select(item => item.Clone()).OrderBy(item => item.sequence).ThenBy(item => item.entryId, StringComparer.Ordinal));
            foreach (FixedPriceSnapshotData price in saveData.priceSnapshots.Select(item => item.Clone()).OrderBy(item => item.priceSnapshotId, StringComparer.Ordinal))
            {
                pricesById.Add(price.priceSnapshotId, price);
            }

            foreach (EconomyProcessedTransactionData processed in saveData.processedTransactions.Select(item => item.Clone()).OrderBy(item => item.transactionId, StringComparer.Ordinal))
            {
                processedByTransactionId.Add(processed.transactionId, processed);
            }

            Revision = saveData.revision;
            nextSequence = Math.Max(1L, saveData.nextSequence);
            return EconomyOperationResult.Success("Economy restored.", Revision, Revision);
        }

        public static bool ValidateSaveData(EconomyRuntimeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Economy payload is missing.";
                return false;
            }

            if (saveData.schemaVersion != EconomyRuntimeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported economy schema version {saveData.schemaVersion}.";
                return false;
            }

            Dictionary<string, EconomyAccountData> accounts = new Dictionary<string, EconomyAccountData>(StringComparer.Ordinal);
            foreach (EconomyAccountData account in saveData.accounts ?? new List<EconomyAccountData>())
            {
                if (!ValidateAccount(account, registry, out failure) || accounts.ContainsKey(account.accountId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate economy account '{account?.accountId}'." : failure;
                    return false;
                }

                accounts.Add(account.accountId, account);
            }

            HashSet<string> reservations = new HashSet<string>(StringComparer.Ordinal);
            foreach (EconomyReservationData reservation in saveData.reservations ?? new List<EconomyReservationData>())
            {
                if (!ValidateReservation(reservation, accounts, out failure) || !reservations.Add(reservation.reservationId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate reservation '{reservation?.reservationId}'." : failure;
                    return false;
                }
            }

            HashSet<string> transactions = new HashSet<string>(StringComparer.Ordinal);
            foreach (EconomyTransactionData transaction in saveData.transactions ?? new List<EconomyTransactionData>())
            {
                if (!ValidateTransaction(transaction, accounts, out failure) || !transactions.Add(transaction.transactionId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate transaction '{transaction?.transactionId}'." : failure;
                    return false;
                }
            }

            foreach (EconomyLedgerEntryData entry in saveData.ledgerEntries ?? new List<EconomyLedgerEntryData>())
            {
                if (!ValidateLedgerEntry(entry, accounts, transactions, out failure))
                {
                    return false;
                }
            }

            foreach (IGrouping<string, EconomyLedgerEntryData> group in (saveData.ledgerEntries ?? new List<EconomyLedgerEntryData>()).GroupBy(entry => entry.transactionId, StringComparer.Ordinal))
            {
                long debits = group.Where(entry => entry.kind == EconomyLedgerEntryKind.Debit).Sum(entry => entry.units);
                long credits = group.Where(entry => entry.kind == EconomyLedgerEntryKind.Credit).Sum(entry => entry.units);
                if (saveData.transactions.FirstOrDefault(item => item.transactionId == group.Key) is EconomyTransactionData transaction
                    && !AllowsNonConservedLedger(transaction.kind)
                    && debits != credits)
                {
                    failure = $"Transaction '{group.Key}' ledger is not conserved.";
                    return false;
                }
            }

            HashSet<string> priceSnapshots = new HashSet<string>(StringComparer.Ordinal);
            foreach (FixedPriceSnapshotData price in saveData.priceSnapshots ?? new List<FixedPriceSnapshotData>())
            {
                if (!ValidateFixedPriceSnapshot(price, accounts, registry, out failure) || !priceSnapshots.Add(price.priceSnapshotId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate fixed price snapshot '{price?.priceSnapshotId}'." : failure;
                    return false;
                }
            }

            HashSet<string> processedTransactions = new HashSet<string>(StringComparer.Ordinal);
            foreach (EconomyProcessedTransactionData processed in saveData.processedTransactions ?? new List<EconomyProcessedTransactionData>())
            {
                if (!ValidateProcessedTransaction(processed, out failure) || !processedTransactions.Add(processed.transactionId))
                {
                    failure = string.IsNullOrWhiteSpace(failure) ? $"Duplicate processed transaction '{processed?.transactionId}'." : failure;
                    return false;
                }
            }

            return true;
        }

        private EconomyTransactionSnapshot CommitTransaction(EconomyTransactionData transaction, bool mutateBalances = false)
        {
            transaction.transactionId = RequiredTransactionId(transaction.transactionId, transaction.kind.ToString());
            transaction.state = EconomyTransactionState.Committed;
            transaction.sequence = nextSequence++;
            transactionsById.Add(transaction.transactionId, transaction);
            if (!string.IsNullOrWhiteSpace(transaction.fromAccountId))
            {
                ledgerEntries.Add(Ledger(transaction, transaction.fromAccountId, EconomyLedgerEntryKind.Debit));
            }

            if (!string.IsNullOrWhiteSpace(transaction.toAccountId))
            {
                ledgerEntries.Add(Ledger(transaction, transaction.toAccountId, EconomyLedgerEntryKind.Credit));
            }

            return Snapshot(transaction);
        }

        private EconomyLedgerEntryData Ledger(EconomyTransactionData transaction, string accountId, EconomyLedgerEntryKind kind)
        {
            accountsById.TryGetValue(accountId, out EconomyAccountData account);
            return new EconomyLedgerEntryData
            {
                entryId = $"ledger.{transaction.transactionId}.{kind.ToString().ToLowerInvariant()}",
                transactionId = transaction.transactionId,
                accountId = accountId,
                currencyId = transaction.currencyId,
                units = transaction.units,
                kind = kind,
                sequence = nextSequence++,
                accountRevision = account?.revision ?? 0L
            };
        }

        private bool PrepareDebit(string accountId, MoneyAmount amount, string reservationId, out EconomyAccountData account, out EconomyReservationData reservation, out EconomyOperationResult failure, bool preview, bool requireReservation)
        {
            account = null;
            reservation = null;
            failure = null;
            if (!amount.IsPositive)
            {
                failure = Fail(EconomyResultCode.InvalidRequest, "Amount must be positive.", preview);
                return false;
            }

            if (!ValidateCurrency(amount.CurrencyId, out string currencyFailure))
            {
                failure = Fail(EconomyResultCode.MissingCurrency, currencyFailure, preview);
                return false;
            }

            if (string.IsNullOrWhiteSpace(accountId) || !accountsById.TryGetValue(accountId, out account))
            {
                failure = Fail(EconomyResultCode.MissingAccount, $"Account '{accountId}' was not found.", preview);
                return false;
            }

            if (!CanUseAccount(account, out EconomyResultCode accountCode, out string accountFailure))
            {
                failure = Fail(accountCode, accountFailure, preview);
                return false;
            }

            if (!string.Equals(account.currencyId, amount.CurrencyId, StringComparison.Ordinal))
            {
                failure = Fail(EconomyResultCode.CurrencyMismatch, $"Account '{accountId}' uses '{account.currencyId}', not '{amount.CurrencyId}'.", preview);
                return false;
            }

            if (!string.IsNullOrWhiteSpace(reservationId))
            {
                if (!reservationsById.TryGetValue(reservationId, out reservation) || reservation.state != EconomyReservationState.Active)
                {
                    failure = Fail(EconomyResultCode.ReservationUnavailable, $"Reservation '{reservationId}' is not active.", preview);
                    return false;
                }

                if (!string.Equals(reservation.accountId, accountId, StringComparison.Ordinal) || !string.Equals(reservation.currencyId, amount.CurrencyId, StringComparison.Ordinal) || reservation.units < amount.Units)
                {
                    failure = Fail(EconomyResultCode.ReservationUnavailable, $"Reservation '{reservationId}' does not cover the requested debit.", preview);
                    return false;
                }
            }
            else if (requireReservation)
            {
                failure = Fail(EconomyResultCode.MissingReservation, "A reservation is required.", preview);
                return false;
            }

            long available = Snapshot(account).AvailableUnits;
            if (reservation != null)
            {
                available += reservation.units;
            }

            if (available < amount.Units)
            {
                failure = Fail(EconomyResultCode.InsufficientFunds, $"Account '{accountId}' has {available} available {amount.CurrencyId}, needs {amount.Units}.", preview);
                return false;
            }

            return true;
        }

        private bool PrepareCredit(string accountId, MoneyAmount amount, out EconomyAccountData account, out EconomyOperationResult failure, bool preview)
        {
            account = null;
            failure = null;
            if (!amount.IsPositive)
            {
                failure = Fail(EconomyResultCode.InvalidRequest, "Amount must be positive.", preview);
                return false;
            }

            if (!ValidateCurrency(amount.CurrencyId, out string currencyFailure))
            {
                failure = Fail(EconomyResultCode.MissingCurrency, currencyFailure, preview);
                return false;
            }

            if (string.IsNullOrWhiteSpace(accountId) || !accountsById.TryGetValue(accountId, out account))
            {
                failure = Fail(EconomyResultCode.MissingAccount, $"Account '{accountId}' was not found.", preview);
                return false;
            }

            if (!CanUseAccount(account, out EconomyResultCode accountCode, out string accountFailure))
            {
                failure = Fail(accountCode, accountFailure, preview);
                return false;
            }

            if (!string.Equals(account.currencyId, amount.CurrencyId, StringComparison.Ordinal))
            {
                failure = Fail(EconomyResultCode.CurrencyMismatch, $"Account '{accountId}' uses '{account.currencyId}', not '{amount.CurrencyId}'.", preview);
                return false;
            }

            return true;
        }

        private bool ValidateCurrency(CurrencyDefinition currency, out string failure) => ValidateCurrency(currency == null ? string.Empty : currency.Id, out failure);

        private bool ValidateCurrency(string currencyId, out string failure)
        {
            failure = string.Empty;
            if (string.IsNullOrWhiteSpace(currencyId))
            {
                failure = "Currency ID is required.";
                return false;
            }

            if (registry != null && !registry.TryGet(currencyId, out CurrencyDefinition _))
            {
                failure = $"Currency definition '{currencyId}' was not found.";
                return false;
            }

            return true;
        }

        private static bool CanUseAccount(EconomyAccountData account, out EconomyResultCode code, out string failure)
        {
            code = EconomyResultCode.Success;
            failure = string.Empty;
            if (account.state == EconomyAccountState.Closed)
            {
                code = EconomyResultCode.AccountClosed;
                failure = $"Account '{account.accountId}' is closed.";
                return false;
            }

            if (account.state == EconomyAccountState.Frozen)
            {
                code = EconomyResultCode.AccountFrozen;
                failure = $"Account '{account.accountId}' is frozen.";
                return false;
            }

            return true;
        }

        private EconomyAccountSnapshot Snapshot(EconomyAccountData account)
        {
            long reserved = reservationsById.Values
                .Where(reservation => reservation.state == EconomyReservationState.Active && string.Equals(reservation.accountId, account.accountId, StringComparison.Ordinal))
                .Sum(reservation => reservation.units);
            return new EconomyAccountSnapshot(account, reserved);
        }

        private EconomyTransactionSnapshot Snapshot(EconomyTransactionData transaction)
        {
            return new EconomyTransactionSnapshot(transaction, ledgerEntries.Where(entry => string.Equals(entry.transactionId, transaction.transactionId, StringComparison.Ordinal)));
        }

        private bool IsDuplicate(string transactionId, string operationKey, string subject, out EconomyOperationResult duplicate)
        {
            duplicate = null;
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return false;
            }

            if (!processedByTransactionId.TryGetValue(transactionId, out EconomyProcessedTransactionData processed))
            {
                return false;
            }

            if (!string.Equals(processed.operationKey, $"{operationKey}:{subject}", StringComparison.Ordinal))
            {
                duplicate = Fail(EconomyResultCode.InvalidRequest, $"Transaction ID '{transactionId}' was already used for another economy operation.", preview: false);
                return true;
            }

            EconomyTransactionSnapshot transaction = TryGetTransaction(transactionId, out EconomyTransactionSnapshot found) ? found : null;
            duplicate = EconomyOperationResult.Success("Duplicate economy transaction ignored.", Revision, Revision, duplicate: true, transaction: transaction);
            return true;
        }

        private void Remember(string transactionId, string operationKey, string subject)
        {
            if (string.IsNullOrWhiteSpace(transactionId))
            {
                return;
            }

            processedByTransactionId[transactionId] = new EconomyProcessedTransactionData
            {
                transactionId = transactionId,
                operationKey = $"{operationKey}:{subject}",
                code = EconomyResultCode.Success,
                revision = Revision
            };
        }

        private EconomyOperationResult Fail(EconomyResultCode code, string message, bool preview)
        {
            return EconomyOperationResult.Failure(code, message, Revision, preview);
        }

        private static string RequiredTransactionId(string transactionId, string prefix)
        {
            return string.IsNullOrWhiteSpace(transactionId) ? $"economy.{prefix.ToLowerInvariant()}.{Guid.NewGuid():N}" : transactionId.Trim();
        }

        private static string TransferSubject(string fromAccountId, string toAccountId, MoneyAmount amount, string reservationId, string priceSnapshotId)
        {
            return $"{fromAccountId ?? string.Empty}>{toAccountId ?? string.Empty}:{amount.CurrencyId}:{amount.Units}:{reservationId ?? string.Empty}:{priceSnapshotId ?? string.Empty}";
        }

        private static bool ValidateAccount(EconomyAccountData account, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (account == null || string.IsNullOrWhiteSpace(account.accountId))
            {
                failure = "Economy account ID is required.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(account.currencyId) || registry != null && !registry.TryGet(account.currencyId, out CurrencyDefinition _))
            {
                failure = $"Economy account '{account.accountId}' references missing currency '{account.currencyId}'.";
                return false;
            }

            if (account.balanceUnits < 0L)
            {
                failure = $"Economy account '{account.accountId}' has a negative balance.";
                return false;
            }

            if (!Enum.IsDefined(typeof(EconomyAccountKind), account.kind) || !Enum.IsDefined(typeof(EconomyAccountState), account.state))
            {
                failure = $"Economy account '{account.accountId}' has invalid enum state.";
                return false;
            }

            return true;
        }

        private static bool ValidateReservation(EconomyReservationData reservation, IReadOnlyDictionary<string, EconomyAccountData> accounts, out string failure)
        {
            failure = string.Empty;
            if (reservation == null || string.IsNullOrWhiteSpace(reservation.reservationId))
            {
                failure = "Reservation ID is required.";
                return false;
            }

            if (reservation.units <= 0L)
            {
                failure = $"Reservation '{reservation.reservationId}' amount must be positive.";
                return false;
            }

            if (accounts == null || !accounts.TryGetValue(reservation.accountId ?? string.Empty, out EconomyAccountData account))
            {
                failure = $"Reservation '{reservation.reservationId}' references missing account '{reservation.accountId}'.";
                return false;
            }

            if (!string.Equals(account.currencyId, reservation.currencyId, StringComparison.Ordinal))
            {
                failure = $"Reservation '{reservation.reservationId}' currency does not match its account.";
                return false;
            }

            if (!Enum.IsDefined(typeof(EconomyReservationState), reservation.state))
            {
                failure = $"Reservation '{reservation.reservationId}' has invalid state.";
                return false;
            }

            return true;
        }

        private static bool ValidateTransaction(EconomyTransactionData transaction, IReadOnlyDictionary<string, EconomyAccountData> accounts, out string failure)
        {
            failure = string.Empty;
            if (transaction == null || string.IsNullOrWhiteSpace(transaction.transactionId))
            {
                failure = "Economy transaction ID is required.";
                return false;
            }

            if (transaction.units <= 0L || string.IsNullOrWhiteSpace(transaction.currencyId))
            {
                failure = $"Transaction '{transaction.transactionId}' has invalid amount.";
                return false;
            }

            if (!Enum.IsDefined(typeof(EconomyTransactionKind), transaction.kind) || !Enum.IsDefined(typeof(EconomyTransactionState), transaction.state))
            {
                failure = $"Transaction '{transaction.transactionId}' has invalid enum state.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(transaction.fromAccountId) && (!accounts.TryGetValue(transaction.fromAccountId, out EconomyAccountData from) || !string.Equals(from.currencyId, transaction.currencyId, StringComparison.Ordinal)))
            {
                failure = $"Transaction '{transaction.transactionId}' source account is missing or mismatched.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(transaction.toAccountId) && (!accounts.TryGetValue(transaction.toAccountId, out EconomyAccountData to) || !string.Equals(to.currencyId, transaction.currencyId, StringComparison.Ordinal)))
            {
                failure = $"Transaction '{transaction.transactionId}' destination account is missing or mismatched.";
                return false;
            }

            return true;
        }

        private static bool ValidateLedgerEntry(EconomyLedgerEntryData entry, IReadOnlyDictionary<string, EconomyAccountData> accounts, ISet<string> transactionIds, out string failure)
        {
            failure = string.Empty;
            if (entry == null || string.IsNullOrWhiteSpace(entry.entryId))
            {
                failure = "Ledger entry ID is required.";
                return false;
            }

            if (entry.units <= 0L || string.IsNullOrWhiteSpace(entry.currencyId))
            {
                failure = $"Ledger entry '{entry.entryId}' has invalid amount.";
                return false;
            }

            if (!transactionIds.Contains(entry.transactionId ?? string.Empty))
            {
                failure = $"Ledger entry '{entry.entryId}' references missing transaction '{entry.transactionId}'.";
                return false;
            }

            if (!accounts.TryGetValue(entry.accountId ?? string.Empty, out EconomyAccountData account) || !string.Equals(account.currencyId, entry.currencyId, StringComparison.Ordinal))
            {
                failure = $"Ledger entry '{entry.entryId}' references missing or mismatched account '{entry.accountId}'.";
                return false;
            }

            return true;
        }

        private static bool ValidateFixedPriceSnapshot(FixedPriceSnapshotData price, IReadOnlyDictionary<string, EconomyAccountData> accounts, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (price == null || string.IsNullOrWhiteSpace(price.priceSnapshotId) || string.IsNullOrWhiteSpace(price.pricedSubjectId))
            {
                failure = "Fixed price snapshot ID and priced subject ID are required.";
                return false;
            }

            if (price.units <= 0L || string.IsNullOrWhiteSpace(price.currencyId))
            {
                failure = $"Fixed price snapshot '{price.priceSnapshotId}' has an invalid amount.";
                return false;
            }

            if (registry != null && !registry.TryGet(price.currencyId, out CurrencyDefinition _))
            {
                failure = $"Fixed price snapshot '{price.priceSnapshotId}' references missing currency '{price.currencyId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(price.sellerAccountId)
                && (!accounts.TryGetValue(price.sellerAccountId, out EconomyAccountData seller)
                    || !string.Equals(seller.currencyId, price.currencyId, StringComparison.Ordinal)))
            {
                failure = $"Fixed price snapshot '{price.priceSnapshotId}' references a missing or mismatched seller account.";
                return false;
            }

            return true;
        }

        private static bool ValidateProcessedTransaction(EconomyProcessedTransactionData processed, out string failure)
        {
            failure = string.Empty;
            if (processed == null || string.IsNullOrWhiteSpace(processed.transactionId) || string.IsNullOrWhiteSpace(processed.operationKey))
            {
                failure = "Processed transaction entries require a transaction ID and operation key.";
                return false;
            }

            if (!Enum.IsDefined(typeof(EconomyResultCode), processed.code))
            {
                failure = $"Processed transaction '{processed.transactionId}' has an invalid result code.";
                return false;
            }

            return true;
        }

        private static bool AllowsNonConservedLedger(EconomyTransactionKind kind)
        {
            return kind == EconomyTransactionKind.Issuance
                || kind == EconomyTransactionKind.Destruction
                || kind == EconomyTransactionKind.PhysicalToAbstract
                || kind == EconomyTransactionKind.AbstractToPhysical;
        }
    }
}
