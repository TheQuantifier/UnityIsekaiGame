using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.Knowledge.Access;

namespace UnityIsekaiGame.Economy
{
    [Serializable]
    public sealed class MoneyAmountData
    {
        public string currencyId;
        public long units;

        public MoneyAmountData Clone()
        {
            return new MoneyAmountData
            {
                currencyId = currencyId ?? string.Empty,
                units = units
            };
        }
    }

    public readonly struct MoneyAmount : IEquatable<MoneyAmount>, IComparable<MoneyAmount>
    {
        public MoneyAmount(string currencyId, long units)
        {
            CurrencyId = currencyId ?? string.Empty;
            Units = units;
        }

        public string CurrencyId { get; }
        public long Units { get; }
        public bool IsPositive => Units > 0L && !string.IsNullOrWhiteSpace(CurrencyId);
        public MoneyAmountData ToData() => new MoneyAmountData { currencyId = CurrencyId, units = Units };
        public static MoneyAmount FromData(MoneyAmountData data) => new MoneyAmount(data?.currencyId, data?.units ?? 0L);

        public MoneyAmount Add(MoneyAmount other)
        {
            EnsureSameCurrency(other);
            return new MoneyAmount(CurrencyId, checked(Units + other.Units));
        }

        public MoneyAmount Subtract(MoneyAmount other)
        {
            EnsureSameCurrency(other);
            return new MoneyAmount(CurrencyId, checked(Units - other.Units));
        }

        public int CompareTo(MoneyAmount other)
        {
            EnsureSameCurrency(other);
            return Units.CompareTo(other.Units);
        }

        public bool Equals(MoneyAmount other)
        {
            return string.Equals(CurrencyId, other.CurrencyId, StringComparison.Ordinal) && Units == other.Units;
        }

        public override bool Equals(object obj) => obj is MoneyAmount other && Equals(other);
        public override int GetHashCode() => ((CurrencyId ?? string.Empty).GetHashCode() * 397) ^ Units.GetHashCode();
        public override string ToString() => $"{CurrencyId}:{Units}";

        private void EnsureSameCurrency(MoneyAmount other)
        {
            if (!string.Equals(CurrencyId, other.CurrencyId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Cannot combine money in '{CurrencyId}' with '{other.CurrencyId}'.");
            }
        }
    }

    [Serializable]
    public sealed class EconomyAccountData
    {
        public string accountId;
        public string ownerId;
        public EconomyAccountKind kind = EconomyAccountKind.PersonWallet;
        public EconomyAccountState state = EconomyAccountState.Active;
        public string currencyId;
        public long balanceUnits;
        public string accessPolicyId;
        public long revision = 1L;

        public EconomyAccountData Clone()
        {
            return new EconomyAccountData
            {
                accountId = accountId ?? string.Empty,
                ownerId = ownerId ?? string.Empty,
                kind = kind,
                state = state,
                currencyId = currencyId ?? string.Empty,
                balanceUnits = balanceUnits,
                accessPolicyId = accessPolicyId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EconomyReservationData
    {
        public string reservationId;
        public string accountId;
        public string currencyId;
        public long units;
        public EconomyReservationState state = EconomyReservationState.Active;
        public string reason;
        public string sourceId;
        public double createdWorldTime;
        public double expiresWorldTime = -1d;
        public string committedTransactionId;
        public long revision = 1L;

        public EconomyReservationData Clone()
        {
            return new EconomyReservationData
            {
                reservationId = reservationId ?? string.Empty,
                accountId = accountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                state = state,
                reason = reason ?? string.Empty,
                sourceId = sourceId ?? string.Empty,
                createdWorldTime = createdWorldTime,
                expiresWorldTime = expiresWorldTime,
                committedTransactionId = committedTransactionId ?? string.Empty,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EconomyLedgerEntryData
    {
        public string entryId;
        public string transactionId;
        public string accountId;
        public string currencyId;
        public long units;
        public EconomyLedgerEntryKind kind;
        public long sequence;
        public long accountRevision;

        public EconomyLedgerEntryData Clone()
        {
            return new EconomyLedgerEntryData
            {
                entryId = entryId ?? string.Empty,
                transactionId = transactionId ?? string.Empty,
                accountId = accountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                kind = kind,
                sequence = sequence,
                accountRevision = accountRevision
            };
        }
    }

    [Serializable]
    public sealed class EconomyTransactionData
    {
        public string transactionId;
        public EconomyTransactionKind kind = EconomyTransactionKind.Transfer;
        public EconomyTransactionState state = EconomyTransactionState.Committed;
        public string currencyId;
        public long units;
        public string fromAccountId;
        public string toAccountId;
        public string reservationId;
        public string originalTransactionId;
        public string itemInstanceId;
        public string itemDefinitionId;
        public int itemQuantity;
        public string priceSnapshotId;
        public string actorId;
        public string reason;
        public double worldTime;
        public long sequence;
        public long revision = 1L;

        public EconomyTransactionData Clone()
        {
            return new EconomyTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                kind = kind,
                state = state,
                currencyId = currencyId ?? string.Empty,
                units = units,
                fromAccountId = fromAccountId ?? string.Empty,
                toAccountId = toAccountId ?? string.Empty,
                reservationId = reservationId ?? string.Empty,
                originalTransactionId = originalTransactionId ?? string.Empty,
                itemInstanceId = itemInstanceId ?? string.Empty,
                itemDefinitionId = itemDefinitionId ?? string.Empty,
                itemQuantity = itemQuantity,
                priceSnapshotId = priceSnapshotId ?? string.Empty,
                actorId = actorId ?? string.Empty,
                reason = reason ?? string.Empty,
                worldTime = worldTime,
                sequence = sequence,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class FixedPriceSnapshotData
    {
        public string priceSnapshotId;
        public string pricedSubjectId;
        public string sellerAccountId;
        public string currencyId;
        public long units;
        public string sourcePriceListId;
        public string capturedBy;
        public double capturedWorldTime;
        public long revision = 1L;

        public FixedPriceSnapshotData Clone()
        {
            return new FixedPriceSnapshotData
            {
                priceSnapshotId = priceSnapshotId ?? string.Empty,
                pricedSubjectId = pricedSubjectId ?? string.Empty,
                sellerAccountId = sellerAccountId ?? string.Empty,
                currencyId = currencyId ?? string.Empty,
                units = units,
                sourcePriceListId = sourcePriceListId ?? string.Empty,
                capturedBy = capturedBy ?? string.Empty,
                capturedWorldTime = capturedWorldTime,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EconomyProcessedTransactionData
    {
        public string transactionId;
        public string operationKey;
        public EconomyResultCode code;
        public long revision;

        public EconomyProcessedTransactionData Clone()
        {
            return new EconomyProcessedTransactionData
            {
                transactionId = transactionId ?? string.Empty,
                operationKey = operationKey ?? string.Empty,
                code = code,
                revision = revision
            };
        }
    }

    [Serializable]
    public sealed class EconomyRuntimeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public long nextSequence;
        public List<EconomyAccountData> accounts = new List<EconomyAccountData>();
        public List<EconomyReservationData> reservations = new List<EconomyReservationData>();
        public List<EconomyTransactionData> transactions = new List<EconomyTransactionData>();
        public List<EconomyLedgerEntryData> ledgerEntries = new List<EconomyLedgerEntryData>();
        public List<FixedPriceSnapshotData> priceSnapshots = new List<FixedPriceSnapshotData>();
        public List<EconomyProcessedTransactionData> processedTransactions = new List<EconomyProcessedTransactionData>();

        public EconomyRuntimeSaveData Clone()
        {
            return new EconomyRuntimeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                nextSequence = nextSequence,
                accounts = accounts == null ? new List<EconomyAccountData>() : accounts.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                reservations = reservations == null ? new List<EconomyReservationData>() : reservations.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                transactions = transactions == null ? new List<EconomyTransactionData>() : transactions.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                ledgerEntries = ledgerEntries == null ? new List<EconomyLedgerEntryData>() : ledgerEntries.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                priceSnapshots = priceSnapshots == null ? new List<FixedPriceSnapshotData>() : priceSnapshots.Select(item => item?.Clone()).Where(item => item != null).ToList(),
                processedTransactions = processedTransactions == null ? new List<EconomyProcessedTransactionData>() : processedTransactions.Select(item => item?.Clone()).Where(item => item != null).ToList()
            };
        }
    }

    public sealed class EconomyAccountSnapshot
    {
        public EconomyAccountSnapshot(EconomyAccountData account, long reservedUnits)
        {
            Data = account?.Clone() ?? new EconomyAccountData();
            ReservedUnits = Math.Max(0L, reservedUnits);
        }

        public EconomyAccountData Data { get; }
        public string AccountId => Data.accountId ?? string.Empty;
        public string OwnerId => Data.ownerId ?? string.Empty;
        public string CurrencyId => Data.currencyId ?? string.Empty;
        public long BalanceUnits => Data.balanceUnits;
        public long ReservedUnits { get; }
        public long AvailableUnits => Math.Max(0L, BalanceUnits - ReservedUnits);
        public long Revision => Data.revision;

        public InformationSubjectReferenceData CreateInformationSubject()
        {
            return new InformationSubjectReferenceData
            {
                subjectType = InformationSubjectType.Ownership,
                subjectId = AccountId,
                ownerPersonId = OwnerId,
                tags = new[] { "economy.account", CurrencyId }
            };
        }
    }

    public sealed class EconomyTransactionSnapshot
    {
        public EconomyTransactionSnapshot(EconomyTransactionData transaction, IEnumerable<EconomyLedgerEntryData> ledgerEntries)
        {
            Data = transaction?.Clone() ?? new EconomyTransactionData();
            LedgerEntries = (ledgerEntries ?? Array.Empty<EconomyLedgerEntryData>()).Select(entry => entry?.Clone()).Where(entry => entry != null).ToArray();
        }

        public EconomyTransactionData Data { get; }
        public IReadOnlyList<EconomyLedgerEntryData> LedgerEntries { get; }
        public string TransactionId => Data.transactionId ?? string.Empty;
        public EconomyTransactionKind Kind => Data.kind;
        public EconomyTransactionState State => Data.state;
        public string CurrencyId => Data.currencyId ?? string.Empty;
        public long Units => Data.units;
    }

    public sealed class EconomyOperationResult
    {
        private EconomyOperationResult(bool succeeded, bool preview, bool duplicate, EconomyResultCode code, string message, long revisionBefore, long revisionAfter, EconomyAccountSnapshot fromAccount, EconomyAccountSnapshot toAccount, EconomyReservationData reservation, EconomyTransactionSnapshot transaction, FixedPriceSnapshotData priceSnapshot)
        {
            Succeeded = succeeded;
            Preview = preview;
            Duplicate = duplicate;
            Code = code;
            Message = message ?? string.Empty;
            RevisionBefore = revisionBefore;
            RevisionAfter = revisionAfter;
            FromAccount = fromAccount;
            ToAccount = toAccount;
            Reservation = reservation?.Clone();
            Transaction = transaction;
            PriceSnapshot = priceSnapshot?.Clone();
        }

        public bool Succeeded { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public EconomyResultCode Code { get; }
        public string Message { get; }
        public long RevisionBefore { get; }
        public long RevisionAfter { get; }
        public EconomyAccountSnapshot FromAccount { get; }
        public EconomyAccountSnapshot ToAccount { get; }
        public EconomyReservationData Reservation { get; }
        public EconomyTransactionSnapshot Transaction { get; }
        public FixedPriceSnapshotData PriceSnapshot { get; }

        public static EconomyOperationResult Success(string message, long before, long after, bool preview = false, bool duplicate = false, EconomyAccountSnapshot fromAccount = null, EconomyAccountSnapshot toAccount = null, EconomyReservationData reservation = null, EconomyTransactionSnapshot transaction = null, FixedPriceSnapshotData priceSnapshot = null)
        {
            return new EconomyOperationResult(true, preview, duplicate, preview ? EconomyResultCode.Preview : duplicate ? EconomyResultCode.Duplicate : EconomyResultCode.Success, message, before, after, fromAccount, toAccount, reservation, transaction, priceSnapshot);
        }

        public static EconomyOperationResult Failure(EconomyResultCode code, string message, long revision, bool preview = false)
        {
            return new EconomyOperationResult(false, preview, false, code, message, revision, revision, null, null, null, null, null);
        }
    }
}
