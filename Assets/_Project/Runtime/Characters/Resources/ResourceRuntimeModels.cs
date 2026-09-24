using System;
using System.Collections.Generic;

namespace UnityIsekaiGame.ResourceSystem
{
    public enum ResourceCategoryKind
    {
        Vital,
        Combat,
        Magical,
        Physical,
        Survival,
        Mental,
        Spiritual,
        Social,
        Corruption,
        Environmental
    }

    public enum ResourceInitializationPolicy
    {
        Full,
        Empty,
        FixedValue,
        PercentageOfMaximum,
        PreserveExisting,
        DefinitionDefault
    }

    public enum ResourceMaximumReconciliationPolicy
    {
        ClampOnly,
        PreservePercentage,
        IncreaseCurrentByMaximumIncrease,
        RefillToMaximum,
        CustomReserved
    }

    public enum ResourcePersistencePolicy
    {
        Persist,
        SessionOnly,
        DoNotPersist
    }

    public enum ResourceAuthorityKind
    {
        LocalPrototype,
        ServerAuthoritative
    }

    public enum ResourceChangeOperation
    {
        Gain,
        Spend,
        Damage,
        Heal,
        Set,
        Regenerate,
        Degenerate,
        Initialize,
        Reconcile,
        Restore,
        Administrative
    }

    public enum ResourceChangeSourceCategory
    {
        Unknown,
        Gameplay,
        Combat,
        Ability,
        Item,
        Status,
        Regeneration,
        Persistence,
        Development
    }

    [Serializable]
    public sealed class RuntimeResourceRecord
    {
        public string resourceDefinitionId;
        public float currentValue;
        public float lastKnownMaximum;
        public string initializedAtUtc;
        public double initializedAtPlaytimeSeconds;
        public string lastChangedAtUtc;
        public double lastChangedAtPlaytimeSeconds;
        public string lastChangeSource;
        public string lastChangeReason;
        public float lifetimeGained;
        public float lifetimeSpent;
        public float lifetimeDamaged;
        public float lifetimeHealed;
        public string becameEmptyAtUtc;
        public string becameFullAtUtc;
        public float regenerationBlockedUntil;
        public bool initialized;
    }

    [Serializable]
    public sealed class PlayerResourcesSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string playerId;
        public string personId;
        public List<RuntimeResourceRecord> resources = new List<RuntimeResourceRecord>();
        public List<string> processedEventIds = new List<string>();
    }

    public readonly struct ResourceSnapshot
    {
        public ResourceSnapshot(string resourceId, float current, float minimum, float maximum, bool initialized)
        {
            ResourceId = resourceId ?? string.Empty;
            Current = current;
            Minimum = minimum;
            Maximum = maximum;
            Initialized = initialized;
        }

        public string ResourceId { get; }
        public float Current { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public bool Initialized { get; }
        public float Normalized => Maximum <= Minimum ? 0f : (Current - Minimum) / (Maximum - Minimum);
        public bool IsEmpty => Current <= Minimum + CharacterResourceCollection.Epsilon;
        public bool IsFull => Current >= Maximum - CharacterResourceCollection.Epsilon;
    }

    public readonly struct ResourceChangeRequest
    {
        public ResourceChangeRequest(
            string resourceId,
            ResourceChangeOperation operation,
            float amount,
            ResourceChangeSourceCategory sourceCategory,
            string sourceId,
            string reason,
            string eventId = "",
            bool allowPartial = false,
            bool allowOverfill = false,
            bool allowUnderflow = false,
            bool restoration = false,
            bool migration = false,
            bool authorityValidated = false)
        {
            ResourceId = resourceId ?? string.Empty;
            Operation = operation;
            Amount = amount;
            SourceCategory = sourceCategory;
            SourceId = sourceId ?? string.Empty;
            Reason = reason ?? string.Empty;
            EventId = eventId ?? string.Empty;
            AllowPartial = allowPartial;
            AllowOverfill = allowOverfill;
            AllowUnderflow = allowUnderflow;
            Restoration = restoration;
            Migration = migration;
            AuthorityValidated = authorityValidated;
        }

        public string ResourceId { get; }
        public ResourceChangeOperation Operation { get; }
        public float Amount { get; }
        public ResourceChangeSourceCategory SourceCategory { get; }
        public string SourceId { get; }
        public string Reason { get; }
        public string EventId { get; }
        public bool AllowPartial { get; }
        public bool AllowOverfill { get; }
        public bool AllowUnderflow { get; }
        public bool Restoration { get; }
        public bool Migration { get; }
        public bool AuthorityValidated { get; }
    }

    public sealed class ResourceChangeResult
    {
        private ResourceChangeResult(
            bool succeeded,
            string code,
            string message,
            ResourceChangeRequest request,
            float requestedAmount,
            float appliedAmount,
            float oldCurrent,
            float newCurrent,
            float minimum,
            float maximum,
            bool clamped,
            bool partial,
            bool becameEmpty,
            bool leftEmpty,
            bool becameFull,
            bool leftFull,
            bool duplicateEvent)
        {
            Succeeded = succeeded;
            Code = string.IsNullOrWhiteSpace(code) ? succeeded ? "Success" : "Failed" : code;
            Message = message ?? string.Empty;
            Request = request;
            RequestedAmount = requestedAmount;
            AppliedAmount = appliedAmount;
            OldCurrent = oldCurrent;
            NewCurrent = newCurrent;
            Minimum = minimum;
            Maximum = maximum;
            Clamped = clamped;
            Partial = partial;
            BecameEmpty = becameEmpty;
            LeftEmpty = leftEmpty;
            BecameFull = becameFull;
            LeftFull = leftFull;
            DuplicateEvent = duplicateEvent;
        }

        public bool Succeeded { get; }
        public string Code { get; }
        public string Message { get; }
        public ResourceChangeRequest Request { get; }
        public float RequestedAmount { get; }
        public float AppliedAmount { get; }
        public float OldCurrent { get; }
        public float NewCurrent { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public bool Clamped { get; }
        public bool Partial { get; }
        public bool BecameEmpty { get; }
        public bool LeftEmpty { get; }
        public bool BecameFull { get; }
        public bool LeftFull { get; }
        public bool DuplicateEvent { get; }

        public static ResourceChangeResult Success(ResourceChangeRequest request, float requestedAmount, float appliedAmount, float oldCurrent, float newCurrent, float minimum, float maximum, bool clamped, bool partial, bool becameEmpty, bool leftEmpty, bool becameFull, bool leftFull, string message = "Resource changed.", bool duplicateEvent = false)
        {
            return new ResourceChangeResult(true, "Success", message, request, requestedAmount, appliedAmount, oldCurrent, newCurrent, minimum, maximum, clamped, partial, becameEmpty, leftEmpty, becameFull, leftFull, duplicateEvent);
        }

        public static ResourceChangeResult Failure(ResourceChangeRequest request, string code, string message, float current = 0f, float minimum = 0f, float maximum = 0f)
        {
            return new ResourceChangeResult(false, code, message, request, request.Amount, 0f, current, current, minimum, maximum, false, false, false, false, false, false, false);
        }
    }
}
