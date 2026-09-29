using System;

namespace UnityIsekaiGame.Beings.Biology.VitalProcesses
{
    public static class BiologicalResourceIds
    {
        public const string Blood = "resource.biology.blood";
        public const string Breath = "resource.biology.breath";
        public const string Temperature = "resource.biology.temperature";
        public const string Nutrition = "resource.biology.nutrition";
        public const string Hydration = "resource.biology.hydration";
        public const string SleepNeed = "resource.biology.sleep-need";
        public const string Fatigue = "resource.biology.fatigue";
    }

    public enum BiologicalResourceModelType
    {
        DepletingPool,
        AccumulatingNeed,
        TargetCenteredValue,
        Inactive
    }

    public enum VitalProcessReadinessState
    {
        Uninitialized,
        WaitingForBody,
        WaitingForAnatomy,
        WaitingForCondition,
        ResolvingProfile,
        Ready,
        Restoring,
        Invalid,
        Disposed
    }

    public enum VitalProcessState
    {
        Inactive,
        Normal,
        StrainedLow,
        StrainedHigh,
        CriticalLow,
        CriticalHigh,
        Invalid
    }

    public enum VitalResourceMutationOperation
    {
        Consume,
        Restore,
        Adjust,
        Set
    }

    public enum VitalProcessResultCode
    {
        Success,
        Preview,
        Duplicate,
        RuntimeNotReady,
        MissingActorBody,
        MissingAnatomy,
        MissingBodyCondition,
        MissingProfile,
        MissingResource,
        InactiveResource,
        InvalidRequest,
        InvalidAmount,
        InvalidRestore,
        StaleBody,
        StaleAnatomy,
        StaleCondition
    }

    [Flags]
    public enum LifecyclePressureFlags
    {
        None = 0,
        BloodCritical = 1 << 0,
        BreathCritical = 1 << 1,
        TemperatureCritical = 1 << 2,
        NutritionCritical = 1 << 3,
        HydrationCritical = 1 << 4,
        SleepNeedCritical = 1 << 5,
        FatigueCritical = 1 << 6
    }

    public readonly struct VitalResourceMutationRequest
    {
        public VitalResourceMutationRequest(
            string actorBodyId,
            string resourceId,
            VitalResourceMutationOperation operation,
            float amount,
            string transactionId = "",
            string sourceId = "",
            string reason = "",
            long expectedBodyRevision = 0L,
            long expectedAnatomyRevision = 0L,
            long expectedConditionRevision = 0L)
        {
            ActorBodyId = actorBodyId ?? string.Empty;
            ResourceId = resourceId ?? string.Empty;
            Operation = operation;
            Amount = amount;
            TransactionId = transactionId ?? string.Empty;
            SourceId = sourceId ?? string.Empty;
            Reason = reason ?? string.Empty;
            ExpectedBodyRevision = expectedBodyRevision;
            ExpectedAnatomyRevision = expectedAnatomyRevision;
            ExpectedConditionRevision = expectedConditionRevision;
        }

        public string ActorBodyId { get; }
        public string ResourceId { get; }
        public VitalResourceMutationOperation Operation { get; }
        public float Amount { get; }
        public string TransactionId { get; }
        public string SourceId { get; }
        public string Reason { get; }
        public long ExpectedBodyRevision { get; }
        public long ExpectedAnatomyRevision { get; }
        public long ExpectedConditionRevision { get; }
    }

    public sealed class VitalResourceMutationResult
    {
        private VitalResourceMutationResult(bool succeeded, VitalProcessResultCode code, string message, VitalResourceMutationRequest request, float previousValue, float newValue, float appliedAmount, VitalProcessState previousState, VitalProcessState newState, bool preview, bool duplicate, VitalProcessSnapshot snapshot)
        {
            Succeeded = succeeded;
            Code = code;
            Message = message ?? string.Empty;
            Request = request;
            PreviousValue = previousValue;
            NewValue = newValue;
            AppliedAmount = appliedAmount;
            PreviousState = previousState;
            NewState = newState;
            Preview = preview;
            Duplicate = duplicate;
            Snapshot = snapshot;
        }

        public bool Succeeded { get; }
        public VitalProcessResultCode Code { get; }
        public string Message { get; }
        public VitalResourceMutationRequest Request { get; }
        public float PreviousValue { get; }
        public float NewValue { get; }
        public float AppliedAmount { get; }
        public VitalProcessState PreviousState { get; }
        public VitalProcessState NewState { get; }
        public bool Preview { get; }
        public bool Duplicate { get; }
        public VitalProcessSnapshot Snapshot { get; }

        public static VitalResourceMutationResult Success(VitalResourceMutationRequest request, float previousValue, float newValue, float appliedAmount, VitalProcessState previousState, VitalProcessState newState, bool preview, bool duplicate, VitalProcessSnapshot snapshot)
        {
            return new VitalResourceMutationResult(true, duplicate ? VitalProcessResultCode.Duplicate : preview ? VitalProcessResultCode.Preview : VitalProcessResultCode.Success, duplicate ? "Duplicate vital resource transaction." : preview ? "Vital resource preview resolved." : "Vital resource changed.", request, previousValue, newValue, appliedAmount, previousState, newState, preview, duplicate, snapshot);
        }

        public static VitalResourceMutationResult Failure(VitalResourceMutationRequest request, VitalProcessResultCode code, string message, VitalProcessSnapshot snapshot = null)
        {
            return new VitalResourceMutationResult(false, code, message, request, 0f, 0f, 0f, VitalProcessState.Invalid, VitalProcessState.Invalid, false, false, snapshot);
        }
    }
}
