using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Capabilities;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Traits
{
    public sealed class CharacterTraitCollection : MonoBehaviour
    {
        [SerializeField] private CalculatedStatCollection calculatedStats;
        [SerializeField] private CharacterSkillCollection skills;
        [SerializeField] private CharacterAbilityCollection abilities;
        [SerializeField] private CharacterCapabilityCollection capabilities;
        [SerializeField] private string ownerId = PersistenceService.LocalPlayerId;

        private readonly Dictionary<string, TraitDefinition> definitionsById = new Dictionary<string, TraitDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, RuntimeTraitRecord> recordsByTraitId = new Dictionary<string, RuntimeTraitRecord>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private bool suppressEvents;

        public event Action<CharacterTraitCollection, TraitOperationResult, bool> TraitsChanged;
        public event Action<CharacterTraitCollection, RuntimeTraitRecord, bool> TraitRecordChanged;

        public bool IsConfigured { get; private set; }
        public IReadOnlyList<RuntimeTraitRecord> TraitRecords => recordsByTraitId.Values.Select(TraitRuntimeCloner.Clone).ToList();

        private void Awake()
        {
            if (calculatedStats == null)
            {
                calculatedStats = GetComponent<CalculatedStatCollection>();
            }

            if (skills == null)
            {
                skills = GetComponent<CharacterSkillCollection>();
            }

            abilities = abilities == null ? GetComponent<CharacterAbilityCollection>() : abilities;
            capabilities = capabilities == null ? GetComponent<CharacterCapabilityCollection>() : capabilities;

        }

        public void Configure(DefinitionRegistry definitionRegistry, CalculatedStatCollection statCollection = null, CharacterSkillCollection skillCollection = null, CharacterCapabilityCollection capabilityCollection = null, string owner = "")
        {
            registry = definitionRegistry;
            Configure(
                definitionRegistry == null ? Enumerable.Empty<TraitDefinition>() : definitionRegistry.DefinitionsById.Values.OfType<TraitDefinition>(),
                statCollection,
                skillCollection,
                capabilityCollection,
                owner);
        }

        public void Configure(IEnumerable<TraitDefinition> definitions, CalculatedStatCollection statCollection = null, CharacterSkillCollection skillCollection = null, CharacterCapabilityCollection capabilityCollection = null, string owner = "")
        {
            calculatedStats = statCollection == null ? calculatedStats == null ? GetComponent<CalculatedStatCollection>() : calculatedStats : statCollection;
            skills = skillCollection == null ? skills == null ? GetComponent<CharacterSkillCollection>() : skills : skillCollection;
            abilities = abilities == null ? GetComponent<CharacterAbilityCollection>() : abilities;
            capabilities = capabilityCollection == null ? capabilities == null ? GetComponent<CharacterCapabilityCollection>() : capabilities : capabilityCollection;
            if (!string.IsNullOrWhiteSpace(owner))
            {
                ownerId = owner;
            }

            definitionsById.Clear();
            foreach (TraitDefinition definition in definitions ?? Enumerable.Empty<TraitDefinition>())
            {
                if (definition != null && definition.AlphaEnabled && !string.IsNullOrWhiteSpace(definition.Id) && !definitionsById.ContainsKey(definition.Id))
                {
                    definitionsById.Add(definition.Id, definition);
                }
            }

            IsConfigured = definitionsById.Count > 0;
            RebuildTraitEffects(restoring: false, notify: false);
        }

        public TraitOperationResult GrantTrait(TraitGrantRequest request)
        {
            request ??= new TraitGrantRequest();
            if (string.IsNullOrWhiteSpace(request.TraitDefinitionId) || !definitionsById.TryGetValue(request.TraitDefinitionId, out TraitDefinition definition))
            {
                return TraitOperationResult.Failure("UnknownTrait", $"Trait '{request?.TraitDefinitionId}' is not configured.");
            }

            RuntimeTraitRecord existing = FindCurrentRecord(definition.Id);
            TraitLifecycleState oldLifecycle = existing == null ? definition.DefaultLifecycle : (TraitLifecycleState)existing.lifecycleState;
            TraitDiscoveryState oldDiscovery = existing == null ? definition.DefaultDiscovery : (TraitDiscoveryState)existing.discoveryState;
            TraitLifecycleState requestedLifecycle = request.RequestedLifecycle ?? definition.DefaultLifecycle;
            TraitDiscoveryState requestedDiscovery = request.RequestedDiscovery ?? definition.DefaultDiscovery;
            List<string> conflicts = DetectConflicts(definition);
            if (conflicts.Count > 0 && !request.AllowConflictReplacement)
            {
                TraitOperationResult rejected = TraitOperationResult.Failure("TraitConflict", $"Trait '{definition.DisplayName}' conflicts with {string.Join(", ", conflicts)}.");
                rejected.Conflicts.AddRange(conflicts);
                return rejected;
            }

            if (conflicts.Count > 0 && !conflicts.All(conflict => (request.TraitsAuthorizedForReplacement ?? Array.Empty<string>()).Contains(conflict)))
            {
                TraitOperationResult rejected = TraitOperationResult.Failure("ReplacementNotAuthorized", "Conflicting Traits require explicit replacement authorization.");
                rejected.Conflicts.AddRange(conflicts);
                return rejected;
            }

            Dictionary<string, RuntimeTraitRecord> rollback = CloneRecords();
            TraitOperationResult result = TraitOperationResult.Success($"Granted {definition.DisplayName}.").WithTrait(definition.Id, oldLifecycle, requestedLifecycle, oldDiscovery, requestedDiscovery).WithRestoration(request.Restoration);
            try
            {
                foreach (string conflictId in conflicts)
                {
                    if (recordsByTraitId.TryGetValue(conflictId, out RuntimeTraitRecord blocker))
                    {
                        HistoricalizeInternal(blocker, $"Replaced by {definition.Id}", request.Restoration || request.Migration);
                        blocker.replacementHistory.Add(new RuntimeTraitReplacementRecord
                        {
                            oldTraitDefinitionId = conflictId,
                            newTraitDefinitionId = definition.Id,
                            reason = request.Reason,
                            replacedAtUtc = DateTime.UtcNow.ToString("O"),
                            replacedAtPlaytimeSeconds = 0d
                        });
                        result.ReplacedTraitIds.Add(conflictId);
                    }
                }

                bool sourceAdded;
                RuntimeTraitRecord record = existing ?? CreateRecord(definition.Id, request.OwnerId, requestedLifecycle, requestedDiscovery, request.Reason);
                if (existing == null)
                {
                    recordsByTraitId.Add(definition.Id, record);
                }

                sourceAdded = AddSource(record, request);
                if (existing != null)
                {
                    record.lifecycleState = (int)requestedLifecycle;
                    record.discoveryState = (int)MoreVisible(oldDiscovery, requestedDiscovery);
                    AddTransition(record, oldLifecycle, requestedLifecycle, oldDiscovery, (TraitDiscoveryState)record.discoveryState, request.Reason);
                }

                ApplyLinkedGrants(definition, record, request, result, new HashSet<string>(StringComparer.Ordinal));
                RebuildTraitEffects(request.Restoration || request.Migration, notify: false, result);
                result.WithSourceAdded(sourceAdded).WithEffectsChanged(true);
                RaiseChanged(result, request.Restoration || request.Migration);
                return result;
            }
            catch (Exception exception)
            {
                recordsByTraitId.Clear();
                foreach (KeyValuePair<string, RuntimeTraitRecord> pair in rollback)
                {
                    recordsByTraitId[pair.Key] = pair.Value;
                }

                RebuildTraitEffects(request.Restoration || request.Migration, notify: false);
                return TraitOperationResult.Failure("TraitTransactionRolledBack", $"Trait transaction rolled back: {exception.Message}");
            }
        }

        public TraitOperationResult RemoveTraitSource(string traitId, TraitSourceCategory category, string sourceId, TraitFinalSourcePolicy finalSourcePolicy = TraitFinalSourcePolicy.Remove)
        {
            if (!recordsByTraitId.TryGetValue(traitId, out RuntimeTraitRecord record))
            {
                return TraitOperationResult.Failure("TraitNotFound", $"Trait '{traitId}' is not present.");
            }

            int removed = record.sourceRecords.RemoveAll(source => source.sourceCategory == (int)category && string.Equals(source.sourceId, sourceId, StringComparison.Ordinal));
            if (removed == 0)
            {
                return TraitOperationResult.Failure("SourceNotFound", $"Trait source '{sourceId}' was not found.");
            }

            if (record.sourceRecords.Count == 0)
            {
                switch (finalSourcePolicy)
                {
                    case TraitFinalSourcePolicy.KeepDormant:
                        SetLifecycleInternal(record, TraitLifecycleState.Dormant, "Final source removed.");
                        break;
                    case TraitFinalSourcePolicy.KeepHistorical:
                        HistoricalizeInternal(record, "Final source removed.", false);
                        break;
                    case TraitFinalSourcePolicy.Remove:
                    default:
                        SetLifecycleInternal(record, TraitLifecycleState.Removed, "Final source removed.");
                        break;
                }
            }

            RemoveLinkedSourcesFor(traitId);
            RebuildTraitEffects(restoring: false);
            TraitOperationResult result = TraitOperationResult.Success($"Removed source '{sourceId}' from {traitId}.").WithTrait(traitId, null, (TraitLifecycleState)record.lifecycleState, null, (TraitDiscoveryState)record.discoveryState).WithEffectsChanged(true);
            RaiseChanged(result, false);
            return result;
        }

        public TraitOperationResult ActivateTrait(string traitId) => ChangeLifecycle(traitId, TraitLifecycleState.Active, "Activated Trait.");
        public TraitOperationResult SuppressTrait(string traitId) => ChangeLifecycle(traitId, TraitLifecycleState.Suppressed, "Suppressed Trait.");
        public TraitOperationResult UnsuppressTrait(string traitId) => ChangeLifecycle(traitId, TraitLifecycleState.Active, "Unsuppressed Trait.");
        public TraitOperationResult RemoveTrait(string traitId) => ChangeLifecycle(traitId, TraitLifecycleState.Removed, "Removed Trait.");
        public TraitOperationResult HistoricalizeTrait(string traitId) => ChangeLifecycle(traitId, TraitLifecycleState.Historical, "Historicalized Trait.");

        public TraitOperationResult SetDiscoveryState(string traitId, TraitDiscoveryState discoveryState)
        {
            if (!recordsByTraitId.TryGetValue(traitId, out RuntimeTraitRecord record))
            {
                return TraitOperationResult.Failure("TraitNotFound", $"Trait '{traitId}' is not present.");
            }

            TraitDiscoveryState old = (TraitDiscoveryState)record.discoveryState;
            record.discoveryState = (int)discoveryState;
            record.latestDiscoveryChangedAtUtc = DateTime.UtcNow.ToString("O");
            AddTransition(record, (TraitLifecycleState)record.lifecycleState, (TraitLifecycleState)record.lifecycleState, old, discoveryState, "Discovery changed.");
            TraitOperationResult result = TraitOperationResult.Success($"Trait discovery changed to {discoveryState}.").WithTrait(traitId, (TraitLifecycleState)record.lifecycleState, (TraitLifecycleState)record.lifecycleState, old, discoveryState);
            RaiseChanged(result, false);
            return result;
        }

        public bool HasTrait(string traitId, bool includeDormant = false, bool includeSuppressed = false)
        {
            if (!recordsByTraitId.TryGetValue(traitId, out RuntimeTraitRecord record))
            {
                return false;
            }

            TraitLifecycleState state = (TraitLifecycleState)record.lifecycleState;
            return state == TraitLifecycleState.Active
                || (includeDormant && state == TraitLifecycleState.Dormant)
                || (includeSuppressed && state == TraitLifecycleState.Suppressed);
        }

        public bool TryGetTrait(string traitId, out RuntimeTraitRecord record)
        {
            record = null;
            if (!recordsByTraitId.TryGetValue(traitId, out RuntimeTraitRecord found))
            {
                return false;
            }

            record = TraitRuntimeCloner.Clone(found);
            return true;
        }

        public IReadOnlyList<TraitSnapshot> GetActiveTraits() => BuildSnapshots(record => (TraitLifecycleState)record.lifecycleState == TraitLifecycleState.Active, revealAll: true);
        public IReadOnlyList<TraitSnapshot> GetKnownTraits() => BuildSnapshots(IsKnownForNormalUi, revealAll: false);
        public IReadOnlyList<TraitSnapshot> GetDevelopmentSnapshot() => BuildSnapshots(_ => true, revealAll: true);

        public TraitOperationResult RebuildTraitEffects(bool restoring = false)
        {
            return RebuildTraitEffects(restoring, notify: true);
        }

        public PlayerTraitsSaveData CreateSaveData(string playerId, string personId)
        {
            List<RuntimeTraitRecord> saveRecords = new List<RuntimeTraitRecord>();
            foreach (RuntimeTraitRecord runtimeRecord in recordsByTraitId.Values)
            {
                RuntimeTraitRecord record = TraitRuntimeCloner.Clone(runtimeRecord);
                record.sourceRecords.RemoveAll(IsDeterministicBodySource);
                record.suppressionSourceRecords.RemoveAll(IsDeterministicBodySource);
                if (record.sourceRecords.Count > 0)
                {
                    saveRecords.Add(record);
                }
            }

            return new PlayerTraitsSaveData
            {
                schemaVersion = PlayerTraitsSaveData.CurrentSchemaVersion,
                playerId = playerId ?? string.Empty,
                personId = personId ?? string.Empty,
                traits = saveRecords
            };
        }

        public bool RestoreFromSaveData(PlayerTraitsSaveData saveData, DefinitionRegistry definitionRegistry, CalculatedStatCollection statCollection, CharacterSkillCollection skillCollection, string expectedPlayerId, out string failureReason, bool restoring)
        {
            failureReason = string.Empty;
            if (!IsConfigured)
            {
                failureReason = "Character Traits must be initialized by CharacterSystemCoordinator before restore.";
                return false;
            }

            if (!ValidateSaveData(saveData, definitionRegistry, expectedPlayerId, out failureReason))
            {
                return false;
            }

            using (Suppress())
            {
                recordsByTraitId.Clear();
                foreach (RuntimeTraitRecord record in saveData.traits ?? new List<RuntimeTraitRecord>())
                {
                    recordsByTraitId[record.traitDefinitionId] = TraitRuntimeCloner.Clone(record);
                }
            }

            RebuildTraitEffects(restoring);
            return true;
        }

        public static bool ValidateSaveData(PlayerTraitsSaveData saveData, DefinitionRegistry registry, string expectedPlayerId, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Player Traits save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != PlayerTraitsSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported player Traits schema version {saveData.schemaVersion}.";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(expectedPlayerId) && !string.Equals(saveData.playerId, expectedPlayerId, StringComparison.Ordinal))
            {
                failureReason = $"Saved Traits owner '{saveData.playerId}' does not match current player '{expectedPlayerId}'.";
                return false;
            }

            if (registry == null)
            {
                failureReason = "Definition registry is not available for Trait restore.";
                return false;
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (RuntimeTraitRecord record in saveData.traits ?? new List<RuntimeTraitRecord>())
            {
                if (record == null || string.IsNullOrWhiteSpace(record.traitDefinitionId))
                {
                    failureReason = "Trait record is missing a Trait definition ID.";
                    return false;
                }

                if (!ids.Add(record.traitDefinitionId))
                {
                    failureReason = $"Duplicate Trait record '{record.traitDefinitionId}' in save data.";
                    return false;
                }

                if (!registry.TryGet(record.traitDefinitionId, out TraitDefinition _))
                {
                    failureReason = $"Trait record references unknown TraitDefinition '{record.traitDefinitionId}'.";
                    return false;
                }

                if (!Enum.IsDefined(typeof(TraitLifecycleState), record.lifecycleState) || !Enum.IsDefined(typeof(TraitDiscoveryState), record.discoveryState))
                {
                    failureReason = $"Trait record '{record.traitDefinitionId}' has invalid lifecycle or discovery state.";
                    return false;
                }

                HashSet<string> sourceKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (RuntimeTraitSourceRecord source in record.sourceRecords ?? new List<RuntimeTraitSourceRecord>())
                {
                    if (source == null || string.IsNullOrWhiteSpace(source.sourceId) || !Enum.IsDefined(typeof(TraitSourceCategory), source.sourceCategory))
                    {
                        failureReason = $"Trait record '{record.traitDefinitionId}' has an invalid source.";
                        return false;
                    }

                    if (!sourceKeys.Add($"{source.sourceCategory}:{source.sourceId}"))
                    {
                        failureReason = $"Trait record '{record.traitDefinitionId}' has duplicate source '{source.sourceId}'.";
                        return false;
                    }
                }
            }

            return true;
        }

        public string BuildDiagnosticSummary(bool includeHidden)
        {
            List<string> lines = new List<string>
            {
                "Feature 5.5 Traits And Capabilities",
                $"Configured Traits: {definitionsById.Count}",
                $"Runtime Records: {recordsByTraitId.Count}",
                $"Owner: {ownerId}"
            };

            IReadOnlyList<TraitSnapshot> snapshots = includeHidden ? GetDevelopmentSnapshot() : GetKnownTraits();
            if (snapshots.Count == 0)
            {
                lines.Add("Traits: None");
            }
            else
            {
                foreach (TraitSnapshot snapshot in snapshots)
                {
                    RuntimeTraitRecord record = snapshot.Record;
                    lines.Add($"{snapshot.PresentationName}: {(TraitLifecycleState)record.lifecycleState} / {(TraitDiscoveryState)record.discoveryState} Sources={record.sourceRecords.Count} ({record.traitDefinitionId})");
                }
            }

            lines.Add("Capabilities:");
            foreach (CapabilitySnapshot snapshot in capabilities == null ? Array.Empty<CapabilitySnapshot>() : capabilities.GetSnapshots())
            {
                string value = snapshot.ValueType == CapabilityValueType.Boolean ? snapshot.BooleanValue.ToString() : snapshot.NumericValue.ToString("0.###");
                lines.Add($"{snapshot.CapabilityId}: {value} Blocked={snapshot.Blocked} Sources={snapshot.Sources.Count}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        public bool IsImmuneTo(string damageTypeId)
        {
            return GetActiveTraitDefinitions().Any(trait => trait.ImmunityGrants.Any(grant => grant != null && grant.AlphaEnabled && grant.DamageType != null && string.Equals(grant.DamageType.Id, damageTypeId, StringComparison.Ordinal)));
        }

        public float GetResistance(string damageTypeId)
        {
            return Mathf.Clamp01(GetActiveTraitDefinitions()
                .SelectMany(trait => trait.ResistanceGrants)
                .Where(grant => grant != null && grant.AlphaEnabled && grant.DamageType != null && string.Equals(grant.DamageType.Id, damageTypeId, StringComparison.Ordinal))
                .Sum(grant => grant.ResistanceFraction));
        }

        private TraitOperationResult ChangeLifecycle(string traitId, TraitLifecycleState target, string reason)
        {
            if (!recordsByTraitId.TryGetValue(traitId, out RuntimeTraitRecord record))
            {
                return TraitOperationResult.Failure("TraitNotFound", $"Trait '{traitId}' is not present.");
            }

            TraitLifecycleState old = (TraitLifecycleState)record.lifecycleState;
            if (!IsValidTransition(old, target))
            {
                return TraitOperationResult.Failure("InvalidTransition", $"Cannot transition Trait '{traitId}' from {old} to {target}.");
            }

            SetLifecycleInternal(record, target, reason);
            RebuildTraitEffects(restoring: false);
            TraitOperationResult result = TraitOperationResult.Success($"Trait '{traitId}' changed to {target}.").WithTrait(traitId, old, target, (TraitDiscoveryState)record.discoveryState, (TraitDiscoveryState)record.discoveryState).WithEffectsChanged(true);
            RaiseChanged(result, false);
            return result;
        }

        private TraitOperationResult RebuildTraitEffects(bool restoring, bool notify, TraitOperationResult passthrough = null)
        {
            foreach (TraitDefinition definition in definitionsById.Values.ToList())
            {
                calculatedStats?.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Trait, TraitSourceId(definition.Id), restoring);
                capabilities?.RemoveSource(CapabilitySourceCategory.Trait, TraitSourceId(definition.Id), restoring);
                abilities?.RemoveSource(AbilityGrantSourceCategory.Trait, TraitSourceId(definition.Id), restoring);
            }

            foreach (RuntimeTraitRecord record in recordsByTraitId.Values.ToList())
            {
                if (!definitionsById.TryGetValue(record.traitDefinitionId, out TraitDefinition definition) || (TraitLifecycleState)record.lifecycleState != TraitLifecycleState.Active)
                {
                    continue;
                }

                ApplyDefinitionEffects(definition, record, restoring, passthrough);
            }

            TraitOperationResult result = passthrough ?? TraitOperationResult.Success("Trait effects rebuilt.", "Rebuilt").WithEffectsChanged(true);
            if (notify)
            {
                RaiseChanged(result, restoring);
            }

            return result;
        }

        private void ApplyDefinitionEffects(TraitDefinition definition, RuntimeTraitRecord record, bool restoring, TraitOperationResult result)
        {
            string sourceId = TraitSourceId(definition.Id);
            foreach (TraitCalculatedStatContributionDefinition authored in definition.CalculatedStatContributions)
            {
                if (authored?.CalculatedStat == null)
                {
                    continue;
                }

                RuntimeCalculatedStatContribution contribution = new RuntimeCalculatedStatContribution
                {
                    contributionId = $"{sourceId}.{authored.EntryId}".ToLowerInvariant(),
                    statId = authored.CalculatedStat.Id,
                    sourceId = sourceId,
                    sourceCategory = (int)CalculatedStatContributionSourceCategory.Trait,
                    kind = (int)authored.Kind,
                    direction = (int)authored.Direction,
                    magnitude = authored.Magnitude,
                    priority = authored.Priority
                };
                calculatedStats?.AddContribution(contribution, out _, restoring);
            }

            foreach (TraitCapabilityGrantDefinition grant in definition.BooleanCapabilityGrants.Concat(definition.NumericCapabilityGrants))
            {
                if (grant?.Capability == null || !grant.AlphaEnabled)
                {
                    continue;
                }

                capabilities?.Add(grant.Capability, CapabilitySourceCategory.Trait, sourceId, grant.EntryId,
                    grant.BooleanValue, grant.NumericValue, grant.Priority, grant.Blocker, restoring);
            }

            foreach (TraitSkillGrantDefinition grant in definition.SkillGrants)
            {
                if (grant?.Skill == null || !grant.AlphaEnabled)
                {
                    continue;
                }

                if (skills == null)
                {
                    continue;
                }

                SkillOperationResult skillResult = skills.GrantSkill(grant.Skill, grant.StartingGrade, SkillAcquisitionSource.Development, $"Trait grant {definition.Id}", sourceId, restoring);
                if (skillResult.Succeeded)
                {
                    result?.SkillGrantIds.Add(grant.Skill.Id);
                }
            }

            foreach (TraitAbilityGrantDefinition grant in definition.AbilityGrants)
            {
                if (grant?.Ability == null || !grant.AlphaEnabled || grant.RequiredLifecycle != (TraitLifecycleState)record.lifecycleState || (grant.RequireDiscovered && (TraitDiscoveryState)record.discoveryState == TraitDiscoveryState.Undiscovered))
                {
                    continue;
                }

                abilities?.Grant(grant.Ability, AbilityGrantSourceCategory.Trait, sourceId, restoring);
            }
        }

        private void ApplyLinkedGrants(TraitDefinition definition, RuntimeTraitRecord parentRecord, TraitGrantRequest parentRequest, TraitOperationResult result, HashSet<string> stack)
        {
            if (!stack.Add(definition.Id))
            {
                throw new InvalidOperationException($"Linked Trait cycle detected at '{definition.Id}'.");
            }

            foreach (TraitLinkedGrantDefinition linked in definition.LinkedTraitGrants)
            {
                if (linked?.Trait == null || !linked.AlphaEnabled)
                {
                    continue;
                }

                if (linked.ChildEffectsDependOnParentActive && (TraitLifecycleState)parentRecord.lifecycleState != TraitLifecycleState.Active)
                {
                    continue;
                }

                TraitGrantRequest childRequest = new TraitGrantRequest
                {
                    OwnerId = parentRequest.OwnerId,
                    TraitDefinitionId = linked.Trait.Id,
                    RequestedLifecycle = linked.Lifecycle,
                    RequestedDiscovery = linked.Discovery,
                    SourceCategory = TraitSourceCategory.Trait,
                    SourceId = TraitSourceId(definition.Id),
                    Reason = $"Linked grant from {definition.Id}",
                    Restoration = parentRequest.Restoration,
                    Migration = parentRequest.Migration,
                    Authority = parentRequest.Authority,
                    RevokeOnSourceRemoval = linked.RevokeOnParentRemoval
                };

                RuntimeTraitRecord existing = FindCurrentRecord(linked.Trait.Id);
                if (existing == null)
                {
                    existing = CreateRecord(linked.Trait.Id, parentRequest.OwnerId, linked.Lifecycle, linked.Discovery, childRequest.Reason);
                    recordsByTraitId.Add(linked.Trait.Id, existing);
                    result.LinkedGrantIds.Add(linked.Trait.Id);
                }

                AddSource(existing, childRequest);
                parentRecord.linkedGrantMetadata.Add($"{definition.Id}->{linked.Trait.Id}");
                ApplyLinkedGrants(linked.Trait, existing, parentRequest, result, stack);
            }

            stack.Remove(definition.Id);
        }

        private void RemoveLinkedSourcesFor(string parentTraitId)
        {
            string sourceId = TraitSourceId(parentTraitId);
            foreach (RuntimeTraitRecord record in recordsByTraitId.Values.ToList())
            {
                record.sourceRecords.RemoveAll(source => source.sourceCategory == (int)TraitSourceCategory.Trait && string.Equals(source.sourceId, sourceId, StringComparison.Ordinal) && source.revokeOnSourceRemoval);
                if (record.sourceRecords.Count == 0 && (TraitLifecycleState)record.lifecycleState != TraitLifecycleState.Historical)
                {
                    SetLifecycleInternal(record, TraitLifecycleState.Removed, "Linked source removed.");
                }
            }
        }

        private bool AddSource(RuntimeTraitRecord record, TraitGrantRequest request)
        {
            string sourceId = string.IsNullOrWhiteSpace(request.SourceId) ? request.SourceCategory.ToString().ToLowerInvariant() : request.SourceId;
            if (record.sourceRecords.Any(source => source.sourceCategory == (int)request.SourceCategory && string.Equals(source.sourceId, sourceId, StringComparison.Ordinal)))
            {
                return false;
            }

            record.sourceRecords.Add(new RuntimeTraitSourceRecord
            {
                sourceCategory = (int)request.SourceCategory,
                sourceId = sourceId,
                reason = request.Reason ?? string.Empty,
                acquiredAtUtc = DateTime.UtcNow.ToString("O"),
                acquiredAtPlaytimeSeconds = 0d,
                permanentSource = request.PermanentSource,
                revokeOnSourceRemoval = request.RevokeOnSourceRemoval,
                authority = request.Authority ?? string.Empty
            });
            return true;
        }

        private RuntimeTraitRecord CreateRecord(string traitId, string owner, TraitLifecycleState lifecycle, TraitDiscoveryState discovery, string reason)
        {
            string now = DateTime.UtcNow.ToString("O");
            return new RuntimeTraitRecord
            {
                traitDefinitionId = traitId,
                lifecycleState = (int)lifecycle,
                discoveryState = (int)discovery,
                ownerId = string.IsNullOrWhiteSpace(owner) ? ownerId : owner,
                firstAcquiredAtUtc = now,
                latestLifecycleChangedAtUtc = now,
                latestDiscoveryChangedAtUtc = now,
                primaryAcquisitionReason = reason ?? string.Empty
            };
        }

        private RuntimeTraitRecord FindCurrentRecord(string traitId)
        {
            return recordsByTraitId.TryGetValue(traitId, out RuntimeTraitRecord record) && (TraitLifecycleState)record.lifecycleState != TraitLifecycleState.Historical
                ? record
                : null;
        }

        private List<string> DetectConflicts(TraitDefinition definition)
        {
            List<string> conflicts = new List<string>();
            HashSet<string> explicitIds = new HashSet<string>(definition.IncompatibleTraits.Where(trait => trait != null).Select(trait => trait.Id), StringComparer.Ordinal);
            HashSet<string> groups = new HashSet<string>(definition.ConflictGroupIds.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
            foreach (RuntimeTraitRecord record in recordsByTraitId.Values)
            {
                TraitLifecycleState state = (TraitLifecycleState)record.lifecycleState;
                if (state == TraitLifecycleState.Removed || state == TraitLifecycleState.Historical || string.Equals(record.traitDefinitionId, definition.Id, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!definitionsById.TryGetValue(record.traitDefinitionId, out TraitDefinition activeDefinition))
                {
                    continue;
                }

                bool explicitConflict = explicitIds.Contains(activeDefinition.Id) || activeDefinition.IncompatibleTraits.Any(trait => trait != null && string.Equals(trait.Id, definition.Id, StringComparison.Ordinal));
                bool groupConflict = activeDefinition.ConflictGroupIds.Any(group => !string.IsNullOrWhiteSpace(group) && groups.Contains(group));
                if (explicitConflict || groupConflict)
                {
                    conflicts.Add(activeDefinition.Id);
                }
            }

            return conflicts;
        }

        private List<TraitDefinition> GetActiveTraitDefinitions()
        {
            return recordsByTraitId.Values
                .Where(record => (TraitLifecycleState)record.lifecycleState == TraitLifecycleState.Active)
                .Select(record => definitionsById.TryGetValue(record.traitDefinitionId, out TraitDefinition definition) ? definition : null)
                .Where(definition => definition != null)
                .ToList();
        }

        private IReadOnlyList<TraitSnapshot> BuildSnapshots(Func<RuntimeTraitRecord, bool> filter, bool revealAll)
        {
            return recordsByTraitId.Values
                .Where(record => filter == null || filter(record))
                .OrderBy(record => record.traitDefinitionId, StringComparer.Ordinal)
                .Select(record =>
                {
                    definitionsById.TryGetValue(record.traitDefinitionId, out TraitDefinition definition);
                    TraitDiscoveryState discovery = (TraitDiscoveryState)record.discoveryState;
                    bool reveal = revealAll || discovery == TraitDiscoveryState.Discovered || (definition != null && (definition.DefaultVisibility == TraitVisibility.Public || definition.DefaultVisibility == TraitVisibility.Known));
                    return new TraitSnapshot
                    {
                        Record = TraitRuntimeCloner.Clone(record),
                        Definition = definition,
                        ShowTrueName = reveal,
                        PresentationName = reveal ? definition == null ? record.traitDefinitionId : definition.DisplayName : discovery == TraitDiscoveryState.Suspected ? "Unknown Peculiarity" : "Hidden Trait"
                    };
                })
                .ToList();
        }

        private bool IsKnownForNormalUi(RuntimeTraitRecord record)
        {
            if ((TraitLifecycleState)record.lifecycleState == TraitLifecycleState.Historical || (TraitLifecycleState)record.lifecycleState == TraitLifecycleState.Removed)
            {
                return false;
            }

            TraitDiscoveryState discovery = (TraitDiscoveryState)record.discoveryState;
            if (discovery == TraitDiscoveryState.Undiscovered)
            {
                return false;
            }

            if (!definitionsById.TryGetValue(record.traitDefinitionId, out TraitDefinition definition))
            {
                return discovery != TraitDiscoveryState.Undiscovered;
            }

            return definition.DefaultVisibility != TraitVisibility.Secret || discovery == TraitDiscoveryState.Discovered;
        }

        private void SetLifecycleInternal(RuntimeTraitRecord record, TraitLifecycleState target, string reason)
        {
            TraitLifecycleState old = (TraitLifecycleState)record.lifecycleState;
            TraitDiscoveryState discovery = (TraitDiscoveryState)record.discoveryState;
            record.lifecycleState = (int)target;
            record.latestLifecycleChangedAtUtc = DateTime.UtcNow.ToString("O");
            AddTransition(record, old, target, discovery, discovery, reason);
        }

        private void HistoricalizeInternal(RuntimeTraitRecord record, string reason, bool restoring)
        {
            SetLifecycleInternal(record, TraitLifecycleState.Historical, reason);
        }

        private void AddTransition(RuntimeTraitRecord record, TraitLifecycleState oldLifecycle, TraitLifecycleState newLifecycle, TraitDiscoveryState oldDiscovery, TraitDiscoveryState newDiscovery, string reason)
        {
            record.transitionHistory.Add(new RuntimeTraitTransitionRecord
            {
                transitionId = $"trait-transition.{Guid.NewGuid():N}",
                traitDefinitionId = record.traitDefinitionId,
                oldLifecycleState = (int)oldLifecycle,
                newLifecycleState = (int)newLifecycle,
                oldDiscoveryState = (int)oldDiscovery,
                newDiscoveryState = (int)newDiscovery,
                reason = reason ?? string.Empty,
                changedAtUtc = DateTime.UtcNow.ToString("O")
            });
        }

        private Dictionary<string, RuntimeTraitRecord> CloneRecords()
        {
            return recordsByTraitId.ToDictionary(pair => pair.Key, pair => TraitRuntimeCloner.Clone(pair.Value), StringComparer.Ordinal);
        }

        private void RaiseChanged(TraitOperationResult result, bool restoring)
        {
            if (suppressEvents)
            {
                return;
            }

            TraitsChanged?.Invoke(this, result, restoring);
            if (!string.IsNullOrWhiteSpace(result.TraitId) && recordsByTraitId.TryGetValue(result.TraitId, out RuntimeTraitRecord record))
            {
                TraitRecordChanged?.Invoke(this, TraitRuntimeCloner.Clone(record), restoring);
            }
        }

        private Scope Suppress()
        {
            suppressEvents = true;
            return new Scope(this);
        }

        private static TraitDiscoveryState MoreVisible(TraitDiscoveryState oldState, TraitDiscoveryState requested)
        {
            return (TraitDiscoveryState)Math.Max((int)oldState, (int)requested);
        }

        private static bool IsValidTransition(TraitLifecycleState oldState, TraitLifecycleState newState)
        {
            if (oldState == newState)
            {
                return true;
            }

            if (oldState == TraitLifecycleState.Historical)
            {
                return false;
            }

            if (newState == TraitLifecycleState.Historical || newState == TraitLifecycleState.Removed)
            {
                return true;
            }

            return oldState == TraitLifecycleState.Dormant && newState == TraitLifecycleState.Active
                || oldState == TraitLifecycleState.Active && newState == TraitLifecycleState.Suppressed
                || oldState == TraitLifecycleState.Suppressed && newState == TraitLifecycleState.Active;
        }

        private static string TraitSourceId(string traitId)
        {
            return $"trait-source.{traitId}";
        }

        private static bool IsDeterministicBodySource(RuntimeTraitSourceRecord source)
        {
            return source != null
                && (source.sourceCategory == (int)TraitSourceCategory.Species
                    || source.sourceCategory == (int)TraitSourceCategory.BiologicalClassification);
        }

        private readonly struct Scope : IDisposable
        {
            private readonly CharacterTraitCollection owner;

            public Scope(CharacterTraitCollection owner)
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
