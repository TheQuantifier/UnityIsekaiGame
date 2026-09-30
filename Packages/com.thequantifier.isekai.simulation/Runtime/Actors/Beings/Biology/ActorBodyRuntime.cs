using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Beings.Biology.Anatomy;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Condition;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.Transformation;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Beings.Biology
{
    [DisallowMultipleComponent]
    public sealed class ActorBodyRuntime : MonoBehaviour
    {
        [SerializeField] private SpeciesDefinition defaultSpecies;
        [SerializeField] private string actorBodyIdOverride;
        [SerializeField] private string personIdOverride;

        private DefinitionRegistry registry;
        private CharacterTraitCollection traits;
        private CharacterCapabilityCollection capabilities;
        private CalculatedStatCollection calculatedStats;
        private string actorBodyId;
        private string personId;
        private string speciesDefinitionId;
        private string appliedSpeciesSourceId;
        private string appliedClassificationSourceId;
        private bool suppressEvents;
        private readonly AnatomyRuntime anatomyRuntime = new AnatomyRuntime();
        private readonly BodyConditionRuntime conditionRuntime = new BodyConditionRuntime();
        private readonly VitalProcessRuntime vitalProcessRuntime = new VitalProcessRuntime();
        private readonly BiologicalHazardRuntime biologicalHazardRuntime = new BiologicalHazardRuntime();
        private readonly BiologicalCompatibilityRuntime biologicalCompatibilityRuntime = new BiologicalCompatibilityRuntime();
        private readonly BiologicalConditionRuntime biologicalConditionRuntime = new BiologicalConditionRuntime();
        private readonly BiologicalRecoveryRuntime biologicalRecoveryRuntime = new BiologicalRecoveryRuntime();
        private readonly BodyTransformationRuntime transformationRuntime = new BodyTransformationRuntime();

        public event Action<ActorBodyRuntime, BodyOperationResult, bool> BodyChanged;

        public BodyReadinessState Readiness { get; private set; } = BodyReadinessState.Uninitialized;
        public long BodyRevision { get; private set; }
        public string ActorBodyId => string.IsNullOrWhiteSpace(actorBodyIdOverride) ? actorBodyId ?? string.Empty : actorBodyIdOverride;
        public string PersonId => string.IsNullOrWhiteSpace(personIdOverride) ? personId ?? string.Empty : personIdOverride;
        public string SpeciesDefinitionId => speciesDefinitionId ?? string.Empty;
        public SpeciesDefinition Species => ResolveSpecies(SpeciesDefinitionId);
        public BiologicalClassificationDefinition BiologicalClassification => Species == null ? null : Species.BiologicalClassification;
        public BodyFormDefinition BodyForm => Species == null ? null : Species.BodyForm;
        public AnatomyRuntime Anatomy => anatomyRuntime;
        public BodyConditionRuntime Condition => conditionRuntime;
        public VitalProcessRuntime VitalProcesses => vitalProcessRuntime;
        public BiologicalHazardRuntime BiologicalHazards => biologicalHazardRuntime;
        public BiologicalCompatibilityRuntime BiologicalCompatibility => biologicalCompatibilityRuntime;
        public BiologicalConditionRuntime BiologicalConditions => biologicalConditionRuntime;
        public BiologicalRecoveryRuntime BiologicalRecovery => biologicalRecoveryRuntime;
        public BodyTransformationRuntime Transformation => transformationRuntime;
        public bool IsReady => Readiness == BodyReadinessState.Ready;

        private void OnDisable()
        {
            Readiness = BodyReadinessState.Disposed;
            anatomyRuntime.Dispose();
            conditionRuntime.Dispose();
            vitalProcessRuntime.Dispose();
            biologicalHazardRuntime.Dispose();
            biologicalCompatibilityRuntime.Dispose();
            biologicalConditionRuntime.Dispose();
            biologicalRecoveryRuntime.Dispose();
            transformationRuntime.Dispose();
        }

        public void Configure(
            DefinitionRegistry definitionRegistry,
            string exactActorBodyId,
            string owningPersonId,
            CharacterTraitCollection traitCollection,
            CalculatedStatCollection statCollection,
            bool restoring = false,
            CharacterCapabilityCollection capabilityCollection = null)
        {
            registry = definitionRegistry ?? registry;
            actorBodyId = string.IsNullOrWhiteSpace(exactActorBodyId) ? actorBodyId : exactActorBodyId;
            personId = owningPersonId ?? string.Empty;
            traits = traitCollection == null ? traits == null ? GetComponent<CharacterTraitCollection>() : traits : traitCollection;
            capabilities = capabilityCollection == null ? capabilities == null ? GetComponent<CharacterCapabilityCollection>() : capabilities : capabilityCollection;
            calculatedStats = statCollection == null ? calculatedStats == null ? GetComponent<CalculatedStatCollection>() : calculatedStats : statCollection;

            if (string.IsNullOrWhiteSpace(SpeciesDefinitionId) && defaultSpecies != null)
            {
                AssignSpecies(defaultSpecies.Id, restoring, "Default body bootstrap");
                return;
            }

            if (!string.IsNullOrWhiteSpace(SpeciesDefinitionId))
            {
                ReapplyCurrentBiologicalSources(restoring);
                BuildAnatomyForCurrentSpecies(restoring, null, preserveRevision: true);
                conditionRuntime.BuildHealthy(ActorBodyId, anatomyRuntime.CreateSnapshot(BodyRevision), registry, restoring, preserveRevision: true);
                BuildVitalProcessesForCurrentBody(restoring, preserveRevision: true);
                BuildBiologicalHazardsForCurrentBody(restoring, preserveRevision: true);
                BuildBiologicalCompatibilityForCurrentBody(restoring, preserveRevision: true);
                BuildBiologicalConditionsForCurrentBody(restoring, preserveRevision: true);
                BuildBiologicalRecoveryForCurrentBody(restoring, preserveRevision: true);
                BuildTransformationForCurrentBody(restoring, preserveRevision: true);
            }

            ValidateBody(out _);
        }

        public BodyOperationResult PreviewAssignSpecies(string requestedSpeciesId)
        {
            return ResolveAssignment(requestedSpeciesId, preview: true, out _, out _, out _);
        }

        public BodyOperationResult AssignSpecies(string requestedSpeciesId, bool restoring = false, string reason = "Species assignment")
        {
            BodyOperationResult resolved = ResolveAssignment(requestedSpeciesId, preview: false, out SpeciesDefinition species, out BiologicalClassificationDefinition classification, out BodyFormDefinition bodyForm);
            if (!resolved.Succeeded)
            {
                return resolved;
            }

            if (string.Equals(SpeciesDefinitionId, species.Id, StringComparison.Ordinal) && Readiness == BodyReadinessState.Ready && anatomyRuntime.IsReady)
            {
                BodyOperationResult duplicate = BodyOperationResult.Success($"Species '{species.Id}' is already assigned to body '{ActorBodyId}'.", CreateSnapshot(), duplicate: true);
                RaiseChanged(duplicate, restoring);
                return duplicate;
            }

            string previousSpecies = speciesDefinitionId;
            string previousSpeciesSource = appliedSpeciesSourceId;
            string previousClassificationSource = appliedClassificationSourceId;
            long previousRevision = BodyRevision;
            BodyReadinessState previousReadiness = Readiness;
            AnatomySaveData previousAnatomy = anatomyRuntime.CreateSaveData();
            BodyConditionSaveData previousCondition = conditionRuntime.CreateSaveData();
            VitalProcessSaveData previousVitalProcesses = vitalProcessRuntime.CreateSaveData();
            BiologicalHazardSaveData previousBiologicalHazards = biologicalHazardRuntime.CreateSaveData();
            BiologicalCompatibilitySnapshot previousBiologicalCompatibility = biologicalCompatibilityRuntime.CreateSnapshot();
            BiologicalConditionSaveData previousBiologicalConditions = biologicalConditionRuntime.CreateSaveData();
            BiologicalRecoverySaveData previousBiologicalRecovery = biologicalRecoveryRuntime.CreateSaveData();

            try
            {
                Readiness = BodyReadinessState.ApplyingBiologicalContributions;
                string speciesSourceId = SpeciesSourceId(ActorBodyId, species.Id);
                string classificationSourceId = ClassificationSourceId(ActorBodyId, classification.Id);
                ClearBiologicalSources(previousSpeciesSource, previousClassificationSource, restoring);
                ClearBiologicalSources(speciesSourceId, classificationSourceId, restoring);
                BodyOperationResult applyClassification = ApplyBiologicalSources(
                    classification.DefaultTraitGrants,
                    classification.DefaultCapabilityGrants,
                    classification.DefaultStatContributions,
                    TraitSourceCategory.BiologicalClassification,
                    CapabilitySourceCategory.BiologicalClassification,
                    CalculatedStatContributionSourceCategory.BiologicalClassification,
                    classificationSourceId,
                    restoring);
                if (!applyClassification.Succeeded)
                {
                    throw new InvalidOperationException(applyClassification.Message);
                }

                BodyOperationResult applySpecies = ApplyBiologicalSources(
                    species.DefaultBodyTraits,
                    species.DefaultBooleanCapabilities.Concat(species.DefaultNumericCapabilities).ToArray(),
                    species.CalculatedStatContributions,
                    TraitSourceCategory.Species,
                    CapabilitySourceCategory.Species,
                    CalculatedStatContributionSourceCategory.Species,
                    speciesSourceId,
                    restoring);
                if (!applySpecies.Succeeded)
                {
                    throw new InvalidOperationException(applySpecies.Message);
                }

                speciesDefinitionId = species.Id;
                appliedSpeciesSourceId = speciesSourceId;
                appliedClassificationSourceId = classificationSourceId;
                BodyRevision++;
                BodyOperationResult anatomyResult = BuildAnatomyForCurrentSpecies(restoring, null, preserveRevision: false);
                if (!anatomyResult.Succeeded)
                {
                    throw new InvalidOperationException(anatomyResult.Message);
                }

                LocalizedStructuralDamageResult conditionResult = conditionRuntime.BuildHealthy(ActorBodyId, anatomyRuntime.CreateSnapshot(BodyRevision), registry, restoring);
                if (!conditionResult.Succeeded)
                {
                    throw new InvalidOperationException(conditionResult.Message);
                }

                VitalResourceMutationResult vitalResult = BuildVitalProcessesForCurrentBody(restoring, preserveRevision: false);
                if (!vitalResult.Succeeded)
                {
                    throw new InvalidOperationException(vitalResult.Message);
                }

                BiologicalHazardOperationResult hazardResult = BuildBiologicalHazardsForCurrentBody(restoring, preserveRevision: false);
                if (!hazardResult.Succeeded)
                {
                    throw new InvalidOperationException(hazardResult.Message);
                }

                BiologicalCompatibilityOperationResult compatibilityResult = BuildBiologicalCompatibilityForCurrentBody(restoring, preserveRevision: false);
                if (!compatibilityResult.Succeeded)
                {
                    throw new InvalidOperationException(compatibilityResult.Message);
                }

                BiologicalConditionResult biologicalConditionResult = BuildBiologicalConditionsForCurrentBody(restoring, preserveRevision: true);
                if (!biologicalConditionResult.Succeeded)
                {
                    throw new InvalidOperationException(biologicalConditionResult.Message);
                }

                biologicalConditionResult = biologicalConditionRuntime.ReconcileForCurrentBody(CreateSnapshot(includeRecovery: false), biologicalCompatibilityRuntime, bodyReplacement: false, restoring);
                if (!biologicalConditionResult.Succeeded)
                {
                    throw new InvalidOperationException(biologicalConditionResult.Message);
                }

                BiologicalRecoveryResult recoveryResult = BuildBiologicalRecoveryForCurrentBody(restoring, preserveRevision: false);
                if (!recoveryResult.Succeeded)
                {
                    throw new InvalidOperationException(recoveryResult.Message);
                }

                BuildTransformationForCurrentBody(restoring, preserveRevision: true);
                Readiness = BodyReadinessState.Ready;

                BodyOperationResult result = BodyOperationResult.Success($"Assigned Species '{species.Id}' to body '{ActorBodyId}'.", CreateSnapshot());
                RaiseChanged(result, restoring);
                return result;
            }
            catch (Exception exception)
            {
                ClearBiologicalSources(appliedSpeciesSourceId, appliedClassificationSourceId, restoring);
                speciesDefinitionId = previousSpecies;
                appliedSpeciesSourceId = previousSpeciesSource;
                appliedClassificationSourceId = previousClassificationSource;
                BodyRevision = previousRevision;
                Readiness = previousReadiness;
                if (!string.IsNullOrWhiteSpace(previousSpecies))
                {
                    SpeciesDefinition rollbackSpecies = ResolveSpecies(previousSpecies);
                    if (rollbackSpecies != null && rollbackSpecies.BiologicalClassification != null)
                    {
                        ApplyBiologicalSources(
                            rollbackSpecies.BiologicalClassification.DefaultTraitGrants,
                            rollbackSpecies.BiologicalClassification.DefaultCapabilityGrants,
                            rollbackSpecies.BiologicalClassification.DefaultStatContributions,
                            TraitSourceCategory.BiologicalClassification,
                            CapabilitySourceCategory.BiologicalClassification,
                            CalculatedStatContributionSourceCategory.BiologicalClassification,
                            previousClassificationSource,
                            restoring);
                        ApplyBiologicalSources(
                            rollbackSpecies.DefaultBodyTraits,
                            rollbackSpecies.DefaultBooleanCapabilities.Concat(rollbackSpecies.DefaultNumericCapabilities).ToArray(),
                            rollbackSpecies.CalculatedStatContributions,
                            TraitSourceCategory.Species,
                            CapabilitySourceCategory.Species,
                            CalculatedStatContributionSourceCategory.Species,
                            previousSpeciesSource,
                            restoring);
                        BodyOperationResult rollbackAnatomy = anatomyRuntime.Build(
                            ActorBodyId,
                            previousRevision,
                            rollbackSpecies,
                            rollbackSpecies.AnatomyDefinition,
                            ToPresenceOverrideDictionary(previousAnatomy),
                            restoring,
                            preserveRevision: true);
                        if (rollbackAnatomy.Succeeded)
                        {
                            anatomyRuntime.SetRevision(previousAnatomy == null ? 0L : previousAnatomy.anatomyRevision);
                        }

                        if (previousCondition != null)
                        {
                            conditionRuntime.RestoreFromSaveData(previousCondition, anatomyRuntime.CreateSnapshot(BodyRevision), registry);
                        }

                        if (previousVitalProcesses != null && previousVitalProcesses.resources != null)
                        {
                            vitalProcessRuntime.RestoreFromSaveData(previousVitalProcesses, rollbackSpecies, anatomyRuntime.CreateSnapshot(BodyRevision), conditionRuntime.CreateSnapshot(), registry);
                        }

                        if (previousBiologicalHazards != null && previousBiologicalHazards.activeHazards != null)
                        {
                            biologicalHazardRuntime.RestoreFromSaveData(previousBiologicalHazards, ActorBodyId, vitalProcessRuntime, registry);
                        }

                        if (previousBiologicalCompatibility != null)
                        {
                            BuildBiologicalCompatibilityForCurrentBody(restoring, preserveRevision: true);
                        }

                        if (previousBiologicalConditions != null)
                        {
                            biologicalConditionRuntime.RestoreFromSaveData(previousBiologicalConditions, CreateSnapshot(includeRecovery: false), registry);
                        }

                        if (previousBiologicalRecovery != null)
                        {
                            biologicalRecoveryRuntime.RestoreFromSaveData(previousBiologicalRecovery, CreateSnapshot(), registry);
                        }
                    }
                }

                return BodyOperationResult.Failure(BodyOperationResultCode.ContributionApplicationFailure, $"Species assignment rolled back: {exception.Message}", snapshot: CreateSnapshot());
            }
        }

        public BodySaveData CreateSaveData()
        {
            return new BodySaveData
            {
                schemaVersion = BodySaveData.CurrentSchemaVersion,
                actorBodyId = ActorBodyId,
                personId = PersonId,
                speciesDefinitionId = SpeciesDefinitionId,
                bodyRevision = BodyRevision,
                anatomy = anatomyRuntime.CreateSaveData(),
                condition = conditionRuntime.CreateSaveData(),
                vitalProcesses = vitalProcessRuntime.CreateSaveData(),
                biologicalHazards = biologicalHazardRuntime.CreateSaveData(),
                biologicalConditions = biologicalConditionRuntime.CreateSaveData(),
                biologicalRecovery = biologicalRecoveryRuntime.CreateSaveData(),
                transformation = transformationRuntime.CreateSaveData()
            };
        }

        public BodyOperationResult RestoreFromSaveData(BodySaveData saveData, DefinitionRegistry definitionRegistry, string expectedActorBodyId, string expectedPersonId, bool restoring)
        {
            if (!ValidateSaveData(saveData, definitionRegistry, expectedActorBodyId, expectedPersonId, out string failureReason))
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, failureReason);
            }

            registry = definitionRegistry ?? registry;
            Readiness = BodyReadinessState.Restoring;
            BodyOperationResult result;
            using (SuppressEvents())
            {
                result = AssignSpecies(saveData.speciesDefinitionId, restoring: true, "Body restore");
            }

            if (!result.Succeeded)
            {
                return result;
            }

            BodyRevision = Math.Max(1L, saveData.bodyRevision);
            SpeciesDefinition restoredSpecies = Species;
            AnatomySaveData anatomySave = saveData.anatomy;
            BodyOperationResult anatomyResult = anatomyRuntime.Build(
                ActorBodyId,
                BodyRevision,
                restoredSpecies,
                restoredSpecies == null ? null : restoredSpecies.AnatomyDefinition,
                ToPresenceOverrideDictionary(anatomySave),
                restoring: true,
                preserveRevision: true);
            if (!anatomyResult.Succeeded)
            {
                return anatomyResult;
            }

            anatomyRuntime.SetRevision(anatomySave == null ? 1L : Math.Max(1L, anatomySave.anatomyRevision));
            if (saveData.schemaVersion >= 3 && saveData.condition != null)
            {
                LocalizedStructuralDamageResult conditionResult = conditionRuntime.RestoreFromSaveData(saveData.condition, anatomyRuntime.CreateSnapshot(BodyRevision), registry);
                if (!conditionResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, conditionResult.Message, snapshot: CreateSnapshot());
                }
            }
            else
            {
                conditionRuntime.BuildHealthy(ActorBodyId, anatomyRuntime.CreateSnapshot(BodyRevision), registry, restoring: true, preserveRevision: true);
            }

            if (saveData.schemaVersion >= 4 && saveData.vitalProcesses != null)
            {
                VitalResourceMutationResult vitalResult = vitalProcessRuntime.RestoreFromSaveData(saveData.vitalProcesses, restoredSpecies, anatomyRuntime.CreateSnapshot(BodyRevision), conditionRuntime.CreateSnapshot(), registry);
                if (!vitalResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, vitalResult.Message, snapshot: CreateSnapshot());
                }
            }
            else
            {
                VitalResourceMutationResult vitalResult = BuildVitalProcessesForCurrentBody(restoring: true, preserveRevision: true);
                if (!vitalResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, vitalResult.Message, snapshot: CreateSnapshot());
                }
            }

            if (saveData.schemaVersion >= 5 && saveData.biologicalHazards != null)
            {
                BiologicalHazardOperationResult hazardResult = biologicalHazardRuntime.RestoreFromSaveData(saveData.biologicalHazards, ActorBodyId, vitalProcessRuntime, registry);
                if (!hazardResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, hazardResult.Message, snapshot: CreateSnapshot());
                }
            }
            else
            {
                BiologicalHazardOperationResult hazardResult = BuildBiologicalHazardsForCurrentBody(restoring: true, preserveRevision: true);
                if (!hazardResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, hazardResult.Message, snapshot: CreateSnapshot());
                }
            }

            BiologicalCompatibilityOperationResult compatibilityResult = BuildBiologicalCompatibilityForCurrentBody(restoring: true, preserveRevision: true);
            if (!compatibilityResult.Succeeded)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, compatibilityResult.Message, snapshot: CreateSnapshot());
            }

            if (saveData.schemaVersion >= 8 && saveData.biologicalConditions != null)
            {
                BiologicalConditionResult biologicalConditionResult = biologicalConditionRuntime.RestoreFromSaveData(saveData.biologicalConditions, CreateSnapshot(includeRecovery: false), registry);
                if (!biologicalConditionResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, biologicalConditionResult.Message, snapshot: CreateSnapshot());
                }
            }
            else
            {
                BiologicalConditionResult biologicalConditionResult = BuildBiologicalConditionsForCurrentBody(restoring: true, preserveRevision: true);
                if (!biologicalConditionResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, biologicalConditionResult.Message, snapshot: CreateSnapshot());
                }
            }

            if (saveData.schemaVersion >= 6 && saveData.biologicalRecovery != null)
            {
                BiologicalRecoveryResult recoveryResult = biologicalRecoveryRuntime.RestoreFromSaveData(saveData.biologicalRecovery, CreateSnapshot(includeRecovery: false), registry);
                if (!recoveryResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, recoveryResult.Message, snapshot: CreateSnapshot());
                }
            }
            else
            {
                BiologicalRecoveryResult recoveryResult = BuildBiologicalRecoveryForCurrentBody(restoring: true, preserveRevision: true);
                if (!recoveryResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, recoveryResult.Message, snapshot: CreateSnapshot());
                }
            }

            if (saveData.schemaVersion >= 7 && saveData.transformation != null)
            {
                BodyTransformationResult transformationResult = transformationRuntime.RestoreFromSaveData(saveData.transformation, this, registry);
                if (!transformationResult.Succeeded)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.RestoreResolutionFailure, transformationResult.Message, snapshot: CreateSnapshot());
                }
            }
            else
            {
                BuildTransformationForCurrentBody(restoring: true, preserveRevision: true);
            }

            Readiness = BodyReadinessState.Ready;
            return BodyOperationResult.Success("Body restored.", CreateSnapshot());
        }

        public static bool ValidateSaveData(BodySaveData saveData, DefinitionRegistry registry, string expectedActorBodyId, string expectedPersonId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Body save data is missing.";
                return false;
            }

            if (saveData.schemaVersion < 1 || saveData.schemaVersion > BodySaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported body schema version {saveData.schemaVersion}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveData.actorBodyId))
            {
                failureReason = "Body save data is missing an Actor/body ID.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedActorBodyId) && !string.Equals(saveData.actorBodyId, expectedActorBodyId, StringComparison.Ordinal))
            {
                failureReason = $"Saved body actor '{saveData.actorBodyId}' does not match current actor '{expectedActorBodyId}'.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedPersonId) && !string.IsNullOrWhiteSpace(saveData.personId) && !string.Equals(saveData.personId, expectedPersonId, StringComparison.Ordinal))
            {
                failureReason = $"Saved body person '{saveData.personId}' does not match current person '{expectedPersonId}'.";
                return false;
            }

            if (registry == null || !registry.TryGet(saveData.speciesDefinitionId, out SpeciesDefinition species) || species == null)
            {
                failureReason = $"Saved body references unknown Species '{saveData.speciesDefinitionId}'.";
                return false;
            }

            if (species.BiologicalClassification == null || species.BodyForm == null)
            {
                failureReason = $"Saved body Species '{species.Id}' has incomplete classification or body form.";
                return false;
            }

            if (species.AnatomyDefinition == null)
            {
                failureReason = $"Saved body Species '{species.Id}' has no Anatomy definition.";
                return false;
            }

            if (saveData.schemaVersion >= 2 && saveData.anatomy != null)
            {
                if (!string.IsNullOrWhiteSpace(saveData.anatomy.actorBodyId) && !string.Equals(saveData.anatomy.actorBodyId, saveData.actorBodyId, StringComparison.Ordinal))
                {
                    failureReason = $"Saved anatomy body '{saveData.anatomy.actorBodyId}' does not match saved body '{saveData.actorBodyId}'.";
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(saveData.anatomy.anatomyDefinitionId) && !string.Equals(saveData.anatomy.anatomyDefinitionId, species.AnatomyDefinition.Id, StringComparison.Ordinal))
                {
                    failureReason = $"Saved anatomy '{saveData.anatomy.anatomyDefinitionId}' does not match Species anatomy '{species.AnatomyDefinition.Id}'.";
                    return false;
                }
            }

            if (saveData.schemaVersion >= 3 && saveData.condition != null)
            {
                if (!string.IsNullOrWhiteSpace(saveData.condition.actorBodyId) && !string.Equals(saveData.condition.actorBodyId, saveData.actorBodyId, StringComparison.Ordinal))
                {
                    failureReason = $"Saved condition body '{saveData.condition.actorBodyId}' does not match saved body '{saveData.actorBodyId}'.";
                    return false;
                }

                AnatomySnapshot validationAnatomy = new AnatomySnapshot(
                    saveData.actorBodyId,
                    species.AnatomyDefinition.Id,
                    AnatomyReadinessState.Ready,
                    Math.Max(1L, saveData.bodyRevision),
                    saveData.anatomy == null ? 1L : Math.Max(1L, saveData.anatomy.anatomyRevision),
                    species.AnatomyDefinition.Nodes.FirstOrDefault(node => node != null && string.IsNullOrWhiteSpace(node.ParentNodeId))?.NodeId ?? string.Empty,
                    species.AnatomyDefinition.Nodes.Where(node => node != null).Select(node => new AnatomyNodeSnapshot(
                        node.NodeId,
                        $"validation.{saveData.actorBodyId}.{species.AnatomyDefinition.Id}.{node.NodeId}",
                        node.DisplayName,
                        node.Category,
                        node.ParentNodeId,
                        node.RegionNodeId,
                        node.BodySide,
                        node.DefaultPresence,
                        node.InternalStructure,
                        node.Corporeal,
                        node.Targetable,
                        node.Vital,
                        node.Optional,
                        node.RelativeTargetingWeight,
                        node.RepeatGroup,
                        node.MirrorGroup,
                        Array.Empty<string>(),
                        node.EquipmentTagIds,
                        node.DamageTagIds)).ToArray(),
                    true,
                    Array.Empty<string>());
                if (!BodyConditionRuntime.ValidateSaveData(saveData.condition, validationAnatomy, registry, out failureReason))
                {
                    return false;
                }
            }

            if (saveData.schemaVersion >= 4)
            {
                if (saveData.vitalProcesses == null)
                {
                    failureReason = "Body save data schema 4 is missing vital process data.";
                    return false;
                }

                AnatomySnapshot validationAnatomy = CreateValidationAnatomySnapshot(saveData, species);
                BodyConditionRuntime validationConditionRuntime = new BodyConditionRuntime();
                if (saveData.condition != null)
                {
                    LocalizedStructuralDamageResult conditionResult = validationConditionRuntime.RestoreFromSaveData(saveData.condition, validationAnatomy, registry);
                    if (!conditionResult.Succeeded)
                    {
                        failureReason = conditionResult.Message;
                        return false;
                    }
                }
                else
                {
                    LocalizedStructuralDamageResult conditionResult = validationConditionRuntime.BuildHealthy(saveData.actorBodyId, validationAnatomy, registry, restoring: true, preserveRevision: true);
                    if (!conditionResult.Succeeded)
                    {
                        failureReason = conditionResult.Message;
                        return false;
                    }
                }

                if (!VitalProcessRuntime.ValidateSaveData(saveData.vitalProcesses, species, validationAnatomy, validationConditionRuntime.CreateSnapshot(), registry, out failureReason))
                {
                    return false;
                }
            }

            if (saveData.schemaVersion >= 5)
            {
                if (saveData.biologicalHazards == null)
                {
                    failureReason = "Body save data schema 5 is missing biological hazard data.";
                    return false;
                }

                if (!BiologicalHazardRuntime.ValidateSaveData(saveData.biologicalHazards, saveData.actorBodyId, registry, out failureReason))
                {
                    return false;
                }
            }

            if (saveData.schemaVersion >= 6)
            {
                if (saveData.biologicalRecovery == null)
                {
                    failureReason = "Body save data schema 6 is missing biological recovery data.";
                    return false;
                }

                BodyConditionRuntime validationConditionRuntime = new BodyConditionRuntime();
                AnatomySnapshot validationAnatomy = CreateValidationAnatomySnapshot(saveData, species);
                if (saveData.condition != null)
                {
                    LocalizedStructuralDamageResult conditionResult = validationConditionRuntime.RestoreFromSaveData(saveData.condition, validationAnatomy, registry);
                    if (!conditionResult.Succeeded)
                    {
                        failureReason = conditionResult.Message;
                        return false;
                    }
                }
                else
                {
                    validationConditionRuntime.BuildHealthy(saveData.actorBodyId, validationAnatomy, registry, restoring: true, preserveRevision: true);
                }

                VitalProcessRuntime validationVitalRuntime = new VitalProcessRuntime();
                if (saveData.vitalProcesses != null)
                {
                    VitalResourceMutationResult vitalResult = validationVitalRuntime.RestoreFromSaveData(saveData.vitalProcesses, species, validationAnatomy, validationConditionRuntime.CreateSnapshot(), registry);
                    if (!vitalResult.Succeeded)
                    {
                        failureReason = vitalResult.Message;
                        return false;
                    }
                }

                BiologicalHazardRuntime validationHazardRuntime = new BiologicalHazardRuntime();
                if (saveData.biologicalHazards != null)
                {
                    BiologicalHazardOperationResult hazardResult = validationHazardRuntime.RestoreFromSaveData(saveData.biologicalHazards, saveData.actorBodyId, validationVitalRuntime, registry);
                    if (!hazardResult.Succeeded)
                    {
                        failureReason = hazardResult.Message;
                        return false;
                    }
                }

                BiologicalCompatibilityRuntime validationCompatibilityRuntime = new BiologicalCompatibilityRuntime();
                BodySnapshot validationBody = new BodySnapshot(
                    saveData.actorBodyId,
                    saveData.personId,
                    species.Id,
                    species.DisplayName,
                    species.BiologicalClassification == null ? string.Empty : species.BiologicalClassification.Id,
                    species.BodyForm == null ? string.Empty : species.BodyForm.Id,
                    BodyReadinessState.Ready,
                    Math.Max(1L, saveData.bodyRevision),
                    Array.Empty<string>(),
                    Array.Empty<BodyCapabilitySummary>(),
                    Array.Empty<BodyTraitSummary>(),
                    Array.Empty<BodyStatContributionSummary>(),
                    species.DefaultDefeatPolicy == null ? string.Empty : species.DefaultDefeatPolicy.Id,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    false,
                    validationAnatomy,
                    validationConditionRuntime.CreateSnapshot(),
                    validationVitalRuntime.CreateSnapshot(),
                    validationHazardRuntime.CreateSnapshot(),
                    null,
                    null,
                    true,
                    Array.Empty<string>());
                validationCompatibilityRuntime.BuildForBody(validationBody, registry, restoring: true, preserveRevision: true);
                validationBody = new BodySnapshot(
                    validationBody.ActorBodyId,
                    validationBody.PersonId,
                    validationBody.SpeciesId,
                    validationBody.SpeciesDisplayName,
                    validationBody.BiologicalClassificationId,
                    validationBody.BodyFormId,
                    validationBody.Readiness,
                    validationBody.BodyRevision,
                    validationBody.SpeciesTags,
                    validationBody.BiologicalCapabilities,
                    validationBody.SpeciesOwnedTraits,
                    validationBody.BiologicalStatContributions,
                    validationBody.DefeatPolicyId,
                    validationBody.RequiresBreathing,
                    validationBody.HasBlood,
                    validationBody.CanBecomeUnconscious,
                    validationBody.CanDie,
                    validationBody.CanBeRevived,
                    validationBody.AcceptsBiologicalHealing,
                    validationBody.AcceptsRepair,
                    validationBody.HasPhysicalBody,
                    validationBody.Anatomy,
                    validationBody.Condition,
                    validationBody.VitalProcesses,
                    validationBody.BiologicalHazards,
                    validationCompatibilityRuntime.CreateSnapshot(),
                    null,
                    true,
                    Array.Empty<string>());
                if (!BiologicalRecoveryRuntime.ValidateSaveData(saveData.biologicalRecovery, validationBody, registry, out failureReason))
                {
                    return false;
                }
            }

            if (saveData.schemaVersion >= 7)
            {
                if (saveData.transformation == null)
                {
                    failureReason = "Body save data schema 7 is missing transformation data.";
                    return false;
                }
            }

            if (saveData.schemaVersion >= 8)
            {
                if (saveData.biologicalConditions == null)
                {
                    failureReason = "Body save data schema 8 is missing Biological Condition data.";
                    return false;
                }

                BodyConditionRuntime validationConditionRuntime = new BodyConditionRuntime();
                AnatomySnapshot validationAnatomy = CreateValidationAnatomySnapshot(saveData, species);
                if (saveData.condition != null)
                {
                    LocalizedStructuralDamageResult conditionResult = validationConditionRuntime.RestoreFromSaveData(saveData.condition, validationAnatomy, registry);
                    if (!conditionResult.Succeeded)
                    {
                        failureReason = conditionResult.Message;
                        return false;
                    }
                }
                else
                {
                    validationConditionRuntime.BuildHealthy(saveData.actorBodyId, validationAnatomy, registry, restoring: true, preserveRevision: true);
                }

                VitalProcessRuntime validationVitalRuntime = new VitalProcessRuntime();
                if (saveData.vitalProcesses != null)
                {
                    VitalResourceMutationResult vitalResult = validationVitalRuntime.RestoreFromSaveData(saveData.vitalProcesses, species, validationAnatomy, validationConditionRuntime.CreateSnapshot(), registry);
                    if (!vitalResult.Succeeded)
                    {
                        failureReason = vitalResult.Message;
                        return false;
                    }
                }

                BiologicalHazardRuntime validationHazardRuntime = new BiologicalHazardRuntime();
                if (saveData.biologicalHazards != null)
                {
                    BiologicalHazardOperationResult hazardResult = validationHazardRuntime.RestoreFromSaveData(saveData.biologicalHazards, saveData.actorBodyId, validationVitalRuntime, registry);
                    if (!hazardResult.Succeeded)
                    {
                        failureReason = hazardResult.Message;
                        return false;
                    }
                }

                BiologicalCompatibilityRuntime validationCompatibilityRuntime = new BiologicalCompatibilityRuntime();
                BodySnapshot validationBody = CreateValidationBodySnapshot(saveData, species, validationAnatomy, validationConditionRuntime, validationVitalRuntime, validationHazardRuntime, null, null);
                validationCompatibilityRuntime.BuildForBody(validationBody, registry, restoring: true, preserveRevision: true);
                validationBody = CreateValidationBodySnapshot(saveData, species, validationAnatomy, validationConditionRuntime, validationVitalRuntime, validationHazardRuntime, validationCompatibilityRuntime.CreateSnapshot(), null);
                if (!BiologicalConditionRuntime.ValidateSaveData(saveData.biologicalConditions, validationBody, registry, out failureReason))
                {
                    return false;
                }
            }

            return true;
        }

        public BodySnapshot CreateSnapshot()
        {
            return CreateSnapshot(includeCompatibility: true, includeRecovery: true);
        }

        private BodySnapshot CreateSnapshot(bool includeCompatibility = true, bool includeRecovery = true)
        {
            SpeciesDefinition species = Species;
            BiologicalClassificationDefinition classification = species == null ? null : species.BiologicalClassification;
            BodyFormDefinition bodyForm = species == null ? null : species.BodyForm;
            List<string> diagnostics = new List<string>();
            bool coherent = ValidateBody(out string validationFailure);
            if (!string.IsNullOrWhiteSpace(validationFailure))
            {
                diagnostics.Add(validationFailure);
            }

            List<string> tagIds = species == null
                ? new List<string>()
                : species.Tags.Where(tag => tag != null).Select(tag => tag.Id).OrderBy(id => id, StringComparer.Ordinal).ToList();

            List<BodyCapabilitySummary> capabilitySummaries = BuildCapabilitySummaries();
            List<BodyTraitSummary> traitSummaries = BuildTraitSummaries(species);
            List<BodyStatContributionSummary> statSummaries = BuildStatSummaries(species, classification);
            AnatomySnapshot anatomySnapshot = anatomyRuntime.CreateSnapshot(BodyRevision);
            if (anatomySnapshot != null && !anatomySnapshot.Coherent)
            {
                diagnostics.AddRange(anatomySnapshot.Diagnostics);
            }

            BodyConditionSnapshot conditionSnapshot = conditionRuntime.CreateSnapshot();
            if (conditionSnapshot != null && !conditionSnapshot.Coherent)
            {
                diagnostics.AddRange(conditionSnapshot.Diagnostics);
            }

            VitalProcessSnapshot vitalSnapshot = vitalProcessRuntime.CreateSnapshot();
            if (vitalSnapshot != null && !vitalSnapshot.Coherent)
            {
                diagnostics.AddRange(vitalSnapshot.Diagnostics);
            }

            BiologicalHazardSnapshot hazardSnapshot = biologicalHazardRuntime.CreateSnapshot();
            if (hazardSnapshot != null && !hazardSnapshot.Coherent)
            {
                diagnostics.AddRange(hazardSnapshot.Diagnostics);
            }

            BiologicalCompatibilitySnapshot compatibilitySnapshot = includeCompatibility ? biologicalCompatibilityRuntime.CreateSnapshot() : null;
            if (compatibilitySnapshot != null && !compatibilitySnapshot.Coherent)
            {
                diagnostics.AddRange(compatibilitySnapshot.Diagnostics);
            }

            BiologicalRecoverySnapshot recoverySnapshot = includeRecovery ? biologicalRecoveryRuntime.CreateSnapshot() : null;
            if (recoverySnapshot != null && !recoverySnapshot.Coherent)
            {
                diagnostics.AddRange(recoverySnapshot.Diagnostics);
            }

            return new BodySnapshot(
                ActorBodyId,
                PersonId,
                species == null ? string.Empty : species.Id,
                species == null ? string.Empty : species.DisplayName,
                classification == null ? string.Empty : classification.Id,
                bodyForm == null ? string.Empty : bodyForm.Id,
                Readiness,
                BodyRevision,
                tagIds,
                capabilitySummaries,
                traitSummaries,
                statSummaries,
                species == null || species.DefaultDefeatPolicy == null ? string.Empty : species.DefaultDefeatPolicy.Id,
                EvaluateBoolean(BiologyCapabilityIds.RequiresBreathing),
                EvaluateBoolean(BiologyCapabilityIds.HasBlood),
                EvaluateBoolean(ActorLifecycleCapabilityIds.CanBecomeUnconscious),
                EvaluateBoolean(ActorLifecycleCapabilityIds.CanDie),
                EvaluateBoolean(ActorLifecycleCapabilityIds.CanBeRevived),
                EvaluateBoolean(BiologyCapabilityIds.AcceptsBiologicalHealing),
                EvaluateBoolean(BiologyCapabilityIds.AcceptsRepair),
                EvaluateBoolean(BiologyCapabilityIds.HasPhysicalBody),
                anatomySnapshot,
                conditionSnapshot,
                vitalSnapshot,
                hazardSnapshot,
                compatibilitySnapshot,
                recoverySnapshot,
                coherent
                    && anatomySnapshot != null && anatomySnapshot.Coherent
                    && conditionSnapshot != null && conditionSnapshot.Coherent
                    && vitalSnapshot != null && vitalSnapshot.Coherent
                    && hazardSnapshot != null && hazardSnapshot.Coherent
                    && (!includeCompatibility || compatibilitySnapshot == null || compatibilitySnapshot.Coherent)
                    && (!includeRecovery || recoverySnapshot == null || recoverySnapshot.Coherent),
                diagnostics);
        }

        private static BodySnapshot CreateValidationBodySnapshot(
            BodySaveData saveData,
            SpeciesDefinition species,
            AnatomySnapshot validationAnatomy,
            BodyConditionRuntime validationConditionRuntime,
            VitalProcessRuntime validationVitalRuntime,
            BiologicalHazardRuntime validationHazardRuntime,
            BiologicalCompatibilitySnapshot compatibilitySnapshot,
            BiologicalRecoverySnapshot recoverySnapshot)
        {
            return new BodySnapshot(
                saveData.actorBodyId,
                saveData.personId,
                species.Id,
                species.DisplayName,
                species.BiologicalClassification == null ? string.Empty : species.BiologicalClassification.Id,
                species.BodyForm == null ? string.Empty : species.BodyForm.Id,
                BodyReadinessState.Ready,
                Math.Max(1L, saveData.bodyRevision),
                Array.Empty<string>(),
                Array.Empty<BodyCapabilitySummary>(),
                Array.Empty<BodyTraitSummary>(),
                Array.Empty<BodyStatContributionSummary>(),
                species.DefaultDefeatPolicy == null ? string.Empty : species.DefaultDefeatPolicy.Id,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                validationAnatomy,
                validationConditionRuntime.CreateSnapshot(),
                validationVitalRuntime.CreateSnapshot(),
                validationHazardRuntime.CreateSnapshot(),
                compatibilitySnapshot,
                recoverySnapshot,
                true,
                Array.Empty<string>());
        }

        public bool ValidateBody(out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(ActorBodyId))
            {
                Readiness = BodyReadinessState.Invalid;
                failureReason = "Body runtime is missing an exact Actor/body ID.";
                return false;
            }

            if (registry == null)
            {
                Readiness = BodyReadinessState.Invalid;
                failureReason = "Body runtime is missing a Definition registry.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(SpeciesDefinitionId))
            {
                Readiness = BodyReadinessState.Invalid;
                failureReason = "Body runtime is missing a Species assignment.";
                return false;
            }

            SpeciesDefinition species = ResolveSpecies(SpeciesDefinitionId);
            if (species == null)
            {
                Readiness = BodyReadinessState.Invalid;
                failureReason = $"Species '{SpeciesDefinitionId}' could not be resolved.";
                return false;
            }

            if (species.BiologicalClassification == null)
            {
                Readiness = BodyReadinessState.Invalid;
                failureReason = $"Species '{species.Id}' is missing a biological classification.";
                return false;
            }

            if (species.BodyForm == null)
            {
                Readiness = BodyReadinessState.Invalid;
                failureReason = $"Species '{species.Id}' is missing a body form.";
                return false;
            }

            if (species.AnatomyDefinition == null)
            {
                Readiness = BodyReadinessState.Invalid;
                failureReason = $"Species '{species.Id}' is missing an Anatomy definition.";
                return false;
            }

            if (!anatomyRuntime.IsReady)
            {
                BodyOperationResult anatomyResult = BuildAnatomyForCurrentSpecies(restoring: false, null, preserveRevision: false);
                if (!anatomyResult.Succeeded)
                {
                    Readiness = BodyReadinessState.Invalid;
                    failureReason = anatomyResult.Message;
                    return false;
                }
            }

            if (!conditionRuntime.IsReady)
            {
                LocalizedStructuralDamageResult conditionResult = conditionRuntime.BuildHealthy(ActorBodyId, anatomyRuntime.CreateSnapshot(BodyRevision), registry, restoring: false, preserveRevision: false);
                if (!conditionResult.Succeeded)
                {
                    Readiness = BodyReadinessState.Invalid;
                    failureReason = conditionResult.Message;
                    return false;
                }
            }

            if (!vitalProcessRuntime.IsReady)
            {
                VitalResourceMutationResult vitalResult = BuildVitalProcessesForCurrentBody(restoring: false, preserveRevision: false);
                if (!vitalResult.Succeeded)
                {
                    Readiness = BodyReadinessState.Invalid;
                    failureReason = vitalResult.Message;
                    return false;
                }
            }

            if (Readiness != BodyReadinessState.Restoring && Readiness != BodyReadinessState.ApplyingBiologicalContributions)
            {
                Readiness = BodyReadinessState.Ready;
            }

            return true;
        }

        private BodyOperationResult ResolveAssignment(string requestedSpeciesId, bool preview, out SpeciesDefinition species, out BiologicalClassificationDefinition classification, out BodyFormDefinition bodyForm)
        {
            species = null;
            classification = null;
            bodyForm = null;

            if (string.IsNullOrWhiteSpace(ActorBodyId))
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingActorBody, "Body assignment requires an exact Actor/body ID.", preview);
            }

            if (registry == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.RuntimeNotReady, "Body assignment requires a Definition registry.", preview);
            }

            if (string.IsNullOrWhiteSpace(requestedSpeciesId) || !registry.TryGet(requestedSpeciesId, out species) || species == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingSpecies, $"Species '{requestedSpeciesId}' is not configured.", preview);
            }

            classification = species.BiologicalClassification;
            if (classification == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingClassification, $"Species '{species.Id}' is missing a biological classification.", preview);
            }

            bodyForm = species.BodyForm;
            if (bodyForm == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingBodyForm, $"Species '{species.Id}' is missing a body form.", preview);
            }

            if (species.AnatomyDefinition == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingAnatomyDefinition, $"Species '{species.Id}' is missing an Anatomy definition.", preview);
            }

            if (!species.AnatomyDefinition.IsCompatibleWith(bodyForm, species))
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.InvalidAnatomyDefinition, $"Anatomy '{species.AnatomyDefinition.Id}' is incompatible with Species '{species.Id}'.", preview);
            }

            if (preview)
            {
                return BodyOperationResult.Success($"Preview resolved Species '{species.Id}' for body '{ActorBodyId}'.", CreateSnapshot(), preview: true);
            }

            return BodyOperationResult.Success("Assignment resolved.", null);
        }

        public BodyOperationResult RebuildAnatomy(bool restoring = false)
        {
            BodyOperationResult result = BuildAnatomyForCurrentSpecies(restoring, null, preserveRevision: false);
            if (result.Succeeded)
            {
                BodyOperationResult operation = BodyOperationResult.Success("Anatomy rebuilt.", CreateSnapshot());
                RaiseChanged(operation, restoring);
                return operation;
            }

            return result;
        }

        public BodyOperationResult SetAnatomyPresenceOverride(string nodeId, AnatomyPresenceState presence, bool restoring = false)
        {
            SpeciesDefinition species = Species;
            if (species == null || species.AnatomyDefinition == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingAnatomyDefinition, "Anatomy presence override requires a resolved Species anatomy.");
            }

            BodyOperationResult result = anatomyRuntime.SetPresenceOverride(species.AnatomyDefinition, nodeId, presence);
            if (!result.Succeeded)
            {
                return result;
            }

            BodyOperationResult operation = BodyOperationResult.Success(result.Message, CreateSnapshot(), duplicate: result.Duplicate);
            RaiseChanged(operation, restoring);
            return operation;
        }

        public BodyOperationResult ValidateAnatomy()
        {
            AnatomySnapshot snapshot = anatomyRuntime.CreateSnapshot(BodyRevision);
            return snapshot != null && snapshot.Coherent && anatomyRuntime.IsReady
                ? BodyOperationResult.Success("Anatomy is ready.", CreateSnapshot())
                : BodyOperationResult.Failure(BodyOperationResultCode.InvalidAnatomyDefinition, snapshot == null ? "Anatomy snapshot is missing." : string.Join(" ", snapshot.Diagnostics), snapshot: CreateSnapshot());
        }

        public AnatomySnapshot CreateAnatomySnapshot()
        {
            return anatomyRuntime.CreateSnapshot(BodyRevision);
        }

        private VitalResourceMutationResult BuildVitalProcessesForCurrentBody(bool restoring, bool preserveRevision)
        {
            return vitalProcessRuntime.BuildForBody(ActorBodyId, Species, anatomyRuntime.CreateSnapshot(BodyRevision), conditionRuntime.CreateSnapshot(), registry, restoring, preserveRevision);
        }

        private BiologicalHazardOperationResult BuildBiologicalHazardsForCurrentBody(bool restoring, bool preserveRevision)
        {
            return biologicalHazardRuntime.BuildForBody(ActorBodyId, vitalProcessRuntime, registry, restoring, preserveRevision);
        }

        private BiologicalCompatibilityOperationResult BuildBiologicalCompatibilityForCurrentBody(bool restoring, bool preserveRevision)
        {
            return biologicalCompatibilityRuntime.BuildForBody(CreateSnapshot(includeCompatibility: false), registry, restoring, preserveRevision);
        }

        private BiologicalConditionResult BuildBiologicalConditionsForCurrentBody(bool restoring, bool preserveRevision)
        {
            return biologicalConditionRuntime.BuildForBody(CreateSnapshot(includeRecovery: false), registry, restoring, preserveRevision);
        }

        private BiologicalRecoveryResult BuildBiologicalRecoveryForCurrentBody(bool restoring, bool preserveRevision)
        {
            return biologicalRecoveryRuntime.BuildForBody(CreateSnapshot(includeRecovery: false), registry, restoring, preserveRevision);
        }

        private void BuildTransformationForCurrentBody(bool restoring, bool preserveRevision)
        {
            transformationRuntime.Configure(this, registry, restoring, preserveRevision);
        }

        private BodyOperationResult BuildAnatomyForCurrentSpecies(bool restoring, IReadOnlyDictionary<string, AnatomyPresenceState> overrides, bool preserveRevision)
        {
            SpeciesDefinition species = Species;
            if (species == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.MissingSpecies, "Anatomy build requires a resolved Species.");
            }

            BodyOperationResult result = anatomyRuntime.Build(ActorBodyId, BodyRevision, species, species.AnatomyDefinition, overrides ?? anatomyRuntime.PresenceOverrides, restoring, preserveRevision);
            if (result.Succeeded)
            {
                return result;
            }

            return result;
        }

        private static Dictionary<string, AnatomyPresenceState> ToPresenceOverrideDictionary(AnatomySaveData saveData)
        {
            Dictionary<string, AnatomyPresenceState> overrides = new Dictionary<string, AnatomyPresenceState>(StringComparer.Ordinal);
            if (saveData?.presenceOverrides == null)
            {
                return overrides;
            }

            foreach (AnatomyPresenceOverrideData entry in saveData.presenceOverrides)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.nodeId))
                {
                    continue;
                }

                overrides[entry.nodeId] = entry.presence;
            }

            return overrides;
        }

        private static AnatomySnapshot CreateValidationAnatomySnapshot(BodySaveData saveData, SpeciesDefinition species)
        {
            return new AnatomySnapshot(
                saveData.actorBodyId,
                species.AnatomyDefinition.Id,
                AnatomyReadinessState.Ready,
                Math.Max(1L, saveData.bodyRevision),
                saveData.anatomy == null ? 1L : Math.Max(1L, saveData.anatomy.anatomyRevision),
                species.AnatomyDefinition.Nodes.FirstOrDefault(node => node != null && string.IsNullOrWhiteSpace(node.ParentNodeId))?.NodeId ?? string.Empty,
                species.AnatomyDefinition.Nodes.Where(node => node != null).Select(node => new AnatomyNodeSnapshot(
                    node.NodeId,
                    $"validation.{saveData.actorBodyId}.{species.AnatomyDefinition.Id}.{node.NodeId}",
                    node.DisplayName,
                    node.Category,
                    node.ParentNodeId,
                    node.RegionNodeId,
                    node.BodySide,
                    node.DefaultPresence,
                    node.InternalStructure,
                    node.Corporeal,
                    node.Targetable,
                    node.Vital,
                    node.Optional,
                    node.RelativeTargetingWeight,
                    node.RepeatGroup,
                    node.MirrorGroup,
                    Array.Empty<string>(),
                    node.EquipmentTagIds,
                    node.DamageTagIds)).ToArray(),
                true,
                Array.Empty<string>());
        }

        private BodyOperationResult ApplyBiologicalSources(
            IReadOnlyList<BiologicalTraitGrantDefinition> traitGrants,
            IReadOnlyList<BiologicalCapabilityGrantDefinition> capabilityGrants,
            IReadOnlyList<BiologicalStatContributionDefinition> statContributions,
            TraitSourceCategory traitSourceCategory,
            CapabilitySourceCategory capabilitySourceCategory,
            CalculatedStatContributionSourceCategory statSourceCategory,
            string sourceId,
            bool restoring)
        {
            if (traits == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.TraitGrantFailure, "Body assignment requires CharacterTraitCollection.");
            }

            if (calculatedStats == null)
            {
                return BodyOperationResult.Failure(BodyOperationResultCode.CalculatedStatContributionFailure, "Body assignment requires CalculatedStatCollection.");
            }

            foreach (BiologicalTraitGrantDefinition grant in traitGrants ?? Array.Empty<BiologicalTraitGrantDefinition>())
            {
                if (grant == null || !grant.AlphaEnabled)
                {
                    continue;
                }

                if (grant.Trait == null)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.TraitGrantFailure, "Biological Trait grant is missing a Trait definition.");
                }

                TraitOperationResult traitResult = traits.GrantTrait(new TraitGrantRequest
                {
                    OwnerId = PersonId,
                    TraitDefinitionId = grant.Trait.Id,
                    RequestedLifecycle = grant.Lifecycle,
                    RequestedDiscovery = grant.Discovery,
                    SourceCategory = traitSourceCategory,
                    SourceId = sourceId,
                    Reason = "Biological body source",
                    Restoration = restoring,
                    Authority = "body-runtime",
                    RevokeOnSourceRemoval = true
                });

                if (!traitResult.Succeeded && traitResult.Code != "TraitConflict")
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.TraitGrantFailure, traitResult.Message);
                }
            }

            foreach (BiologicalCapabilityGrantDefinition grant in capabilityGrants ?? Array.Empty<BiologicalCapabilityGrantDefinition>())
            {
                if (grant == null || !grant.AlphaEnabled)
                {
                    continue;
                }

                if (grant.Capability == null)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.CapabilityApplicationFailure, "Biological Capability grant is missing a Capability definition.");
                }

                bool added = capabilities != null && capabilities.Add(grant.Capability, capabilitySourceCategory, sourceId,
                    grant.EntryId, grant.BooleanValue, grant.NumericValue, grant.Priority, grant.Blocker, restoring);
                if (!added)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.CapabilityApplicationFailure, $"Capability '{grant.Capability.Id}' could not be applied.");
                }
            }

            BiologicalStatContributionDefinition[] enabledStatContributions = (statContributions ?? Array.Empty<BiologicalStatContributionDefinition>())
                .Where(contribution => contribution != null && contribution.AlphaEnabled)
                .ToArray();
            if (enabledStatContributions.Length > 0)
            {
                calculatedStats.RemoveContributionsFromSource(statSourceCategory, sourceId, restoring);
            }

            BodyOperationResult statApplyResult = ApplyStatContributions(enabledStatContributions, statSourceCategory, sourceId, restoring);
            if (!statApplyResult.Succeeded)
            {
                calculatedStats.RemoveContributionsFromSource(statSourceCategory, sourceId, restoring);
                statApplyResult = ApplyStatContributions(enabledStatContributions, statSourceCategory, sourceId, restoring);
                if (!statApplyResult.Succeeded)
                {
                    return statApplyResult;
                }
            }

            return BodyOperationResult.Success("Biological sources applied.", null);
        }

        private BodyOperationResult ApplyStatContributions(
            IReadOnlyList<BiologicalStatContributionDefinition> statContributions,
            CalculatedStatContributionSourceCategory statSourceCategory,
            string sourceId,
            bool restoring)
        {
            foreach (BiologicalStatContributionDefinition contribution in statContributions ?? Array.Empty<BiologicalStatContributionDefinition>())
            {
                if (contribution.CalculatedStat == null)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.CalculatedStatContributionFailure, "Biological stat contribution is missing a Calculated Stat definition.");
                }

                bool added = calculatedStats.AddContribution(new RuntimeCalculatedStatContribution
                {
                    contributionId = contribution.ContributionId,
                    statId = contribution.CalculatedStat.Id,
                    sourceId = sourceId,
                    sourceCategory = (int)statSourceCategory,
                    kind = (int)contribution.Kind,
                    direction = (int)contribution.Direction,
                    magnitude = contribution.Magnitude,
                    priority = contribution.Priority
                }, out string failureReason, restoring);

                if (!added)
                {
                    return BodyOperationResult.Failure(BodyOperationResultCode.CalculatedStatContributionFailure, failureReason);
                }
            }

            return BodyOperationResult.Success("Biological stat sources applied.", null);
        }

        private void ClearBiologicalSources(string speciesSourceId, string classificationSourceId, bool restoring)
        {
            if (traits != null)
            {
                if (!string.IsNullOrWhiteSpace(speciesSourceId))
                {
                    ClearTraitSources(TraitSourceCategory.Species, speciesSourceId);
                    capabilities?.RemoveSource(CapabilitySourceCategory.Species, speciesSourceId, restoring);
                }

                if (!string.IsNullOrWhiteSpace(classificationSourceId))
                {
                    ClearTraitSources(TraitSourceCategory.BiologicalClassification, classificationSourceId);
                    capabilities?.RemoveSource(CapabilitySourceCategory.BiologicalClassification, classificationSourceId, restoring);
                }
            }

            calculatedStats?.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Species, speciesSourceId, restoring);
            calculatedStats?.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.BiologicalClassification, classificationSourceId, restoring);
        }

        private void ClearTraitSources(TraitSourceCategory category, string sourceId)
        {
            if (traits == null || string.IsNullOrWhiteSpace(sourceId))
            {
                return;
            }

            foreach (TraitSnapshot snapshot in traits.GetDevelopmentSnapshot())
            {
                if (snapshot?.Definition == null || snapshot.Record?.sourceRecords == null)
                {
                    continue;
                }

                if (snapshot.Record.sourceRecords.Any(source => source.sourceCategory == (int)category && string.Equals(source.sourceId, sourceId, StringComparison.Ordinal)))
                {
                    traits.RemoveTraitSource(snapshot.Definition.Id, category, sourceId, TraitFinalSourcePolicy.Remove);
                }
            }
        }

        private void ReapplyCurrentBiologicalSources(bool restoring)
        {
            SpeciesDefinition species = ResolveSpecies(SpeciesDefinitionId);
            if (species == null || species.BiologicalClassification == null)
            {
                return;
            }

            string speciesSourceId = SpeciesSourceId(ActorBodyId, species.Id);
            string classificationSourceId = ClassificationSourceId(ActorBodyId, species.BiologicalClassification.Id);
            ClearBiologicalSources(speciesSourceId, classificationSourceId, restoring);
            ApplyBiologicalSources(
                species.BiologicalClassification.DefaultTraitGrants,
                species.BiologicalClassification.DefaultCapabilityGrants,
                species.BiologicalClassification.DefaultStatContributions,
                TraitSourceCategory.BiologicalClassification,
                CapabilitySourceCategory.BiologicalClassification,
                CalculatedStatContributionSourceCategory.BiologicalClassification,
                classificationSourceId,
                restoring);
            ApplyBiologicalSources(
                species.DefaultBodyTraits,
                species.DefaultBooleanCapabilities.Concat(species.DefaultNumericCapabilities).ToArray(),
                species.CalculatedStatContributions,
                TraitSourceCategory.Species,
                CapabilitySourceCategory.Species,
                CalculatedStatContributionSourceCategory.Species,
                speciesSourceId,
                restoring);
            appliedSpeciesSourceId = speciesSourceId;
            appliedClassificationSourceId = classificationSourceId;
        }

        private SpeciesDefinition ResolveSpecies(string speciesId)
        {
            if (string.IsNullOrWhiteSpace(speciesId) || registry == null)
            {
                return null;
            }

            return registry.TryGet(speciesId, out SpeciesDefinition species) ? species : null;
        }

        private bool EvaluateBoolean(string capabilityId)
        {
            return capabilities != null && capabilities.Evaluate(capabilityId).BooleanValue;
        }

        private List<BodyCapabilitySummary> BuildCapabilitySummaries()
        {
            if (capabilities == null)
            {
                return new List<BodyCapabilitySummary>();
            }

            string[] ids =
            {
                BiologyCapabilityIds.IsLiving,
                BiologyCapabilityIds.IsUndead,
                BiologyCapabilityIds.IsConstruct,
                BiologyCapabilityIds.IsSpirit,
                BiologyCapabilityIds.HasBlood,
                BiologyCapabilityIds.RequiresBreathing,
                BiologyCapabilityIds.CanBePoisoned,
                BiologyCapabilityIds.CanContractDisease,
                BiologyCapabilityIds.AcceptsBiologicalHealing,
                BiologyCapabilityIds.AcceptsRepair,
                BiologyCapabilityIds.IsCorporeal,
                BiologyCapabilityIds.HasPhysicalBody,
                ActorLifecycleCapabilityIds.CanBecomeUnconscious,
                ActorLifecycleCapabilityIds.CanDie,
                ActorLifecycleCapabilityIds.CanBeRevived,
                BiologyCapabilityIds.CanBreathe,
                BiologyCapabilityIds.CanBleed
            };

            return ids.Select(id => capabilities.Evaluate(id))
                .Select(snapshot => new BodyCapabilitySummary(snapshot.CapabilityId, snapshot.BooleanValue, snapshot.NumericValue, snapshot.Blocked))
                .ToList();
        }

        private static List<BodyTraitSummary> BuildTraitSummaries(SpeciesDefinition species)
        {
            return species == null
                ? new List<BodyTraitSummary>()
                : species.DefaultBodyTraits
                    .Where(grant => grant?.Trait != null && grant.AlphaEnabled)
                    .Select(grant => new BodyTraitSummary(grant.Trait.Id, grant.Trait.DisplayName))
                    .ToList();
        }

        private static List<BodyStatContributionSummary> BuildStatSummaries(SpeciesDefinition species, BiologicalClassificationDefinition classification)
        {
            List<BodyStatContributionSummary> summaries = new List<BodyStatContributionSummary>();
            if (classification != null)
            {
                summaries.AddRange(classification.DefaultStatContributions
                    .Where(contribution => contribution?.CalculatedStat != null && contribution.AlphaEnabled)
                    .Select(contribution => new BodyStatContributionSummary(contribution.CalculatedStat.Id, classification.Id, contribution.Magnitude, contribution.Direction.ToString())));
            }

            if (species != null)
            {
                summaries.AddRange(species.CalculatedStatContributions
                    .Where(contribution => contribution?.CalculatedStat != null && contribution.AlphaEnabled)
                    .Select(contribution => new BodyStatContributionSummary(contribution.CalculatedStat.Id, species.Id, contribution.Magnitude, contribution.Direction.ToString())));
            }

            return summaries;
        }

        private void RaiseChanged(BodyOperationResult result, bool restoring)
        {
            if (!suppressEvents)
            {
                BodyChanged?.Invoke(this, result, restoring);
            }
        }

        private IDisposable SuppressEvents()
        {
            suppressEvents = true;
            return new Suppression(this);
        }

        private static string SpeciesSourceId(string actorBodyId, string speciesId)
        {
            return $"body.species.{actorBodyId}.{speciesId}";
        }

        private static string ClassificationSourceId(string actorBodyId, string classificationId)
        {
            return $"body.classification.{actorBodyId}.{classificationId}";
        }

        private sealed class Suppression : IDisposable
        {
            private readonly ActorBodyRuntime owner;

            public Suppression(ActorBodyRuntime owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner != null)
                {
                    owner.suppressEvents = false;
                }
            }
        }
    }
}
