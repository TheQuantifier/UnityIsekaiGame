using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Stats
{
    public sealed class CharacterAttributes : MonoBehaviour
    {
        private readonly Dictionary<string, AttributeDefinition> definitionsById = new Dictionary<string, AttributeDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeAttributeValueRecord> valuesById = new Dictionary<string, RuntimeAttributeValueRecord>(StringComparer.Ordinal);
        private readonly List<RuntimeAttributeSourceContribution> permanentSourceContributions = new List<RuntimeAttributeSourceContribution>();
        private readonly List<AttributeTrainingEventRecord> trainingEvents = new List<AttributeTrainingEventRecord>();

        public event Action<CharacterAttributes, IReadOnlyList<string>, bool> AttributesChanged;

        public IReadOnlyList<RuntimeAttributeSourceContribution> PermanentSourceContributions => permanentSourceContributions;
        public IReadOnlyList<AttributeTrainingEventRecord> TrainingEvents => trainingEvents;
        public IReadOnlyCollection<RuntimeAttributeValueRecord> AttributeValues => valuesById.Values;
        public bool IsConfigured { get; private set; }

        public void Configure(DefinitionRegistry registry)
        {
            Configure(registry == null
                ? Enumerable.Empty<AttributeDefinition>()
                : registry.DefinitionsById.Values.OfType<AttributeDefinition>());
        }

        public void Configure(IEnumerable<AttributeDefinition> definitions)
        {
            definitionsById.Clear();
            foreach (AttributeDefinition definition in definitions ?? Enumerable.Empty<AttributeDefinition>())
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Id) && !definitionsById.ContainsKey(definition.Id))
                {
                    definitionsById.Add(definition.Id, definition);
                }
            }

            IsConfigured = definitionsById.Count > 0;
            RecalculateAll(false);
        }

        public bool HasAttribute(string attributeId)
        {
            return definitionsById.ContainsKey(attributeId);
        }

        public float GetValue(string attributeId)
        {
            return valuesById.TryGetValue(attributeId, out RuntimeAttributeValueRecord record) ? record.currentValue : 0f;
        }

        public int GetDisplayedValue(string attributeId)
        {
            return Mathf.FloorToInt(GetValue(attributeId));
        }

        public IReadOnlyList<RuntimeAttributeValueRecord> GetOrderedValues()
        {
            return definitionsById.Values
                .OrderBy(definition => definition.DisplayName)
                .Select(definition => valuesById.TryGetValue(definition.Id, out RuntimeAttributeValueRecord record)
                    ? CloneValue(record)
                    : CreateFoundationRecord(definition))
                .ToList();
        }

        public bool TryAddPermanentSource(string sourceId, CalculatedStatContributionSourceCategory sourceCategory, string attributeId, float amount, bool removable, out string failureReason, bool restoring = false)
        {
            failureReason = string.Empty;

            if (!ValidateContributionInput(sourceId, attributeId, amount, allowZero: false, out failureReason))
            {
                return false;
            }

            if (permanentSourceContributions.Any(contribution => string.Equals(contribution.sourceId, sourceId, StringComparison.Ordinal)
                && string.Equals(contribution.attributeId, attributeId, StringComparison.Ordinal)))
            {
                failureReason = $"Permanent attribute source '{sourceId}' already contributes to '{attributeId}'.";
                return false;
            }

            RuntimeAttributeSourceContribution contribution = new RuntimeAttributeSourceContribution
            {
                contributionId = CreateContributionId("attribute-source"),
                sourceId = sourceId,
                sourceCategory = (int)sourceCategory,
                attributeId = attributeId,
                amount = amount,
                removable = removable,
                appliedAtUtc = DateTime.UtcNow.ToString("O")
            };

            permanentSourceContributions.Add(contribution);
            RecalculateChanged(new[] { attributeId }, restoring);
            return true;
        }

        public bool RemovePermanentSource(string sourceId, out string failureReason, bool restoring = false)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                failureReason = "Source ID is missing.";
                return false;
            }

            List<string> changed = permanentSourceContributions
                .Where(contribution => string.Equals(contribution.sourceId, sourceId, StringComparison.Ordinal))
                .Select(contribution => contribution.attributeId)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (changed.Count == 0)
            {
                return false;
            }

            permanentSourceContributions.RemoveAll(contribution => string.Equals(contribution.sourceId, sourceId, StringComparison.Ordinal) && contribution.removable);
            RecalculateChanged(changed, restoring);
            return true;
        }

        public bool TryRecordTrainingEvent(string eventId, AttributeGrowthEventCategory category, IReadOnlyList<RuntimeAttributeSourceContribution> contributions, string sourceSystem, out string failureReason, bool restoring = false)
        {
            failureReason = string.Empty;

            if (string.IsNullOrWhiteSpace(eventId))
            {
                eventId = CreateContributionId("attribute-growth");
            }

            if (trainingEvents.Any(record => string.Equals(record.eventId, eventId, StringComparison.Ordinal)))
            {
                failureReason = $"Attribute growth event '{eventId}' is already recorded.";
                return false;
            }

            if (contributions == null || contributions.Count == 0)
            {
                failureReason = "Attribute growth event has no contributions.";
                return false;
            }

            List<RuntimeAttributeSourceContribution> cloned = new List<RuntimeAttributeSourceContribution>();
            for (int i = 0; i < contributions.Count; i++)
            {
                RuntimeAttributeSourceContribution contribution = contributions[i];
                if (contribution == null || !ValidateContributionInput(eventId, contribution.attributeId, contribution.amount, allowZero: false, out failureReason))
                {
                    return false;
                }

                cloned.Add(new RuntimeAttributeSourceContribution
                {
                    contributionId = string.IsNullOrWhiteSpace(contribution.contributionId) ? CreateContributionId("attribute-growth-contribution") : contribution.contributionId,
                    sourceId = string.IsNullOrWhiteSpace(contribution.sourceId) ? eventId : contribution.sourceId,
                    sourceCategory = contribution.sourceCategory,
                    attributeId = contribution.attributeId,
                    amount = contribution.amount,
                    removable = false,
                    appliedAtUtc = string.IsNullOrWhiteSpace(contribution.appliedAtUtc) ? DateTime.UtcNow.ToString("O") : contribution.appliedAtUtc
                });
            }

            trainingEvents.Add(new AttributeTrainingEventRecord
            {
                eventId = eventId,
                category = (int)category,
                sourceSystem = sourceSystem ?? string.Empty,
                recordedAtUtc = DateTime.UtcNow.ToString("O"),
                contributions = cloned
            });

            RecalculateChanged(cloned.Select(contribution => contribution.attributeId).Distinct(StringComparer.Ordinal).ToList(), restoring);
            return true;
        }

        public PlayerAttributesSaveData CreateSaveData(string playerId, string personId)
        {
            return new PlayerAttributesSaveData
            {
                schemaVersion = PlayerAttributesSaveData.CurrentSchemaVersion,
                playerId = playerId ?? string.Empty,
                personId = personId ?? string.Empty,
                permanentSourceContributions = permanentSourceContributions.Select(CloneContribution).ToList(),
                trainingEvents = trainingEvents.Select(CloneTrainingEvent).ToList()
            };
        }

        public bool RestoreFromSaveData(PlayerAttributesSaveData saveData, DefinitionRegistry registry, out string failureReason, bool restoring)
        {
            failureReason = string.Empty;
            if (!IsConfigured)
            {
                failureReason = "Character Attributes must be initialized by CharacterSystemCoordinator before restore.";
                return false;
            }

            if (!ValidateSaveData(saveData, registry, out failureReason))
            {
                return false;
            }

            permanentSourceContributions.Clear();
            trainingEvents.Clear();
            permanentSourceContributions.AddRange(saveData.permanentSourceContributions.Select(CloneContribution));
            trainingEvents.AddRange(saveData.trainingEvents.Select(CloneTrainingEvent));
            RecalculateAll(restoring);
            return true;
        }

        public static bool ValidateSaveData(PlayerAttributesSaveData saveData, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Attribute save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != PlayerAttributesSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported player attributes schema version {saveData.schemaVersion}. Development saves from earlier Feature 5.2 schemas are intentionally rejected.";
                return false;
            }

            if (registry == null)
            {
                failureReason = "Definition registry is not available for attribute restore.";
                return false;
            }

            HashSet<string> permanentKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (RuntimeAttributeSourceContribution contribution in saveData.permanentSourceContributions ?? new List<RuntimeAttributeSourceContribution>())
            {
                if (!ValidateSavedContribution(contribution, registry, out failureReason))
                {
                    return false;
                }

                string key = $"{contribution.sourceId}|{contribution.attributeId}";
                if (!permanentKeys.Add(key))
                {
                    failureReason = $"Duplicate permanent attribute contribution '{key}' in save data.";
                    return false;
                }
            }

            HashSet<string> eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (AttributeTrainingEventRecord record in saveData.trainingEvents ?? new List<AttributeTrainingEventRecord>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.eventId))
                {
                    failureReason = "Attribute training event is missing an event ID.";
                    return false;
                }

                if (!eventIds.Add(record.eventId))
                {
                    failureReason = $"Duplicate attribute training event '{record.eventId}' in save data.";
                    return false;
                }

                if (record.contributions == null || record.contributions.Count == 0)
                {
                    failureReason = $"Attribute training event '{record.eventId}' has no contributions.";
                    return false;
                }

                foreach (RuntimeAttributeSourceContribution contribution in record.contributions)
                {
                    if (!ValidateSavedContribution(contribution, registry, out failureReason))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public string BuildDiagnosticSummary()
        {
            List<string> lines = new List<string> { "Feature 5.4a Base Attributes" };
            foreach (RuntimeAttributeValueRecord record in GetOrderedValues())
            {
                lines.Add($"{record.attributeId}: full {record.currentValue:0.###}, display {Mathf.FloorToInt(record.currentValue)}, foundation {record.foundationValue:0.###}, permanent {record.permanentSourceTotal:0.###}, growth {record.growthTotal:0.###}");
            }

            lines.Add($"Permanent Sources: {permanentSourceContributions.Count}");
            lines.Add($"Growth Events: {trainingEvents.Count}");
            return string.Join(Environment.NewLine, lines);
        }

        private void RecalculateAll(bool restoring)
        {
            valuesById.Clear();
            foreach (AttributeDefinition definition in definitionsById.Values)
            {
                valuesById.Add(definition.Id, CalculateValue(definition));
            }

            AttributesChanged?.Invoke(this, valuesById.Keys.ToList(), restoring);
        }

        private void RecalculateChanged(IReadOnlyList<string> attributeIds, bool restoring)
        {
            List<string> changed = new List<string>();
            foreach (string attributeId in attributeIds ?? Array.Empty<string>())
            {
                if (!definitionsById.TryGetValue(attributeId, out AttributeDefinition definition))
                {
                    continue;
                }

                float previous = GetValue(attributeId);
                valuesById[attributeId] = CalculateValue(definition);
                if (!Mathf.Approximately(previous, valuesById[attributeId].currentValue))
                {
                    changed.Add(attributeId);
                }
            }

            if (changed.Count > 0)
            {
                AttributesChanged?.Invoke(this, changed, restoring);
            }
        }

        private RuntimeAttributeValueRecord CalculateValue(AttributeDefinition definition)
        {
            float permanentTotal = permanentSourceContributions
                .Where(contribution => string.Equals(contribution.attributeId, definition.Id, StringComparison.Ordinal))
                .Sum(contribution => contribution.amount);
            float growthTotal = trainingEvents
                .Where(record => record.contributions != null)
                .SelectMany(record => record.contributions)
                .Where(contribution => string.Equals(contribution.attributeId, definition.Id, StringComparison.Ordinal))
                .Sum(contribution => contribution.amount);

            float current = Mathf.Max(0f, definition.FoundationValue + permanentTotal + growthTotal);
            return new RuntimeAttributeValueRecord
            {
                attributeId = definition.Id,
                foundationValue = definition.FoundationValue,
                permanentSourceTotal = permanentTotal,
                growthTotal = growthTotal,
                currentValue = current
            };
        }

        private bool ValidateContributionInput(string sourceId, string attributeId, float amount, bool allowZero, out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(sourceId))
            {
                failureReason = "Attribute contribution source ID is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(attributeId) || !definitionsById.ContainsKey(attributeId))
            {
                failureReason = $"Attribute '{attributeId}' is not configured.";
                return false;
            }

            if (!IsFinite(amount) || amount < 0f || (!allowZero && Mathf.Approximately(amount, 0f)))
            {
                failureReason = "Attribute contribution amount must be finite and positive.";
                return false;
            }

            return true;
        }

        private static bool ValidateSavedContribution(RuntimeAttributeSourceContribution contribution, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (contribution == null)
            {
                failureReason = "Attribute contribution is missing.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(contribution.attributeId) || !registry.TryGet(contribution.attributeId, out AttributeDefinition _))
            {
                failureReason = $"Attribute contribution references unknown attribute '{contribution.attributeId}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(contribution.sourceId))
            {
                failureReason = "Attribute contribution source ID is missing.";
                return false;
            }

            if (!IsFinite(contribution.amount) || contribution.amount < 0f)
            {
                failureReason = "Saved attribute contribution amount must be finite and non-negative.";
                return false;
            }

            return true;
        }

        private static RuntimeAttributeSourceContribution CloneContribution(RuntimeAttributeSourceContribution contribution)
        {
            return contribution == null
                ? null
                : new RuntimeAttributeSourceContribution
                {
                    contributionId = contribution.contributionId,
                    attributeId = contribution.attributeId,
                    sourceId = contribution.sourceId,
                    sourceCategory = contribution.sourceCategory,
                    amount = contribution.amount,
                    removable = contribution.removable,
                    appliedAtUtc = contribution.appliedAtUtc
                };
        }

        private static AttributeTrainingEventRecord CloneTrainingEvent(AttributeTrainingEventRecord record)
        {
            return record == null
                ? null
                : new AttributeTrainingEventRecord
                {
                    eventId = record.eventId,
                    category = record.category,
                    sourceSystem = record.sourceSystem,
                    recordedAtUtc = record.recordedAtUtc,
                    contributions = record.contributions == null ? new List<RuntimeAttributeSourceContribution>() : record.contributions.Select(CloneContribution).ToList()
                };
        }

        private static RuntimeAttributeValueRecord CloneValue(RuntimeAttributeValueRecord record)
        {
            return new RuntimeAttributeValueRecord
            {
                attributeId = record.attributeId,
                foundationValue = record.foundationValue,
                permanentSourceTotal = record.permanentSourceTotal,
                growthTotal = record.growthTotal,
                currentValue = record.currentValue
            };
        }

        private static RuntimeAttributeValueRecord CreateFoundationRecord(AttributeDefinition definition)
        {
            return new RuntimeAttributeValueRecord
            {
                attributeId = definition.Id,
                foundationValue = definition.FoundationValue,
                currentValue = definition.FoundationValue
            };
        }

        private static string CreateContributionId(string prefix)
        {
            return $"{prefix}.{Guid.NewGuid():N}".ToLowerInvariant();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
