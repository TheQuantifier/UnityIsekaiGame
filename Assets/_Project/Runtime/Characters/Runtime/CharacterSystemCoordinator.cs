using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.Beings.Biology;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Equipment;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.GameData.Persistence;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.ResourceSystem;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;
using UnityIsekaiGame.WorldEntities;

namespace UnityIsekaiGame.CharacterSystem
{
    public sealed class CharacterSystemCoordinator : MonoBehaviour
    {
        [SerializeField] private PlayerIdentityProgression identity;
        [SerializeField] private ActorStats actorStats;
        [SerializeField] private CharacterAttributes attributes;
        [SerializeField] private CalculatedStatCollection calculatedStats;
        [SerializeField] private CharacterResourceCollection resources;
        [SerializeField] private CharacterSkillCollection skills;
        [SerializeField] private CharacterTraitCollection traits;
        [SerializeField] private ActorBodyRuntime body;
        [SerializeField] private StatusEffectController statuses;
        [SerializeField] private PlayerInventory inventory;
        [SerializeField] private PlayerEquipment equipment;
        [SerializeField] private WorldEntityIdentity worldEntityIdentity;
        [SerializeField] private string actorIdOverride;

        private DefinitionRegistry registry;
        private CharacterFullSnapshot cachedPlayerSnapshot;
        private CharacterFullSnapshot cachedDevelopmentSnapshot;
        private CharacterQueryService query;
        private string runtimeActorFallbackId;
        private bool subscribed;
        private bool snapshotDirty = true;

        public event Action<CharacterSystemCoordinator, bool> CharacterInitialized;
        public event Action<CharacterSystemCoordinator, bool> CharacterReady;
        public event Action<CharacterSystemCoordinator, bool> CharacterDisposed;
        public event Action<CharacterSystemCoordinator, long, bool, string> CharacterRevisionChanged;
        public event Action<CharacterSystemCoordinator, CharacterFullSnapshot, bool> CharacterSnapshotChanged;

        public CharacterReadinessState Readiness { get; private set; } = CharacterReadinessState.Uninitialized;
        public long Revision { get; private set; }
        public string LastFailureReason { get; private set; }
        public bool IsReady => Readiness == CharacterReadinessState.Ready;
        public CharacterQueryService Query => query ??= new CharacterQueryService(this);

        public PlayerIdentityProgression Identity => identity;
        public ActorStats ActorStats => actorStats;
        public CharacterAttributes Attributes => attributes;
        public CalculatedStatCollection CalculatedStats => calculatedStats;
        public CharacterResourceCollection Resources => resources;
        public CharacterSkillCollection Skills => skills;
        public CharacterTraitCollection Traits => traits;
        public ActorBodyRuntime Body => body;
        public StatusEffectController Statuses => statuses;
        public PlayerInventory Inventory => inventory;
        public PlayerEquipment Equipment => equipment;
        public string AccountId => identity == null ? string.Empty : identity.AccountId;
        public string PlayerId => identity == null ? PersistenceService.LocalPlayerId : identity.PlayerId;
        public string PersonId => identity == null ? string.Empty : identity.PersonId;
        public string ActorId => ResolveActorId();

        private void Awake()
        {
            ResolveSubsystems(addMissingCore: false);
        }

        private void OnEnable()
        {
            ResolveSubsystems(addMissingCore: false);
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            SetReadiness(CharacterReadinessState.Disposed, false);
            CharacterDisposed?.Invoke(this, false);
        }

        public bool InitializeFromRegistry(DefinitionRegistry definitionRegistry, bool restoring = false, bool addMissingCore = true)
        {
            registry = definitionRegistry ?? registry;
            LastFailureReason = string.Empty;
            try
            {
                SetReadiness(restoring ? CharacterReadinessState.Restoring : CharacterReadinessState.DefinitionsReady, restoring);
                Unsubscribe();
                ResolveSubsystems(addMissingCore);
                if (registry == null)
                {
                    Fail("Definition registry is missing.");
                    return false;
                }

                attributes?.Configure(registry);
                calculatedStats?.Configure(registry, attributes);
                if (actorStats != null)
                {
                    actorStats.ConfigureDerivedStats(registry);
                    attributes = actorStats.CharacterAttributes ?? attributes;
                    calculatedStats = actorStats.CalculatedStats ?? calculatedStats;
                }
                skills?.Configure(registry, calculatedStats, null);
                traits?.Configure(registry, calculatedStats, skills, PlayerId);
                body?.Configure(registry, ActorId, PersonId, traits, calculatedStats, restoring);
                resources?.Configure(registry, calculatedStats, PlayerId);
                identity?.ConfigureRuntimeReferences(actorStats, worldEntityIdentity, null, null);
                SetReadiness(CharacterReadinessState.IdentityReady, restoring);

                Subscribe();
                CharacterInitialized?.Invoke(this, restoring);
                FullRebuild(restoring, "Initialize");
                return IsReady;
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                return false;
            }
        }

        public bool FullRebuild(bool restoring = false, string reason = "FullRebuild")
        {
            if (Readiness == CharacterReadinessState.Failed)
            {
                return false;
            }

            try
            {
                SetReadiness(CharacterReadinessState.Rebuilding, restoring);
                skills?.RebuildSkillEffects(restoring);
                traits?.RebuildTraitEffects(restoring);
                if (body != null && !body.ValidateBody(out string bodyFailure))
                {
                    throw new InvalidOperationException(bodyFailure);
                }

                calculatedStats?.ForceRecalculateAll(restoring);
                if (resources != null)
                {
                    foreach (ResourceSnapshot snapshot in resources.GetSnapshots())
                    {
                        resources.ReconcileResource(snapshot.ResourceId, restoring);
                    }
                }

                SetReadiness(CharacterReadinessState.Ready, restoring);
                IncrementRevision(reason, restoring);
                CharacterReady?.Invoke(this, restoring);
                return true;
            }
            catch (Exception exception)
            {
                Fail(exception.Message);
                return false;
            }
        }

        public CharacterFullSnapshot GetSnapshot(bool developmentView = false)
        {
            if (!snapshotDirty)
            {
                return developmentView ? cachedDevelopmentSnapshot : cachedPlayerSnapshot;
            }

            cachedPlayerSnapshot = BuildSnapshot(false);
            cachedDevelopmentSnapshot = BuildSnapshot(true);
            snapshotDirty = false;
            return developmentView ? cachedDevelopmentSnapshot : cachedPlayerSnapshot;
        }

        public RequirementEvaluationContext CreateRequirementContext()
        {
            RequirementEvaluationContext context = new RequirementEvaluationContext
            {
                Attributes = attributes,
                CalculatedStats = calculatedStats,
                Resources = resources,
                Skills = skills,
                Traits = traits,
                Identity = identity,
                Equipment = equipment,
                Statuses = statuses,
                Inventory = inventory
            };

            if (skills != null)
            {
                foreach (RuntimeSkillRecord skill in skills.LearnedSkills)
                {
                    foreach (string abilityId in skill.unlockedAbilityOrActionIds ?? new List<string>())
                    {
                        context.OwnedAbilityOrActionIds.Add(abilityId);
                    }
                }
            }

            if (traits != null)
            {
                foreach (TraitSnapshot trait in traits.GetActiveTraits())
                {
                    if (trait?.Definition == null)
                    {
                        continue;
                    }

                    foreach (TraitAbilityGrantDefinition grant in trait.Definition.AbilityActionGrants)
                    {
                        if (grant != null && !string.IsNullOrWhiteSpace(grant.AbilityOrActionId))
                        {
                            context.OwnedAbilityOrActionIds.Add(grant.AbilityOrActionId);
                        }
                    }
                }
            }

            return context;
        }

        public CharacterIntegrityReport ValidateIntegrity()
        {
            CharacterIntegrityReport report = new CharacterIntegrityReport();
            CharacterSystemCoordinator[] coordinators = GetComponents<CharacterSystemCoordinator>();
            if (coordinators.Length > 1)
            {
                report.AddError($"Duplicate CharacterSystemCoordinator components found on '{name}'.");
            }

            if (identity != null && !identity.ValidateIdentity(out string identityFailure))
            {
                report.AddError(identityFailure);
            }

            if (!string.IsNullOrWhiteSpace(PersonId) && !string.IsNullOrWhiteSpace(ActorId) && string.Equals(PersonId, ActorId, StringComparison.Ordinal))
            {
                report.AddError("Person ID and Actor/body ID must remain distinct.");
            }

            ValidateConfigured(report, attributes, attributes == null || attributes.IsConfigured, "Base Attributes");
            ValidateConfigured(report, calculatedStats, calculatedStats == null || calculatedStats.IsConfigured, "Calculated Stats");
            ValidateConfigured(report, resources, resources == null || resources.IsConfigured, "Current Resources");
            ValidateConfigured(report, skills, skills == null || skills.IsConfigured, "Skills");
            ValidateConfigured(report, traits, traits == null || traits.IsConfigured, "Traits");
            ValidateConfigured(report, body, body == null || body.IsReady, "Body");

            ValidateDuplicateIds(report, skills == null ? Array.Empty<string>() : skills.LearnedSkills.Select(skill => skill.skillDefinitionId), "Skill record");
            ValidateDuplicateIds(report, traits == null ? Array.Empty<string>() : traits.TraitRecords.Select(trait => trait.traitDefinitionId), "Trait record");
            ValidateDuplicateIds(report, resources == null ? Array.Empty<string>() : resources.ResourceRecords.Select(resource => resource.resourceDefinitionId), "Resource record");

            report.AddInfo($"Readiness={Readiness}, Revision={Revision}, Person={PersonId}, Actor={ActorId}.");
            if (body != null)
            {
                BodySnapshot bodySnapshot = body.CreateSnapshot();
                report.AddInfo($"Body={bodySnapshot.SpeciesId}, Classification={bodySnapshot.BiologicalClassificationId}, Form={bodySnapshot.BodyFormId}, BodyRevision={bodySnapshot.BodyRevision}.");
            }

            return report;
        }

        public string BuildDiagnosticSummary(bool developmentView)
        {
            CharacterFullSnapshot snapshot = GetSnapshot(developmentView);
            CharacterIntegrityReport integrity = ValidateIntegrity();
            List<string> lines = new List<string>
            {
                "Feature 5.6 Character System",
                $"Readiness: {Readiness}",
                $"Revision: {Revision}",
                $"Account: {snapshot.Identity.AccountId}",
                $"Player: {snapshot.Identity.PlayerId}",
                $"Person: {snapshot.Identity.PersonId}",
                $"Actor: {snapshot.Identity.ActorId}",
                $"Origin: {snapshot.Identity.OriginId}",
                $"Birth Gift: {snapshot.Identity.BirthGiftId}",
                $"Base Attributes: {snapshot.Numerical.BaseAttributes.Count}",
                $"Calculated Stats: {snapshot.Numerical.CalculatedStats.Count}",
                $"Resources: {snapshot.Numerical.Resources.Count}",
                $"Skills: {snapshot.Progression.LearnedSkills.Count}",
                $"Traits: {snapshot.Progression.Traits.Count}",
                body == null ? "Body: None" : $"Body: {body.CreateSnapshot().SpeciesDisplayName} ({body.CreateSnapshot().SpeciesId})",
                $"Capabilities: {snapshot.Capabilities.Capabilities.Count}",
                integrity.GetSummary()
            };
            return string.Join(Environment.NewLine, lines);
        }

        private void ResolveSubsystems(bool addMissingCore)
        {
            actorStats = actorStats == null ? GetComponent<ActorStats>() : actorStats;
            identity = identity == null ? GetComponent<PlayerIdentityProgression>() : identity;
            attributes = attributes == null ? GetComponent<CharacterAttributes>() : attributes;
            calculatedStats = calculatedStats == null ? GetComponent<CalculatedStatCollection>() : calculatedStats;
            resources = resources == null ? GetComponent<CharacterResourceCollection>() : resources;
            skills = skills == null ? GetComponent<CharacterSkillCollection>() : skills;
            traits = traits == null ? GetComponent<CharacterTraitCollection>() : traits;
            body = body == null ? GetComponent<ActorBodyRuntime>() : body;
            statuses = statuses == null ? GetComponent<StatusEffectController>() : statuses;
            inventory = inventory == null ? GetComponent<PlayerInventory>() : inventory;
            equipment = equipment == null ? GetComponent<PlayerEquipment>() : equipment;
            worldEntityIdentity = worldEntityIdentity == null ? GetComponent<WorldEntityIdentity>() : worldEntityIdentity;

            if (!addMissingCore)
            {
                return;
            }

            attributes = attributes == null ? gameObject.AddComponent<CharacterAttributes>() : attributes;
            calculatedStats = calculatedStats == null ? gameObject.AddComponent<CalculatedStatCollection>() : calculatedStats;
            resources = resources == null ? gameObject.AddComponent<CharacterResourceCollection>() : resources;
            skills = skills == null ? gameObject.AddComponent<CharacterSkillCollection>() : skills;
            traits = traits == null ? gameObject.AddComponent<CharacterTraitCollection>() : traits;
        }

        private CharacterFullSnapshot BuildSnapshot(bool developmentView)
        {
            CharacterIdentitySnapshot identitySnapshot = new CharacterIdentitySnapshot(
                AccountId,
                PlayerId,
                PersonId,
                ActorId,
                string.IsNullOrWhiteSpace(PersonId) ? name : PersonId,
                identity?.Origin == null ? string.Empty : identity.Origin.originFamilyId,
                identity?.Origin == null ? string.Empty : identity.Origin.originId,
                identity?.BirthGift == null ? string.Empty : identity.BirthGift.giftDefinitionId,
                Readiness,
                Revision);

            CharacterProgressionSnapshot progression = new CharacterProgressionSnapshot(
                identity == null ? default : identity.CalculateOverallLevel(),
                skills == null ? Array.Empty<RuntimeSkillRecord>() : skills.LearnedSkills,
                traits == null ? Array.Empty<TraitSnapshot>() : developmentView ? traits.GetDevelopmentSnapshot() : traits.GetKnownTraits(),
                identity == null ? Array.Empty<RuntimeRoleRecord>() : identity.Roles,
                identity == null ? Array.Empty<RuntimeSocialStatusRecord>() : identity.SocialStatuses,
                identity == null ? Array.Empty<RuntimeTitleRecord>() : identity.Titles);

            Dictionary<string, float> calculated = new Dictionary<string, float>(StringComparer.Ordinal);
            if (calculatedStats != null && calculatedStats.IsConfigured)
            {
                foreach (CalculatedStatDefinition definition in calculatedStats.GetOrderedDefinitions(characterMenuOnly: false))
                {
                    calculated[definition.Id] = calculatedStats.GetValue(definition.Id);
                }
            }

            CharacterNumericalSnapshot numerical = new CharacterNumericalSnapshot(
                attributes == null ? Array.Empty<RuntimeAttributeValueRecord>() : attributes.GetOrderedValues(),
                calculated,
                resources == null ? Array.Empty<ResourceSnapshot>() : resources.GetSnapshots());

            CharacterSocialSnapshot social = new CharacterSocialSnapshot(
                identity == null ? Array.Empty<RuntimeRoleRecord>() : identity.Roles,
                identity == null ? Array.Empty<RuntimeSocialStatusRecord>() : identity.SocialStatuses,
                identity == null ? Array.Empty<RuntimeTitleRecord>() : identity.Titles,
                identity == null ? Array.Empty<WalletBalanceRecord>() : identity.WalletBalances);

            CharacterCapabilitySnapshot capabilities = BuildCapabilitySnapshot();

            return new CharacterFullSnapshot(
                CharacterFullSnapshot.CurrentSchemaVersion,
                Revision,
                identitySnapshot,
                progression,
                numerical,
                social,
                capabilities,
                developmentView,
                LastFailureReason);
        }

        private CharacterCapabilitySnapshot BuildCapabilitySnapshot()
        {
            Dictionary<string, float> resistances = new Dictionary<string, float>(StringComparer.Ordinal);
            HashSet<string> immunities = new HashSet<string>(StringComparer.Ordinal);
            if (traits != null)
            {
                foreach (TraitSnapshot snapshot in traits.GetActiveTraits())
                {
                    if (snapshot?.Definition == null)
                    {
                        continue;
                    }

                    foreach (TraitResistanceGrantDefinition grant in snapshot.Definition.ResistanceGrants)
                    {
                        if (grant?.DamageType == null || !grant.AlphaEnabled)
                        {
                            continue;
                        }

                        resistances[grant.DamageType.Id] = Mathf.Clamp01((resistances.TryGetValue(grant.DamageType.Id, out float existing) ? existing : 0f) + grant.ResistanceFraction);
                    }

                    foreach (TraitResistanceGrantDefinition grant in snapshot.Definition.ImmunityGrants)
                    {
                        if (grant?.DamageType != null && grant.AlphaEnabled)
                        {
                            immunities.Add(grant.DamageType.Id);
                        }
                    }
                }
            }

            return new CharacterCapabilitySnapshot(
                traits == null ? Array.Empty<Capabilities.CapabilitySnapshot>() : traits.Capabilities.GetSnapshots(),
                resistances,
                immunities.ToList());
        }

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            if (identity != null)
            {
                identity.ProgressionChanged += OnProgressionChanged;
            }

            if (actorStats != null)
            {
                actorStats.StatsChanged += OnActorStatsChanged;
            }

            if (attributes != null)
            {
                attributes.AttributesChanged += OnAttributesChanged;
            }

            if (calculatedStats != null)
            {
                calculatedStats.CalculatedStatsChanged += OnCalculatedStatsChanged;
            }

            if (resources != null)
            {
                resources.ResourceChanged += OnResourceChanged;
                resources.ResourceMaximumChanged += OnResourceMaximumChanged;
                resources.ResourcesRestored += OnResourcesRestored;
            }

            if (skills != null)
            {
                skills.SkillsChanged += OnSkillsChanged;
                skills.SkillLearned += OnSkillRecordChanged;
                skills.SkillPromoted += OnSkillPromoted;
                skills.SkillMastered += OnSkillRecordChanged;
            }

            if (traits != null)
            {
                traits.TraitsChanged += OnTraitsChanged;
                traits.TraitRecordChanged += OnTraitRecordChanged;
            }

            if (body != null)
            {
                body.BodyChanged += OnBodyChanged;
            }

            if (statuses != null)
            {
                statuses.StatusAdded += OnStatusChanged;
                statuses.StatusChanged += OnStatusChanged;
                statuses.StatusRemoved += OnStatusChanged;
                statuses.StatusExpired += OnStatusChanged;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged += OnEquipmentChanged;
            }

            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            if (identity != null)
            {
                identity.ProgressionChanged -= OnProgressionChanged;
            }

            if (actorStats != null)
            {
                actorStats.StatsChanged -= OnActorStatsChanged;
            }

            if (attributes != null)
            {
                attributes.AttributesChanged -= OnAttributesChanged;
            }

            if (calculatedStats != null)
            {
                calculatedStats.CalculatedStatsChanged -= OnCalculatedStatsChanged;
            }

            if (resources != null)
            {
                resources.ResourceChanged -= OnResourceChanged;
                resources.ResourceMaximumChanged -= OnResourceMaximumChanged;
                resources.ResourcesRestored -= OnResourcesRestored;
            }

            if (skills != null)
            {
                skills.SkillsChanged -= OnSkillsChanged;
                skills.SkillLearned -= OnSkillRecordChanged;
                skills.SkillPromoted -= OnSkillPromoted;
                skills.SkillMastered -= OnSkillRecordChanged;
            }

            if (traits != null)
            {
                traits.TraitsChanged -= OnTraitsChanged;
                traits.TraitRecordChanged -= OnTraitRecordChanged;
            }

            if (body != null)
            {
                body.BodyChanged -= OnBodyChanged;
            }

            if (statuses != null)
            {
                statuses.StatusAdded -= OnStatusChanged;
                statuses.StatusChanged -= OnStatusChanged;
                statuses.StatusRemoved -= OnStatusChanged;
                statuses.StatusExpired -= OnStatusChanged;
            }

            if (equipment != null)
            {
                equipment.EquipmentChanged -= OnEquipmentChanged;
            }

            subscribed = false;
        }

        private void IncrementRevision(string reason, bool restoring)
        {
            Revision++;
            snapshotDirty = true;
            CharacterRevisionChanged?.Invoke(this, Revision, restoring, reason ?? string.Empty);
            CharacterSnapshotChanged?.Invoke(this, GetSnapshot(developmentView: false), restoring);
        }

        private void SetReadiness(CharacterReadinessState state, bool restoring)
        {
            Readiness = state;
            snapshotDirty = true;
        }

        private void Fail(string reason)
        {
            LastFailureReason = string.IsNullOrWhiteSpace(reason) ? "Character System failed." : reason;
            SetReadiness(CharacterReadinessState.Failed, false);
            Debug.LogWarning($"Character System failed for '{name}': {LastFailureReason}");
        }

        private string ResolveActorId()
        {
            if (!string.IsNullOrWhiteSpace(actorIdOverride))
            {
                return actorIdOverride;
            }

            if (worldEntityIdentity != null && !string.IsNullOrWhiteSpace(worldEntityIdentity.EntityId))
            {
                return worldEntityIdentity.EntityId;
            }

            if (string.IsNullOrWhiteSpace(runtimeActorFallbackId))
            {
                runtimeActorFallbackId = $"actor.runtime.{Guid.NewGuid():N}";
            }

            return runtimeActorFallbackId;
        }

        private static void ValidateConfigured(CharacterIntegrityReport report, UnityEngine.Object component, bool configured, string label)
        {
            if (component == null)
            {
                report.AddWarning($"{label} component is missing. This is allowed for reduced NPCs but not for the full prototype player.");
                return;
            }

            if (!configured)
            {
                report.AddError($"{label} component is present but not configured.");
            }
        }

        private static void ValidateDuplicateIds(CharacterIntegrityReport report, IEnumerable<string> ids, string label)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.AddError($"{label} has a missing ID.");
                    continue;
                }

                if (!seen.Add(id))
                {
                    report.AddError($"Duplicate {label} '{id}'.");
                }
            }
        }

        private void OnProgressionChanged(PlayerIdentityProgression source, bool restoring) => IncrementRevision("IdentityProgression", restoring);
        private void OnActorStatsChanged() => IncrementRevision("ActorStats", false);
        private void OnAttributesChanged(CharacterAttributes source, IReadOnlyList<string> ids, bool restoring) => IncrementRevision("BaseAttributes", restoring);
        private void OnCalculatedStatsChanged(CalculatedStatCollection source, IReadOnlyList<string> ids, bool restoring) => IncrementRevision("CalculatedStats", restoring);
        private void OnResourceChanged(CharacterResourceCollection source, ResourceChangeResult result) => IncrementRevision($"Resource:{result?.Request.ResourceId}", result?.Request.Restoration ?? false);
        private void OnResourceMaximumChanged(CharacterResourceCollection source, ResourceSnapshot snapshot, float oldMaximum, bool restoring) => IncrementRevision($"ResourceMaximum:{snapshot.ResourceId}", restoring);
        private void OnResourcesRestored(CharacterResourceCollection source, bool restoring) => IncrementRevision("ResourcesRestored", restoring);
        private void OnSkillsChanged(CharacterSkillCollection source, bool restoring) => IncrementRevision("Skills", restoring);
        private void OnSkillRecordChanged(CharacterSkillCollection source, RuntimeSkillRecord record, bool restoring) => IncrementRevision($"Skill:{record?.skillDefinitionId}", restoring);
        private void OnSkillPromoted(CharacterSkillCollection source, SkillChangedEventArgs args) => IncrementRevision($"SkillPromotion:{args?.Skill?.skillDefinitionId}", args?.Restoring ?? false);
        private void OnTraitsChanged(CharacterTraitCollection source, TraitOperationResult result, bool restoring) => IncrementRevision($"Trait:{result?.TraitId}", restoring);
        private void OnTraitRecordChanged(CharacterTraitCollection source, RuntimeTraitRecord record, bool restoring) => IncrementRevision($"TraitRecord:{record?.traitDefinitionId}", restoring);
        private void OnBodyChanged(ActorBodyRuntime source, BodyOperationResult result, bool restoring) => IncrementRevision($"Body:{result?.Snapshot?.SpeciesId}", restoring);
        private void OnStatusChanged(RuntimeStatusEffect status) => IncrementRevision($"Status:{status?.Definition?.Id}", false);
        private void OnEquipmentChanged() => IncrementRevision("Equipment", false);
    }
}
