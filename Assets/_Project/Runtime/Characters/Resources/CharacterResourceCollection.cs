using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.ResourceSystem
{
    public sealed class CharacterResourceCollection : MonoBehaviour
    {
        public const float Epsilon = 0.0001f;

        [SerializeField] private CalculatedStatCollection calculatedStats;

        private readonly Dictionary<string, ResourceDefinition> definitionsById = new Dictionary<string, ResourceDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeResourceRecord> recordsById = new Dictionary<string, RuntimeResourceRecord>(StringComparer.Ordinal);
        private readonly HashSet<string> processedEventIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> suppressedAutomaticTickResourceIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> nextRegenerationTick = new Dictionary<string, float>(StringComparer.Ordinal);
        private readonly Dictionary<string, float> nextDegenerationTick = new Dictionary<string, float>(StringComparer.Ordinal);
        private string ownerId = string.Empty;
        private bool suppressNotifications;

        public event Action<CharacterResourceCollection, ResourceChangeResult> ResourceChanged;
        public event Action<CharacterResourceCollection, ResourceSnapshot, float, bool> ResourceMaximumChanged;
        public event Action<CharacterResourceCollection, ResourceSnapshot, bool> ResourceBecameEmpty;
        public event Action<CharacterResourceCollection, ResourceSnapshot, bool> ResourceLeftEmpty;
        public event Action<CharacterResourceCollection, ResourceSnapshot, bool> ResourceBecameFull;
        public event Action<CharacterResourceCollection, ResourceSnapshot, bool> ResourceLeftFull;
        public event Action<CharacterResourceCollection, bool> ResourcesRestored;

        public bool IsConfigured { get; private set; }
        public IReadOnlyCollection<RuntimeResourceRecord> ResourceRecords => recordsById.Values.Select(CloneRecord).ToList();
        public IReadOnlyCollection<ResourceDefinition> Definitions => definitionsById.Values.ToList();

        private void Awake()
        {
            if (calculatedStats == null)
            {
                calculatedStats = GetComponent<CalculatedStatCollection>();
            }

        }

        private void OnEnable()
        {
            if (calculatedStats != null)
            {
                calculatedStats.CalculatedStatsChanged += OnCalculatedStatsChanged;
            }
        }

        private void OnDisable()
        {
            if (calculatedStats != null)
            {
                calculatedStats.CalculatedStatsChanged -= OnCalculatedStatsChanged;
            }
        }

        public void Configure(DefinitionRegistry registry, CalculatedStatCollection statCollection = null, string owner = "")
        {
            Configure(registry == null
                ? Enumerable.Empty<ResourceDefinition>()
                : registry.DefinitionsById.Values.OfType<ResourceDefinition>(),
                statCollection,
                owner);
        }

        public void Configure(IEnumerable<ResourceDefinition> definitions, CalculatedStatCollection statCollection = null, string owner = "")
        {
            if (calculatedStats != null)
            {
                calculatedStats.CalculatedStatsChanged -= OnCalculatedStatsChanged;
            }

            calculatedStats = statCollection == null ? calculatedStats == null ? GetComponent<CalculatedStatCollection>() : calculatedStats : statCollection;
            ownerId = string.IsNullOrWhiteSpace(owner) ? ownerId : owner;

            List<ResourceDefinition> definitionSnapshot = (definitions ?? Enumerable.Empty<ResourceDefinition>()).ToList();
            definitionsById.Clear();
            foreach (ResourceDefinition definition in definitionSnapshot)
            {
                if (definition == null || !definition.AlphaEnabled || string.IsNullOrWhiteSpace(definition.Id) || definitionsById.ContainsKey(definition.Id))
                {
                    continue;
                }

                definitionsById.Add(definition.Id, definition);
            }

            IsConfigured = definitionsById.Count > 0;
            InitializeMissingResources(ResourceInitializationPolicy.DefinitionDefault, false, true);

            if (isActiveAndEnabled && calculatedStats != null)
            {
                calculatedStats.CalculatedStatsChanged += OnCalculatedStatsChanged;
            }
        }

        public bool HasResource(string resourceId)
        {
            return recordsById.ContainsKey(resourceId);
        }

        public void SetAutomaticResourceTickSuppressed(string resourceId, bool suppressed)
        {
            if (string.IsNullOrWhiteSpace(resourceId))
            {
                return;
            }

            if (suppressed)
            {
                suppressedAutomaticTickResourceIds.Add(resourceId);
            }
            else
            {
                suppressedAutomaticTickResourceIds.Remove(resourceId);
            }
        }

        public bool IsAutomaticResourceTickSuppressed(string resourceId)
        {
            return !string.IsNullOrWhiteSpace(resourceId) && suppressedAutomaticTickResourceIds.Contains(resourceId);
        }

        public bool TryGetResource(string resourceId, out ResourceSnapshot snapshot)
        {
            if (!definitionsById.TryGetValue(resourceId, out ResourceDefinition definition) || !recordsById.TryGetValue(resourceId, out RuntimeResourceRecord record))
            {
                snapshot = default;
                return false;
            }

            snapshot = CreateSnapshot(definition, record);
            return true;
        }

        public IReadOnlyList<ResourceSnapshot> GetSnapshots()
        {
            return definitionsById.Values
                .OrderBy(definition => definition.DisplayName)
                .Select(definition => recordsById.TryGetValue(definition.Id, out RuntimeResourceRecord record) ? CreateSnapshot(definition, record) : default)
                .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.ResourceId))
                .ToList();
        }

        public float GetCurrent(string resourceId)
        {
            return TryGetResource(resourceId, out ResourceSnapshot snapshot) ? snapshot.Current : 0f;
        }

        public float GetMaximum(string resourceId)
        {
            return TryGetResource(resourceId, out ResourceSnapshot snapshot) ? snapshot.Maximum : 0f;
        }

        public float GetMinimum(string resourceId)
        {
            return TryGetResource(resourceId, out ResourceSnapshot snapshot) ? snapshot.Minimum : 0f;
        }

        public float GetNormalized(string resourceId)
        {
            return TryGetResource(resourceId, out ResourceSnapshot snapshot) ? Mathf.Clamp01(snapshot.Normalized) : 0f;
        }

        public bool CanSpend(string resourceId, float amount)
        {
            if (!TryGetResource(resourceId, out ResourceSnapshot snapshot) || amount <= 0f)
            {
                return false;
            }

            return snapshot.Current - amount >= snapshot.Minimum - Epsilon;
        }

        public ResourceChangeResult TryGain(string resourceId, float amount, string sourceId = "", string reason = "", string eventId = "", bool allowPartial = true)
        {
            return ApplyChange(new ResourceChangeRequest(resourceId, ResourceChangeOperation.Gain, amount, ResourceChangeSourceCategory.Gameplay, sourceId, reason, eventId, allowPartial));
        }

        public ResourceChangeResult TrySpend(string resourceId, float amount, string sourceId = "", string reason = "", string eventId = "", bool allowPartial = false)
        {
            return ApplyChange(new ResourceChangeRequest(resourceId, ResourceChangeOperation.Spend, amount, ResourceChangeSourceCategory.Gameplay, sourceId, reason, eventId, allowPartial));
        }

        public ResourceChangeResult ApplyDamage(string resourceId, float amount, string sourceId = "", string reason = "", string eventId = "")
        {
            return ApplyChange(new ResourceChangeRequest(resourceId, ResourceChangeOperation.Damage, amount, ResourceChangeSourceCategory.Combat, sourceId, reason, eventId));
        }

        public ResourceChangeResult ApplyHealing(string resourceId, float amount, string sourceId = "", string reason = "", string eventId = "")
        {
            return ApplyChange(new ResourceChangeRequest(resourceId, ResourceChangeOperation.Heal, amount, ResourceChangeSourceCategory.Ability, sourceId, reason, eventId, allowPartial: true));
        }

        public ResourceChangeResult SetCurrent(string resourceId, float value, string sourceId = "", string reason = "", bool restoration = false)
        {
            return ApplyChange(new ResourceChangeRequest(resourceId, restoration ? ResourceChangeOperation.Restore : ResourceChangeOperation.Set, value, ResourceChangeSourceCategory.Persistence, sourceId, reason, restoration: restoration));
        }

        public ResourceChangeResult ApplyChange(ResourceChangeRequest request)
        {
            if (!definitionsById.TryGetValue(request.ResourceId, out ResourceDefinition definition) || !recordsById.TryGetValue(request.ResourceId, out RuntimeResourceRecord record))
            {
                return ResourceChangeResult.Failure(request, "UnknownResource", $"Resource '{request.ResourceId}' is not configured.");
            }

            float minimum = definition.MinimumValue;
            float maximum = ResolveMaximum(definition);
            if (!IsFinite(request.Amount))
            {
                return ResourceChangeResult.Failure(request, "InvalidAmount", "Resource change amount must be finite.", record.currentValue, minimum, maximum);
            }

            if (!string.IsNullOrWhiteSpace(request.EventId) && !processedEventIds.Add(request.EventId))
            {
                return ResourceChangeResult.Success(request, request.Amount, 0f, record.currentValue, record.currentValue, minimum, maximum, false, false, false, false, false, false, "Duplicate resource event ignored.", duplicateEvent: true);
            }

            if (!ValidateOperationAllowed(definition, request, out string failureReason))
            {
                return ResourceChangeResult.Failure(request, "OperationNotAllowed", failureReason, record.currentValue, minimum, maximum);
            }

            if (RequiresPositiveAmount(request.Operation) && request.Amount <= 0f)
            {
                return ResourceChangeResult.Failure(request, "InvalidAmount", "Resource change amount must be greater than zero.", record.currentValue, minimum, maximum);
            }

            float oldCurrent = record.currentValue;
            bool wasEmpty = oldCurrent <= minimum + Epsilon;
            bool wasFull = oldCurrent >= maximum - Epsilon;
            float target = CalculateTarget(definition, request, oldCurrent, minimum, maximum, out bool rejectedForInsufficient);
            if (rejectedForInsufficient)
            {
                return ResourceChangeResult.Failure(request, "InsufficientResource", $"Not enough {definition.DisplayName}.", oldCurrent, minimum, maximum);
            }

            bool clamped = false;
            float constrained = Constrain(definition, request, target, minimum, maximum, out clamped);
            float applied = Mathf.Abs(constrained - oldCurrent);
            bool partial = !Mathf.Approximately(applied, Mathf.Abs(request.Amount)) && request.Operation != ResourceChangeOperation.Set && request.Operation != ResourceChangeOperation.Restore;

            if (Mathf.Approximately(oldCurrent, constrained))
            {
                return ResourceChangeResult.Success(request, request.Amount, 0f, oldCurrent, oldCurrent, minimum, maximum, clamped, partial, false, false, false, false, $"{definition.DisplayName} did not change.");
            }

            record.currentValue = constrained;
            record.lastKnownMaximum = maximum;
            record.lastChangedAtUtc = DateTime.UtcNow.ToString("O");
            record.lastChangedAtPlaytimeSeconds = CurrentPlaytimeSeconds;
            record.lastChangeSource = request.SourceId;
            record.lastChangeReason = request.Reason;
            AddLifetime(record, request.Operation, applied);
            ApplyRegenerationDelay(definition, record, request.Operation);

            bool isEmpty = constrained <= minimum + Epsilon;
            bool isFull = constrained >= maximum - Epsilon;
            if (!wasEmpty && isEmpty)
            {
                record.becameEmptyAtUtc = record.lastChangedAtUtc;
            }

            if (!wasFull && isFull)
            {
                record.becameFullAtUtc = record.lastChangedAtUtc;
            }

            ResourceChangeResult result = ResourceChangeResult.Success(
                request,
                request.Amount,
                applied,
                oldCurrent,
                constrained,
                minimum,
                maximum,
                clamped,
                partial,
                !wasEmpty && isEmpty,
                wasEmpty && !isEmpty,
                !wasFull && isFull,
                wasFull && !isFull,
                $"{definition.DisplayName} changed from {oldCurrent:0.###} to {constrained:0.###}.");

            RaiseChangeEvents(result, request.Restoration || request.Migration);
            return result;
        }

        public void InitializeMissingResources(ResourceInitializationPolicy policy, bool restoration, bool preserveExisting)
        {
            foreach (ResourceDefinition definition in definitionsById.Values.ToList())
            {
                if (recordsById.ContainsKey(definition.Id))
                {
                    continue;
                }

                float maximum = ResolveMaximum(definition);
                float current = ResolveInitialValue(definition, policy == ResourceInitializationPolicy.DefinitionDefault ? definition.InitializationPolicy : policy, maximum, definition.MinimumValue, 0f, preserveExisting);
                RuntimeResourceRecord record = new RuntimeResourceRecord
                {
                    resourceDefinitionId = definition.Id,
                    currentValue = current,
                    lastKnownMaximum = maximum,
                    initializedAtUtc = DateTime.UtcNow.ToString("O"),
                    lastChangedAtUtc = DateTime.UtcNow.ToString("O"),
                    lastChangeSource = "resource.initialize",
                    lastChangeReason = policy.ToString(),
                    initialized = true
                };
                recordsById.Add(definition.Id, record);
                ResourceChangeRequest request = new ResourceChangeRequest(definition.Id, ResourceChangeOperation.Initialize, current, ResourceChangeSourceCategory.Persistence, "resource.initialize", "Initialize resource.", restoration: restoration);
                RaiseChangeEvents(ResourceChangeResult.Success(request, current, current, current, current, definition.MinimumValue, maximum, false, false, current <= definition.MinimumValue + Epsilon, false, current >= maximum - Epsilon, false), restoration);
            }
        }

        public void ResetToDefinitionDefaults(string sourceId = "resource.reset", string reason = "Reset resources.", bool restoration = true)
        {
            processedEventIds.Clear();
            nextRegenerationTick.Clear();
            nextDegenerationTick.Clear();

            foreach (ResourceDefinition definition in definitionsById.Values.ToList())
            {
                float maximum = ResolveMaximum(definition);
                float current = ResolveInitialValue(definition, definition.InitializationPolicy, maximum, definition.MinimumValue, 0f, false);
                if (!recordsById.TryGetValue(definition.Id, out RuntimeResourceRecord record))
                {
                    record = new RuntimeResourceRecord
                    {
                        resourceDefinitionId = definition.Id,
                        initializedAtUtc = DateTime.UtcNow.ToString("O"),
                        initialized = true
                    };
                    recordsById.Add(definition.Id, record);
                }

                float oldCurrent = record.currentValue;
                record.currentValue = current;
                record.lastKnownMaximum = maximum;
                record.lastChangedAtUtc = DateTime.UtcNow.ToString("O");
                record.lastChangedAtPlaytimeSeconds = CurrentPlaytimeSeconds;
                record.lastChangeSource = sourceId ?? string.Empty;
                record.lastChangeReason = reason ?? string.Empty;
                record.lifetimeGained = 0f;
                record.lifetimeSpent = 0f;
                record.lifetimeDamaged = 0f;
                record.lifetimeHealed = 0f;
                record.becameEmptyAtUtc = current <= definition.MinimumValue + Epsilon ? record.lastChangedAtUtc : string.Empty;
                record.becameFullAtUtc = current >= maximum - Epsilon ? record.lastChangedAtUtc : string.Empty;
                record.regenerationBlockedUntil = 0f;
                record.initialized = true;

                ResourceChangeRequest request = new ResourceChangeRequest(definition.Id, ResourceChangeOperation.Restore, current, ResourceChangeSourceCategory.Development, sourceId, reason, restoration: restoration);
                RaiseChangeEvents(ResourceChangeResult.Success(
                    request,
                    current,
                    Mathf.Abs(current - oldCurrent),
                    oldCurrent,
                    current,
                    definition.MinimumValue,
                    maximum,
                    false,
                    false,
                    oldCurrent > definition.MinimumValue + Epsilon && current <= definition.MinimumValue + Epsilon,
                    oldCurrent <= definition.MinimumValue + Epsilon && current > definition.MinimumValue + Epsilon,
                    oldCurrent < maximum - Epsilon && current >= maximum - Epsilon,
                    oldCurrent >= maximum - Epsilon && current < maximum - Epsilon,
                    $"{definition.DisplayName} reset to definition default."),
                    restoration);
            }

            ResourcesRestored?.Invoke(this, restoration);
        }

        public bool ReconcileResource(string resourceId, bool restoring = false)
        {
            if (!definitionsById.TryGetValue(resourceId, out ResourceDefinition definition) || !recordsById.TryGetValue(resourceId, out RuntimeResourceRecord record))
            {
                return false;
            }

            float oldMaximum = record.lastKnownMaximum <= 0f ? ResolveMaximum(definition) : record.lastKnownMaximum;
            float newMaximum = ResolveMaximum(definition);
            if (!Mathf.Approximately(oldMaximum, newMaximum))
            {
                ResourceMaximumChanged?.Invoke(this, CreateSnapshot(definition, record), oldMaximum, restoring);
            }

            float oldCurrent = record.currentValue;
            float reconciled = ReconcileCurrent(definition, record.currentValue, oldMaximum, newMaximum);
            record.lastKnownMaximum = newMaximum;
            if (Mathf.Approximately(oldCurrent, reconciled))
            {
                return false;
            }

            ResourceChangeRequest request = new ResourceChangeRequest(resourceId, ResourceChangeOperation.Reconcile, reconciled, ResourceChangeSourceCategory.Gameplay, "calculated-stat.maximum", "Maximum changed.", restoration: restoring);
            record.currentValue = reconciled;
            record.lastChangedAtUtc = DateTime.UtcNow.ToString("O");
            record.lastChangeSource = request.SourceId;
            record.lastChangeReason = request.Reason;
            RaiseChangeEvents(ResourceChangeResult.Success(request, reconciled, Mathf.Abs(reconciled - oldCurrent), oldCurrent, reconciled, definition.MinimumValue, newMaximum, true, false, oldCurrent > definition.MinimumValue + Epsilon && reconciled <= definition.MinimumValue + Epsilon, oldCurrent <= definition.MinimumValue + Epsilon && reconciled > definition.MinimumValue + Epsilon, oldCurrent < newMaximum - Epsilon && reconciled >= newMaximum - Epsilon, oldCurrent >= oldMaximum - Epsilon && reconciled < newMaximum - Epsilon), restoring);
            return true;
        }

        public PlayerResourcesSaveData CreateSaveData(string playerId, string personId)
        {
            return new PlayerResourcesSaveData
            {
                schemaVersion = PlayerResourcesSaveData.CurrentSchemaVersion,
                playerId = playerId ?? string.Empty,
                personId = personId ?? string.Empty,
                resources = recordsById.Values.Select(CloneRecord).ToList(),
                processedEventIds = processedEventIds.ToList()
            };
        }

        public string BuildDiagnosticSummary()
        {
            List<string> lines = new List<string>
            {
                "Feature 5.4b Current Resources",
                $"Configured: {IsConfigured}",
                $"Definitions: {definitionsById.Count}",
                $"Records: {recordsById.Count}",
                $"Owner: {ownerId}"
            };

            foreach (ResourceDefinition definition in definitionsById.Values.OrderBy(definition => definition.DisplayName))
            {
                if (!recordsById.TryGetValue(definition.Id, out RuntimeResourceRecord record))
                {
                    lines.Add($"{definition.DisplayName}: missing record ({definition.Id})");
                    continue;
                }

                ResourceSnapshot snapshot = CreateSnapshot(definition, record);
                lines.Add($"{definition.DisplayName}: {snapshot.Current:0.###}/{snapshot.Maximum:0.###} Min={snapshot.Minimum:0.###} Normalized={snapshot.Normalized:0.###} MaxStat={definition.LinkedMaximumStatId} Regen={(definition.RegenerationEnabled ? $"{definition.RegenerationPerSecond:0.###}/s delay {definition.RegenerationDelayAfterSpend:0.###}" : "Off")} Persist={definition.PersistencePolicy} Authority={definition.Authority}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public bool RestoreFromSaveData(PlayerResourcesSaveData saveData, DefinitionRegistry registry, CalculatedStatCollection statCollection, string expectedPlayerId, out string failureReason, bool restoring)
        {
            failureReason = string.Empty;
            if (!IsConfigured)
            {
                failureReason = "Character Resources must be initialized by CharacterSystemCoordinator before restore.";
                return false;
            }

            if (!ValidateSaveData(saveData, registry, statCollection, expectedPlayerId, out failureReason))
            {
                return false;
            }

            suppressNotifications = true;
            try
            {
                recordsById.Clear();
                processedEventIds.Clear();
                foreach (RuntimeResourceRecord record in saveData.resources ?? new List<RuntimeResourceRecord>())
                {
                    RuntimeResourceRecord clone = CloneRecord(record);
                    recordsById[clone.resourceDefinitionId] = clone;
                }

                foreach (string eventId in saveData.processedEventIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(eventId))
                    {
                        processedEventIds.Add(eventId);
                    }
                }
            }
            finally
            {
                suppressNotifications = false;
            }

            foreach (string resourceId in definitionsById.Keys.ToList())
            {
                ReconcileResource(resourceId, restoring);
            }

            ResourcesRestored?.Invoke(this, restoring);
            return true;
        }

        public static bool ValidateSaveData(PlayerResourcesSaveData saveData, DefinitionRegistry registry, CalculatedStatCollection statCollection, string expectedPlayerId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Player resources save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != PlayerResourcesSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported player resources schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedPlayerId) && !string.Equals(saveData.playerId, expectedPlayerId, StringComparison.Ordinal))
            {
                failureReason = $"Saved resources owner '{saveData.playerId}' does not match current player '{expectedPlayerId}'.";
                return false;
            }

            if (registry == null)
            {
                failureReason = "Definition registry is not available for resource restore.";
                return false;
            }

            HashSet<string> resourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RuntimeResourceRecord record in saveData.resources ?? new List<RuntimeResourceRecord>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.resourceDefinitionId))
                {
                    failureReason = "Resource record is missing a resource definition ID.";
                    return false;
                }

                if (!resourceIds.Add(record.resourceDefinitionId))
                {
                    failureReason = $"Duplicate resource record '{record.resourceDefinitionId}' in save data.";
                    return false;
                }

                if (!registry.TryGet(record.resourceDefinitionId, out ResourceDefinition definition))
                {
                    failureReason = $"Resource record references unknown ResourceDefinition '{record.resourceDefinitionId}'.";
                    return false;
                }

                float maximum = statCollection != null && statCollection.IsConfigured && statCollection.HasStat(definition.LinkedMaximumStatId)
                    ? statCollection.GetValue(definition.LinkedMaximumStatId)
                    : definition.DefaultMaximum;
                if (!IsFinite(record.currentValue) || record.currentValue < definition.MinimumValue - Epsilon || (!definition.OverfillAllowed && record.currentValue > maximum + Epsilon) || !IsFinite(record.lifetimeGained) || !IsFinite(record.lifetimeSpent) || !IsFinite(record.lifetimeDamaged) || !IsFinite(record.lifetimeHealed))
                {
                    failureReason = $"Resource record '{record.resourceDefinitionId}' has invalid numeric values.";
                    return false;
                }
            }

            HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string eventId in saveData.processedEventIds ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(eventId))
                {
                    continue;
                }

                if (!eventIds.Add(eventId))
                {
                    failureReason = $"Duplicate processed resource event ID '{eventId}' in save data.";
                    return false;
                }
            }

            return true;
        }

        public void TickResources(float deltaSeconds, float now)
        {
            if (deltaSeconds <= 0f)
            {
                return;
            }

            foreach (ResourceDefinition definition in definitionsById.Values.ToList())
            {
                if (IsAutomaticResourceTickSuppressed(definition.Id))
                {
                    continue;
                }

                if (!recordsById.TryGetValue(definition.Id, out RuntimeResourceRecord record))
                {
                    continue;
                }

                if (definition.RegenerationEnabled && now >= record.regenerationBlockedUntil && now >= GetNextTick(nextRegenerationTick, definition.Id))
                {
                    nextRegenerationTick[definition.Id] = now + definition.RegenerationInterval;
                    float amount = definition.RegenerationPerSecond * definition.RegenerationInterval;
                    ApplyChange(new ResourceChangeRequest(definition.Id, ResourceChangeOperation.Regenerate, amount, ResourceChangeSourceCategory.Regeneration, "resource.regeneration", "Resource regeneration.", allowPartial: true));
                }

                if (definition.DegenerationEnabled && now >= GetNextTick(nextDegenerationTick, definition.Id))
                {
                    nextDegenerationTick[definition.Id] = now + definition.DegenerationInterval;
                    float amount = definition.DegenerationPerSecond * definition.DegenerationInterval;
                    ApplyChange(new ResourceChangeRequest(definition.Id, ResourceChangeOperation.Degenerate, amount, ResourceChangeSourceCategory.Regeneration, "resource.degeneration", "Resource degeneration.", allowPartial: true));
                }
            }
        }

        private void OnCalculatedStatsChanged(CalculatedStatCollection stats, IReadOnlyList<string> statIds, bool restoring)
        {
            foreach (ResourceDefinition definition in definitionsById.Values.ToList())
            {
                if (statIds == null || statIds.Contains(definition.LinkedMaximumStatId))
                {
                    ReconcileResource(definition.Id, restoring);
                }
            }
        }

        private ResourceSnapshot CreateSnapshot(ResourceDefinition definition, RuntimeResourceRecord record)
        {
            return new ResourceSnapshot(definition.Id, record.currentValue, definition.MinimumValue, ResolveMaximum(definition), record.initialized);
        }

        private float ResolveMaximum(ResourceDefinition definition)
        {
            if (definition == null)
            {
                return 0f;
            }

            float maximum = calculatedStats != null && calculatedStats.IsConfigured && calculatedStats.HasStat(definition.LinkedMaximumStatId)
                ? calculatedStats.GetValue(definition.LinkedMaximumStatId)
                : definition.DefaultMaximum;
            return Mathf.Max(definition.MinimumValue, maximum);
        }

        private float ResolveInitialValue(ResourceDefinition definition, ResourceInitializationPolicy policy, float maximum, float minimum, float existing, bool preserveExisting)
        {
            if (preserveExisting && policy == ResourceInitializationPolicy.PreserveExisting)
            {
                return Mathf.Clamp(existing, minimum, maximum);
            }

            switch (policy)
            {
                case ResourceInitializationPolicy.Empty:
                    return minimum;
                case ResourceInitializationPolicy.FixedValue:
                    return Mathf.Clamp(definition.InitialFixedValue, minimum, maximum);
                case ResourceInitializationPolicy.PercentageOfMaximum:
                    return Mathf.Clamp(Mathf.Lerp(minimum, maximum, definition.InitialPercentageOfMaximum), minimum, maximum);
                case ResourceInitializationPolicy.PreserveExisting:
                    return Mathf.Clamp(existing, minimum, maximum);
                case ResourceInitializationPolicy.Full:
                case ResourceInitializationPolicy.DefinitionDefault:
                default:
                    return maximum;
            }
        }

        private float ReconcileCurrent(ResourceDefinition definition, float current, float oldMaximum, float newMaximum)
        {
            switch (definition.MaximumReconciliationPolicy)
            {
                case ResourceMaximumReconciliationPolicy.PreservePercentage:
                    float oldSpan = Mathf.Max(Epsilon, oldMaximum - definition.MinimumValue);
                    float percent = Mathf.Clamp01((current - definition.MinimumValue) / oldSpan);
                    return Mathf.Clamp(Mathf.Lerp(definition.MinimumValue, newMaximum, percent), definition.MinimumValue, newMaximum);
                case ResourceMaximumReconciliationPolicy.IncreaseCurrentByMaximumIncrease:
                    float delta = Mathf.Max(0f, newMaximum - oldMaximum);
                    return Mathf.Clamp(current + delta, definition.MinimumValue, newMaximum);
                case ResourceMaximumReconciliationPolicy.RefillToMaximum:
                    return newMaximum;
                case ResourceMaximumReconciliationPolicy.ClampOnly:
                case ResourceMaximumReconciliationPolicy.CustomReserved:
                default:
                    return Mathf.Clamp(current, definition.MinimumValue, newMaximum);
            }
        }

        private static float CalculateTarget(ResourceDefinition definition, ResourceChangeRequest request, float current, float minimum, float maximum, out bool rejectedForInsufficient)
        {
            rejectedForInsufficient = false;
            switch (request.Operation)
            {
                case ResourceChangeOperation.Gain:
                case ResourceChangeOperation.Heal:
                case ResourceChangeOperation.Regenerate:
                    return current + request.Amount;
                case ResourceChangeOperation.Spend:
                case ResourceChangeOperation.Damage:
                case ResourceChangeOperation.Degenerate:
                    if (!request.AllowPartial && current - request.Amount < minimum - Epsilon)
                    {
                        rejectedForInsufficient = true;
                    }

                    return current - request.Amount;
                case ResourceChangeOperation.Set:
                case ResourceChangeOperation.Restore:
                case ResourceChangeOperation.Initialize:
                case ResourceChangeOperation.Administrative:
                case ResourceChangeOperation.Reconcile:
                default:
                    return request.Amount;
            }
        }

        private static float Constrain(ResourceDefinition definition, ResourceChangeRequest request, float target, float minimum, float maximum, out bool clamped)
        {
            float lower = definition.UnderflowAllowed || request.AllowUnderflow ? float.NegativeInfinity : minimum;
            float upper = definition.OverfillAllowed || request.AllowOverfill ? float.PositiveInfinity : maximum;
            float constrained = Mathf.Clamp(target, lower, upper);
            clamped = !Mathf.Approximately(constrained, target);
            return constrained;
        }

        private static bool RequiresPositiveAmount(ResourceChangeOperation operation)
        {
            return operation == ResourceChangeOperation.Gain
                || operation == ResourceChangeOperation.Spend
                || operation == ResourceChangeOperation.Damage
                || operation == ResourceChangeOperation.Heal
                || operation == ResourceChangeOperation.Regenerate
                || operation == ResourceChangeOperation.Degenerate;
        }

        private static bool ValidateOperationAllowed(ResourceDefinition definition, ResourceChangeRequest request, out string failureReason)
        {
            failureReason = string.Empty;
            if ((request.Operation == ResourceChangeOperation.Gain || request.Operation == ResourceChangeOperation.Regenerate) && !definition.GainAllowed)
            {
                failureReason = $"{definition.DisplayName} does not allow gain.";
                return false;
            }

            if ((request.Operation == ResourceChangeOperation.Spend || request.Operation == ResourceChangeOperation.Degenerate) && !definition.SpendAllowed)
            {
                failureReason = $"{definition.DisplayName} does not allow spending.";
                return false;
            }

            if (request.Operation == ResourceChangeOperation.Damage && !definition.DamageAllowed)
            {
                failureReason = $"{definition.DisplayName} does not allow damage.";
                return false;
            }

            if (request.Operation == ResourceChangeOperation.Heal && !definition.HealingAllowed)
            {
                failureReason = $"{definition.DisplayName} does not allow healing.";
                return false;
            }

            return true;
        }

        private void ApplyRegenerationDelay(ResourceDefinition definition, RuntimeResourceRecord record, ResourceChangeOperation operation)
        {
            if (operation == ResourceChangeOperation.Spend && definition.RegenerationDelayAfterSpend > 0f)
            {
                record.regenerationBlockedUntil = CurrentPlaytimeSeconds + definition.RegenerationDelayAfterSpend;
            }
            else if (operation == ResourceChangeOperation.Damage && definition.RegenerationDelayAfterDamage > 0f)
            {
                record.regenerationBlockedUntil = CurrentPlaytimeSeconds + definition.RegenerationDelayAfterDamage;
            }
        }

        private static float CurrentPlaytimeSeconds => Application.isPlaying ? Time.time : 0f;

        private static void AddLifetime(RuntimeResourceRecord record, ResourceChangeOperation operation, float applied)
        {
            switch (operation)
            {
                case ResourceChangeOperation.Gain:
                case ResourceChangeOperation.Regenerate:
                    record.lifetimeGained += applied;
                    break;
                case ResourceChangeOperation.Spend:
                case ResourceChangeOperation.Degenerate:
                    record.lifetimeSpent += applied;
                    break;
                case ResourceChangeOperation.Damage:
                    record.lifetimeDamaged += applied;
                    break;
                case ResourceChangeOperation.Heal:
                    record.lifetimeHealed += applied;
                    break;
            }
        }

        private void RaiseChangeEvents(ResourceChangeResult result, bool restoring)
        {
            if (suppressNotifications || !result.Succeeded || !definitionsById.TryGetValue(result.Request.ResourceId, out ResourceDefinition definition) || !recordsById.TryGetValue(result.Request.ResourceId, out RuntimeResourceRecord record))
            {
                return;
            }

            ResourceSnapshot snapshot = CreateSnapshot(definition, record);
            ResourceChanged?.Invoke(this, result);
            if (result.BecameEmpty)
            {
                ResourceBecameEmpty?.Invoke(this, snapshot, restoring);
            }

            if (result.LeftEmpty)
            {
                ResourceLeftEmpty?.Invoke(this, snapshot, restoring);
            }

            if (result.BecameFull)
            {
                ResourceBecameFull?.Invoke(this, snapshot, restoring);
            }

            if (result.LeftFull)
            {
                ResourceLeftFull?.Invoke(this, snapshot, restoring);
            }
        }

        private static float GetNextTick(Dictionary<string, float> ticks, string resourceId)
        {
            return ticks.TryGetValue(resourceId, out float next) ? next : 0f;
        }

        private static RuntimeResourceRecord CloneRecord(RuntimeResourceRecord record)
        {
            return record == null
                ? null
                : new RuntimeResourceRecord
                {
                    resourceDefinitionId = record.resourceDefinitionId,
                    currentValue = record.currentValue,
                    lastKnownMaximum = record.lastKnownMaximum,
                    initializedAtUtc = record.initializedAtUtc,
                    initializedAtPlaytimeSeconds = record.initializedAtPlaytimeSeconds,
                    lastChangedAtUtc = record.lastChangedAtUtc,
                    lastChangedAtPlaytimeSeconds = record.lastChangedAtPlaytimeSeconds,
                    lastChangeSource = record.lastChangeSource,
                    lastChangeReason = record.lastChangeReason,
                    lifetimeGained = record.lifetimeGained,
                    lifetimeSpent = record.lifetimeSpent,
                    lifetimeDamaged = record.lifetimeDamaged,
                    lifetimeHealed = record.lifetimeHealed,
                    becameEmptyAtUtc = record.becameEmptyAtUtc,
                    becameFullAtUtc = record.becameFullAtUtc,
                    regenerationBlockedUntil = record.regenerationBlockedUntil,
                    initialized = record.initialized
                };
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
