using System;
using System.Collections.Generic;
using System.Linq;
using UnityIsekaiGame.GameData;

namespace UnityIsekaiGame.Inventory.Recipes
{
    [Serializable]
    public sealed class RecipeKnowledgeRecordData
    {
        public string recordId;
        public string personId;
        public string recipeId;
        public string versionId;
        public string variantId;
        public RecipeKnowledgeCompleteness completeness = RecipeKnowledgeCompleteness.Unknown;
        public bool incorrect;
        public bool outdated;
        public string[] knownInputIds = Array.Empty<string>();
        public string[] knownOutputIds = Array.Empty<string>();
        public string[] knownStepIds = Array.Empty<string>();
        public string[] sourceIds = Array.Empty<string>();
        public string beliefId;
        public string memoryId;
        public string historicalRecordId;
        public long revision = 1L;

        public RecipeKnowledgeRecordData Clone()
        {
            return new RecipeKnowledgeRecordData
            {
                recordId = recordId ?? string.Empty,
                personId = personId ?? string.Empty,
                recipeId = recipeId ?? string.Empty,
                versionId = versionId ?? string.Empty,
                variantId = variantId ?? string.Empty,
                completeness = completeness,
                incorrect = incorrect,
                outdated = outdated,
                knownInputIds = CloneIds(knownInputIds),
                knownOutputIds = CloneIds(knownOutputIds),
                knownStepIds = CloneIds(knownStepIds),
                sourceIds = CloneIds(sourceIds),
                beliefId = beliefId ?? string.Empty,
                memoryId = memoryId ?? string.Empty,
                historicalRecordId = historicalRecordId ?? string.Empty,
                revision = Math.Max(1L, revision)
            };
        }

        private static string[] CloneIds(IEnumerable<string> values)
        {
            return (values ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }
    }

    [Serializable]
    public sealed class RecipeKnowledgeSaveData
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public long revision;
        public List<RecipeKnowledgeRecordData> records = new List<RecipeKnowledgeRecordData>();

        public RecipeKnowledgeSaveData Clone()
        {
            return new RecipeKnowledgeSaveData
            {
                schemaVersion = schemaVersion,
                revision = revision,
                records = records == null ? new List<RecipeKnowledgeRecordData>() : records.Select(record => record?.Clone()).Where(record => record != null).ToList()
            };
        }
    }

    public sealed class RecipeKnowledgeRuntime
    {
        private readonly Dictionary<string, RecipeKnowledgeRecordData> recordsById = new Dictionary<string, RecipeKnowledgeRecordData>(StringComparer.Ordinal);
        private long revision;

        public long Revision => revision;
        public int RecordCount => recordsById.Count;
        public IReadOnlyList<RecipeKnowledgeRecordData> Records => recordsById.Values.OrderBy(record => record.recordId, StringComparer.Ordinal).Select(record => record.Clone()).ToArray();

        public RecipeKnowledgeRecordData LearnOrUpdate(RecipeKnowledgeRecordData record)
        {
            if (record == null || string.IsNullOrWhiteSpace(record.recordId) || string.IsNullOrWhiteSpace(record.personId) || string.IsNullOrWhiteSpace(record.recipeId))
            {
                return null;
            }

            RecipeKnowledgeRecordData stored = record.Clone();
            stored.revision = recordsById.TryGetValue(stored.recordId, out RecipeKnowledgeRecordData existing) ? existing.revision + 1L : 1L;
            recordsById[stored.recordId] = stored.Clone();
            revision++;
            return stored.Clone();
        }

        public bool TryGet(string recordId, out RecipeKnowledgeRecordData record)
        {
            if (!string.IsNullOrWhiteSpace(recordId) && recordsById.TryGetValue(recordId, out RecipeKnowledgeRecordData found))
            {
                record = found.Clone();
                return true;
            }

            record = null;
            return false;
        }

        public IReadOnlyList<RecipeKnowledgeRecordData> QueryPerson(string personId)
        {
            return recordsById.Values
                .Where(record => string.Equals(record.personId, personId, StringComparison.Ordinal))
                .OrderBy(record => record.recipeId, StringComparer.Ordinal)
                .ThenBy(record => record.versionId, StringComparer.Ordinal)
                .ThenBy(record => record.recordId, StringComparer.Ordinal)
                .Select(record => record.Clone())
                .ToArray();
        }

        public RecipeResolvedSnapshot ProjectKnownRecipe(RecipeResolvedSnapshot authoritative, RecipeKnowledgeRecordData knowledge, RecipeProjectionAccessLevel accessLevel)
        {
            if (authoritative == null)
            {
                return null;
            }

            if (accessLevel == RecipeProjectionAccessLevel.Privileged || knowledge == null || knowledge.completeness == RecipeKnowledgeCompleteness.Complete)
            {
                return authoritative.Clone();
            }

            HashSet<string> knownInputs = new HashSet<string>(knowledge.knownInputIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            HashSet<string> knownOutputs = new HashSet<string>(knowledge.knownOutputIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            HashSet<string> knownSteps = new HashSet<string>(knowledge.knownStepIds ?? Array.Empty<string>(), StringComparer.Ordinal);

            RecipeInputSpecificationData[] inputs = authoritative.Inputs.Where(input => knownInputs.Contains(input.inputId)).Select(input => input.CloneScaled(1f)).ToArray();
            RecipeOutputSpecificationData[] outputs = authoritative.Outputs.Where(output => knownOutputs.Contains(output.outputId)).Select(output => output.CloneScaled(1f)).ToArray();
            RecipeProcedureStepData[] steps = authoritative.ProcedureSteps.Where(step => knownSteps.Contains(step.stepId)).Select(step => step.Clone()).ToArray();
            return new RecipeResolvedSnapshot(authoritative.RecipeId, authoritative.VersionId, authoritative.VariantId, authoritative.BatchSize, inputs, outputs, Array.Empty<RecipeTransferMappingData>(), steps, authoritative.RequirementIds, true, authoritative.Signature);
        }

        public RecipeKnowledgeSaveData CreateSaveData()
        {
            return new RecipeKnowledgeSaveData
            {
                schemaVersion = RecipeKnowledgeSaveData.CurrentSchemaVersion,
                revision = revision,
                records = recordsById.Values.OrderBy(record => record.recordId, StringComparer.Ordinal).Select(record => record.Clone()).ToList()
            };
        }

        public bool RestoreFromSaveData(RecipeKnowledgeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            if (!ValidateSaveData(saveData, registry, out failure))
            {
                return false;
            }

            recordsById.Clear();
            foreach (RecipeKnowledgeRecordData record in saveData.records.Select(record => record.Clone()).OrderBy(record => record.recordId, StringComparer.Ordinal))
            {
                recordsById[record.recordId] = record;
            }

            revision = Math.Max(0L, saveData.revision);
            return true;
        }

        public static bool ValidateSaveData(RecipeKnowledgeSaveData saveData, DefinitionRegistry registry, out string failure)
        {
            failure = string.Empty;
            if (saveData == null)
            {
                failure = "Recipe knowledge save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != RecipeKnowledgeSaveData.CurrentSchemaVersion)
            {
                failure = $"Unsupported recipe knowledge schema version {saveData.schemaVersion}.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (RecipeKnowledgeRecordData record in saveData.records ?? new List<RecipeKnowledgeRecordData>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.recordId) || string.IsNullOrWhiteSpace(record.personId) || string.IsNullOrWhiteSpace(record.recipeId))
                {
                    failure = "Recipe knowledge record is missing identity, person, or recipe.";
                    return false;
                }

                if (!ids.Add(record.recordId))
                {
                    failure = $"Duplicate recipe knowledge record '{record.recordId}'.";
                    return false;
                }

                if (registry != null && !registry.TryGet(record.recipeId, out RecipeDefinition _))
                {
                    failure = $"Recipe knowledge record '{record.recordId}' references missing recipe '{record.recipeId}'.";
                    return false;
                }
            }

            return true;
        }
    }
}
