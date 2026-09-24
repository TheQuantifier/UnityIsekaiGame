using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Abilities;
using UnityIsekaiGame.Magic;
using UnityIsekaiGame.Stats;

namespace UnityIsekaiGame.Skills
{
    public sealed class CharacterSkillCollection : MonoBehaviour
    {
        [SerializeField] private CalculatedStatCollection calculatedStats;
        [SerializeField] private PlayerSpellLoadout spellLoadout;
        [SerializeField] private CharacterAbilityCollection abilities;
        [SerializeField] private List<SkillDefinition> fallbackDefinitions = new List<SkillDefinition>();

        private readonly Dictionary<string, SkillDefinition> definitionsById = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);
        private readonly List<SkillLearningProgressRecord> hiddenProgress = new List<SkillLearningProgressRecord>();
        private readonly List<RuntimeSkillRecord> learnedSkills = new List<RuntimeSkillRecord>();
        private readonly HashSet<string> processedActionSkillKeys = new HashSet<string>(StringComparer.Ordinal);
        private DefinitionRegistry registry;
        private bool notificationsSuppressed;

        public event Action<CharacterSkillCollection, bool> SkillsChanged;
        public event Action<CharacterSkillCollection, SkillLearningProgressRecord, bool> HiddenProgressChanged;
        public event Action<CharacterSkillCollection, RuntimeSkillRecord, bool> SkillLearned;
        public event Action<CharacterSkillCollection, SkillChangedEventArgs> SkillPromoted;
        public event Action<CharacterSkillCollection, RuntimeSkillRecord, bool> SkillMastered;

        public IReadOnlyList<RuntimeSkillRecord> LearnedSkills => learnedSkills.Select(CloneSkill).ToList();
        public IReadOnlyList<SkillLearningProgressRecord> DevelopmentLearningProgress => hiddenProgress.Select(CloneProgress).ToList();
        public bool IsConfigured { get; private set; }

        private void Awake()
        {
            if (calculatedStats == null)
            {
                calculatedStats = GetComponent<CalculatedStatCollection>();
            }

            if (spellLoadout == null)
            {
                spellLoadout = GetComponent<PlayerSpellLoadout>();
            }

            abilities = abilities == null ? GetComponent<CharacterAbilityCollection>() : abilities;

            if (!IsConfigured && fallbackDefinitions.Count > 0)
            {
                Configure(fallbackDefinitions, null, calculatedStats, spellLoadout);
            }
        }

        public void Configure(DefinitionRegistry definitionRegistry, CalculatedStatCollection statCollection = null, PlayerSpellLoadout loadout = null)
        {
            registry = definitionRegistry;
            Configure(
                definitionRegistry == null ? Enumerable.Empty<SkillDefinition>() : definitionRegistry.DefinitionsById.Values.OfType<SkillDefinition>(),
                definitionRegistry,
                statCollection,
                loadout);
        }

        public void Configure(IEnumerable<SkillDefinition> definitions, DefinitionRegistry definitionRegistry, CalculatedStatCollection statCollection = null, PlayerSpellLoadout loadout = null)
        {
            registry = definitionRegistry ?? registry;
            calculatedStats = statCollection == null ? calculatedStats == null ? GetComponent<CalculatedStatCollection>() : calculatedStats : statCollection;
            spellLoadout = loadout == null ? spellLoadout == null ? GetComponent<PlayerSpellLoadout>() : spellLoadout : loadout;
            abilities = abilities == null ? GetComponent<CharacterAbilityCollection>() : abilities;
            definitionsById.Clear();

            foreach (SkillDefinition definition in definitions ?? Enumerable.Empty<SkillDefinition>())
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id) || definitionsById.ContainsKey(definition.Id))
                {
                    continue;
                }

                definitionsById.Add(definition.Id, definition);
            }

            IsConfigured = definitionsById.Count > 0;
            RebuildSkillEffects(restoring: false, notify: false);
        }

        public bool TryGetSkill(string skillId, out RuntimeSkillRecord skill)
        {
            skill = learnedSkills.FirstOrDefault(record => string.Equals(record.skillDefinitionId, skillId, StringComparison.Ordinal));
            skill = CloneSkill(skill);
            return skill != null;
        }

        public SkillGrade GetGrade(string skillId)
        {
            RuntimeSkillRecord skill = learnedSkills.FirstOrDefault(record => string.Equals(record.skillDefinitionId, skillId, StringComparison.Ordinal));
            return skill == null ? SkillGrade.F : SkillGradeUtility.Clamp((SkillGrade)skill.currentGrade);
        }

        public SkillLearningProgressRecord GetLearningProgressForDevelopment(string skillId)
        {
            return CloneProgress(hiddenProgress.FirstOrDefault(record => string.Equals(record.skillDefinitionId, skillId, StringComparison.Ordinal)));
        }

        public SkillOperationResult RecordQualifyingAction(SkillActionExecutionEvent actionEvent)
        {
            EnsureConfiguredFromFallback();
            if (actionEvent == null || actionEvent.Restoring)
            {
                return SkillOperationResult.Failure("InvalidActionEvent", "Skill action event is missing or is a restoration event.");
            }

            if (!actionEvent.Executed)
            {
                return SkillOperationResult.Success("Blocked action did not count.", "BlockedAction");
            }

            if (string.IsNullOrWhiteSpace(actionEvent.EventId))
            {
                actionEvent.EventId = CreateRuntimeId("skill-action");
            }

            List<SkillDefinition> matchingDefinitions = definitionsById.Values
                .Where(skill => skill != null && skill.AlphaEnabled)
                .ToList();

            int changed = 0;
            foreach (SkillDefinition definition in matchingDefinitions)
            {
                if (!Matches(definition, actionEvent))
                {
                    continue;
                }

                string processKey = ProcessKey(actionEvent.EventId, definition.Id);
                if (!processedActionSkillKeys.Add(processKey))
                {
                    continue;
                }

                RuntimeSkillRecord learned = FindLiveSkill(definition.Id);
                if (learned == null)
                {
                    SkillOperationResult progress = AddHiddenProgress(definition, actionEvent);
                    if (progress.Succeeded)
                    {
                        changed++;
                    }
                }
                else
                {
                    SkillOperationResult xp = AwardSkillUseInternal(definition, learned, actionEvent.EventId, 1, "action-use", actionEvent.SourceSystem, false);
                    if (xp.Succeeded)
                    {
                        changed++;
                    }
                }
            }

            if (changed > 0)
            {
                RaiseSkillsChanged(false);
            }

            return SkillOperationResult.Success(changed == 0 ? "No Skill matched the action." : $"Skill action processed for {changed} Skill(s).");
        }

        public SkillOperationResult GrantSkill(SkillDefinition definition, SkillGrade startingGrade, SkillAcquisitionSource source, string reason, string sourceDefinitionId = "", bool restoring = false)
        {
            EnsureConfiguredFromFallback();
            if (definition == null)
            {
                return SkillOperationResult.Failure("MissingSkill", "Skill definition is missing.");
            }

            if (!definitionsById.ContainsKey(definition.Id))
            {
                definitionsById.Add(definition.Id, definition);
            }

            RuntimeSkillRecord existing = FindLiveSkill(definition.Id);
            SkillGrade requested = SkillGradeUtility.Clamp(startingGrade);
            if (existing == null)
            {
                RuntimeSkillRecord record = CreateLearnedSkillRecord(definition, requested, source, reason, restoring);
                learnedSkills.Add(record);
                hiddenProgress.RemoveAll(progress => string.Equals(progress.skillDefinitionId, definition.Id, StringComparison.Ordinal));
                ApplyReachedGradePackages(definition, record, SkillGrade.F, requested, includeLowerGrades: true, restoring);
                RaiseSkillLearned(record, restoring);
                RaiseSkillsChanged(restoring);
                return SkillOperationResult.Success($"Learned {definition.DisplayName} at {requested}.");
            }

            SkillGrade current = SkillGradeUtility.Clamp((SkillGrade)existing.currentGrade);
            if (requested <= current)
            {
                return SkillOperationResult.Success($"{definition.DisplayName} is already {current} or higher.", "AlreadyKnown");
            }

            PromoteTo(definition, existing, requested, source.ToString(), reason, restoring);
            RaiseSkillsChanged(restoring);
            return SkillOperationResult.Success($"Promoted {definition.DisplayName} to {requested}.");
        }

        public SkillOperationResult AwardSkillUse(string skillId, string eventId = "", int amount = 1)
        {
            EnsureConfiguredFromFallback();
            if (string.IsNullOrWhiteSpace(skillId) || !definitionsById.TryGetValue(skillId, out SkillDefinition definition))
            {
                return SkillOperationResult.Failure("UnknownSkill", $"Skill '{skillId}' is not configured.");
            }

            RuntimeSkillRecord learned = FindLiveSkill(skillId);
            if (learned == null)
            {
                return SkillOperationResult.Failure("SkillNotLearned", $"Skill '{definition.DisplayName}' is not learned.");
            }

            return AwardSkillUseInternal(definition, learned, string.IsNullOrWhiteSpace(eventId) ? CreateRuntimeId("skill-use") : eventId, Mathf.Max(1, amount), "development", "test-lab", false);
        }

        public SkillOperationResult RebuildSkillEffects(bool restoring = false)
        {
            return RebuildSkillEffects(restoring, notify: true);
        }

        private SkillOperationResult RebuildSkillEffects(bool restoring, bool notify)
        {
            EnsureConfiguredFromFallback();
            foreach (RuntimeSkillRecord record in learnedSkills)
            {
                for (int gradeIndex = SkillGradeUtility.MinimumIndex; gradeIndex <= SkillGradeUtility.MaximumIndex; gradeIndex++)
                {
                    string sourceId = GradeSourceId(record.skillDefinitionId, (SkillGrade)gradeIndex);
                    calculatedStats?.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Skill, sourceId, restoring);
                    abilities?.RemoveSource(AbilityGrantSourceCategory.Skill, sourceId, restoring);
                }
            }

            using (SuppressNotifications())
            {
                foreach (RuntimeSkillRecord record in learnedSkills)
                {
                    record.appliedGradeSourceIds.Clear();
                    record.unlockedAbilityIds.Clear();
                    record.unlockedCapabilityIds.Clear();
                    if (definitionsById.TryGetValue(record.skillDefinitionId, out SkillDefinition definition))
                    {
                        ApplyReachedGradePackages(definition, record, SkillGrade.F, SkillGradeUtility.Clamp((SkillGrade)record.currentGrade), includeLowerGrades: true, restoring);
                    }
                }
            }

            if (notify)
            {
                RaiseSkillsChanged(restoring);
            }

            return SkillOperationResult.Success("Skill effects rebuilt.");
        }

        public SkillOperationResult ClearDevelopmentState(bool confirmed)
        {
            if (!confirmed)
            {
                return SkillOperationResult.Failure("ConfirmationRequired", "Repeat the destructive action to clear Skill development state.");
            }

            foreach (RuntimeSkillRecord record in learnedSkills)
            {
                for (int gradeIndex = SkillGradeUtility.MinimumIndex; gradeIndex <= SkillGradeUtility.MaximumIndex; gradeIndex++)
                {
                    string sourceId = GradeSourceId(record.skillDefinitionId, (SkillGrade)gradeIndex);
                    calculatedStats?.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Skill, sourceId);
                    abilities?.RemoveSource(AbilityGrantSourceCategory.Skill, sourceId);
                }
            }

            learnedSkills.Clear();
            hiddenProgress.Clear();
            processedActionSkillKeys.Clear();
            RaiseSkillsChanged(false);
            return SkillOperationResult.Success("Skill development state cleared.");
        }

        public PlayerSkillsSaveData CreateSaveData(string playerId, string personId)
        {
            return new PlayerSkillsSaveData
            {
                schemaVersion = PlayerSkillsSaveData.CurrentSchemaVersion,
                playerId = playerId ?? string.Empty,
                personId = personId ?? string.Empty,
                hiddenLearningProgress = hiddenProgress.Select(CloneProgress).ToList(),
                learnedSkills = learnedSkills.Select(CloneSkill).ToList(),
                consumedActionEventIds = processedActionSkillKeys.ToList()
            };
        }

        public bool RestoreFromSaveData(PlayerSkillsSaveData saveData, DefinitionRegistry definitionRegistry, out string failureReason, bool restoring)
        {
            failureReason = string.Empty;
            Configure(definitionRegistry, calculatedStats, spellLoadout);
            if (!ValidateSaveData(saveData, definitionRegistry, out failureReason))
            {
                return false;
            }

            if (calculatedStats != null)
            {
                foreach (RuntimeSkillRecord record in learnedSkills)
                {
                    for (int gradeIndex = SkillGradeUtility.MinimumIndex; gradeIndex <= SkillGradeUtility.MaximumIndex; gradeIndex++)
                    {
                        calculatedStats.RemoveContributionsFromSource(CalculatedStatContributionSourceCategory.Skill, GradeSourceId(record.skillDefinitionId, (SkillGrade)gradeIndex), restoring);
                    }
                }
            }

            using (SuppressNotifications())
            {
                hiddenProgress.Clear();
                learnedSkills.Clear();
                processedActionSkillKeys.Clear();
                hiddenProgress.AddRange((saveData.hiddenLearningProgress ?? new List<SkillLearningProgressRecord>()).Select(CloneProgress));
                learnedSkills.AddRange((saveData.learnedSkills ?? new List<RuntimeSkillRecord>()).Select(CloneSkill));
                foreach (string key in saveData.consumedActionEventIds ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        processedActionSkillKeys.Add(key);
                    }
                }
            }

            RebuildSkillEffects(restoring);
            return true;
        }

        public static bool ValidateSaveData(PlayerSkillsSaveData saveData, DefinitionRegistry registry, out string failureReason)
        {
            failureReason = string.Empty;
            if (saveData == null)
            {
                failureReason = "Skill save data is missing.";
                return false;
            }

            if (saveData.schemaVersion != PlayerSkillsSaveData.CurrentSchemaVersion)
            {
                failureReason = $"Unsupported player Skills schema version {saveData.schemaVersion}.";
                return false;
            }

            if (registry == null)
            {
                failureReason = "Definition registry is not available for Skill restore.";
                return false;
            }

            HashSet<string> learnedIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (RuntimeSkillRecord skill in saveData.learnedSkills ?? new List<RuntimeSkillRecord>())
            {
                if (skill == null || string.IsNullOrWhiteSpace(skill.skillDefinitionId))
                {
                    failureReason = "Learned Skill record is missing a Skill definition ID.";
                    return false;
                }

                if (!learnedIds.Add(skill.skillDefinitionId))
                {
                    failureReason = $"Duplicate learned Skill '{skill.skillDefinitionId}' in save data.";
                    return false;
                }

                if (!registry.TryGet(skill.skillDefinitionId, out SkillDefinition definition))
                {
                    failureReason = $"Learned Skill references unknown SkillDefinition '{skill.skillDefinitionId}'.";
                    return false;
                }

                SkillGrade grade = (SkillGrade)skill.currentGrade;
                if (!SkillGradeUtility.IsValid(grade))
                {
                    failureReason = $"Learned Skill '{skill.skillDefinitionId}' has invalid grade '{skill.currentGrade}'.";
                    return false;
                }

                if (skill.currentXp < 0 || skill.lifetimeXp < 0 || skill.lifetimeValidUses < 0 || skill.lifetimeXp < skill.currentXp)
                {
                    failureReason = $"Learned Skill '{skill.skillDefinitionId}' has invalid XP counters.";
                    return false;
                }

                if (grade == SkillGrade.AAA && skill.currentXp != 0)
                {
                    failureReason = $"Mastered Skill '{skill.skillDefinitionId}' cannot store current XP in alpha.";
                    return false;
                }

                if (grade != SkillGrade.AAA && skill.currentXp >= definition.GetXpThresholdFrom(grade))
                {
                    failureReason = $"Learned Skill '{skill.skillDefinitionId}' has unprocessed XP for grade {grade}.";
                    return false;
                }
            }

            HashSet<string> progressIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (SkillLearningProgressRecord progress in saveData.hiddenLearningProgress ?? new List<SkillLearningProgressRecord>())
            {
                if (progress == null || string.IsNullOrWhiteSpace(progress.skillDefinitionId))
                {
                    failureReason = "Hidden Skill progress record is missing a Skill definition ID.";
                    return false;
                }

                if (!progressIds.Add(progress.skillDefinitionId))
                {
                    failureReason = $"Duplicate hidden Skill progress '{progress.skillDefinitionId}' in save data.";
                    return false;
                }

                if (learnedIds.Contains(progress.skillDefinitionId))
                {
                    failureReason = $"Skill '{progress.skillDefinitionId}' is both learned and hidden progress.";
                    return false;
                }

                if (!registry.TryGet(progress.skillDefinitionId, out SkillDefinition definition))
                {
                    failureReason = $"Hidden Skill progress references unknown SkillDefinition '{progress.skillDefinitionId}'.";
                    return false;
                }

                int required = progress.requiredCountSnapshot <= 0 ? definition.NaturalLearning.RequiredCount : progress.requiredCountSnapshot;
                if (progress.currentHiddenCount < 0 || progress.currentHiddenCount >= required)
                {
                    failureReason = $"Hidden Skill progress '{progress.skillDefinitionId}' has an invalid hidden count.";
                    return false;
                }
            }

            return true;
        }

        public string BuildDiagnosticSummary(bool includeHidden)
        {
            EnsureConfiguredFromFallback();
            List<string> lines = new List<string> { "Feature 5.3 Skills" };
            if (learnedSkills.Count == 0)
            {
                lines.Add("Learned: None");
            }
            else
            {
                foreach (RuntimeSkillRecord record in learnedSkills.OrderBy(record => record.skillDefinitionId))
                {
                    SkillDefinition definition = definitionsById.TryGetValue(record.skillDefinitionId, out SkillDefinition found) ? found : null;
                    SkillGrade grade = SkillGradeUtility.Clamp((SkillGrade)record.currentGrade);
                    int next = grade == SkillGrade.AAA || definition == null ? 0 : definition.GetXpThresholdFrom(grade);
                    string nextText = grade == SkillGrade.AAA ? "Mastered" : $"{record.currentXp}/{next} XP";
                    lines.Add($"{(definition == null ? record.skillDefinitionId : definition.DisplayName)}: {grade} {nextText} Uses={record.lifetimeValidUses} LifetimeXP={record.lifetimeXp}");
                }
            }

            if (includeHidden)
            {
                lines.Add("Hidden Learning:");
                if (hiddenProgress.Count == 0)
                {
                    lines.Add("None");
                }
                else
                {
                    foreach (SkillLearningProgressRecord progress in hiddenProgress.OrderBy(record => record.skillDefinitionId))
                    {
                        lines.Add($"{progress.skillDefinitionId}: {progress.currentHiddenCount}/{progress.requiredCountSnapshot} ({progress.qualifyingEventId})");
                    }
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private SkillOperationResult AddHiddenProgress(SkillDefinition definition, SkillActionExecutionEvent actionEvent)
        {
            SkillLearningProgressRecord progress = hiddenProgress.FirstOrDefault(record => string.Equals(record.skillDefinitionId, definition.Id, StringComparison.Ordinal));
            if (progress == null)
            {
                progress = new SkillLearningProgressRecord
                {
                    skillDefinitionId = definition.Id,
                    requiredCountSnapshot = definition.NaturalLearning.RequiredCount,
                    qualifyingEventId = definition.NaturalLearning.QualifyingEventId,
                    acquisitionState = 0,
                    firstProgressAtUtc = DateTime.UtcNow.ToString("O"),
                    firstProgressAtPlaytimeSeconds = actionEvent.PlaytimeSeconds,
                    sourceSystem = actionEvent.SourceSystem ?? string.Empty
                };
                hiddenProgress.Add(progress);
            }

            progress.currentHiddenCount++;
            progress.latestProgressAtUtc = DateTime.UtcNow.ToString("O");
            progress.latestProgressAtPlaytimeSeconds = actionEvent.PlaytimeSeconds;
            RaiseHiddenProgressChanged(progress, false);

            if (progress.currentHiddenCount >= progress.requiredCountSnapshot)
            {
                SkillGrade startingGrade = definition.NaturalLearning.GrantedStartingGrade;
                hiddenProgress.Remove(progress);
                RuntimeSkillRecord record = CreateLearnedSkillRecord(definition, startingGrade, SkillAcquisitionSource.NaturalLearning, $"Natural learning via {progress.qualifyingEventId}", false);
                learnedSkills.Add(record);
                ApplyReachedGradePackages(definition, record, SkillGrade.F, startingGrade, includeLowerGrades: true, false);
                RaiseSkillLearned(record, false);
                return SkillOperationResult.Success($"Learned {definition.DisplayName} at {startingGrade}.");
            }

            return SkillOperationResult.Success($"Advanced hidden {definition.DisplayName} learning progress.");
        }

        private SkillOperationResult AwardSkillUseInternal(SkillDefinition definition, RuntimeSkillRecord record, string eventId, int amount, string source, string reason, bool restoring)
        {
            SkillGrade oldGrade = SkillGradeUtility.Clamp((SkillGrade)record.currentGrade);
            if (oldGrade == SkillGrade.AAA)
            {
                return SkillOperationResult.Success($"{definition.DisplayName} is mastered; no XP gained.", "MasteredNoXp");
            }

            if (amount <= 0)
            {
                return SkillOperationResult.Failure("InvalidXp", "Skill XP amount must be positive.");
            }

            record.currentXp += amount;
            record.lifetimeXp += amount;
            record.lifetimeValidUses += 1;
            record.lastUseAtUtc = DateTime.UtcNow.ToString("O");
            record.lastUseAtPlaytimeSeconds = 0d;

            ProcessPromotions(definition, record, source, reason, restoring);
            RaiseSkillsChanged(restoring);
            return SkillOperationResult.Success($"Awarded {amount} XP to {definition.DisplayName}.");
        }

        private void ProcessPromotions(SkillDefinition definition, RuntimeSkillRecord record, string source, string reason, bool restoring)
        {
            while (true)
            {
                SkillGrade current = SkillGradeUtility.Clamp((SkillGrade)record.currentGrade);
                if (current == SkillGrade.AAA)
                {
                    record.currentXp = 0;
                    break;
                }

                int threshold = definition.GetXpThresholdFrom(current);
                if (threshold <= 0 || record.currentXp < threshold || !SkillGradeUtility.TryGetNext(current, out SkillGrade next))
                {
                    break;
                }

                record.currentXp -= threshold;
                PromoteTo(definition, record, next, source, reason, restoring);
            }
        }

        private void PromoteTo(SkillDefinition definition, RuntimeSkillRecord record, SkillGrade requestedGrade, string source, string reason, bool restoring)
        {
            SkillGrade oldGrade = SkillGradeUtility.Clamp((SkillGrade)record.currentGrade);
            SkillGrade newGrade = SkillGradeUtility.Clamp(requestedGrade);
            if (newGrade <= oldGrade)
            {
                return;
            }

            record.currentGrade = (int)newGrade;
            record.promotionHistory.Add(new SkillPromotionRecord
            {
                fromGrade = (int)oldGrade,
                toGrade = (int)newGrade,
                promotedAtUtc = DateTime.UtcNow.ToString("O"),
                promotedAtPlaytimeSeconds = 0d,
                source = source ?? string.Empty,
                reason = reason ?? string.Empty
            });

            ApplyReachedGradePackages(definition, record, oldGrade, newGrade, includeLowerGrades: false, restoring);
            RaiseSkillPromoted(record, oldGrade, newGrade, restoring);
            if (newGrade == SkillGrade.AAA)
            {
                record.currentXp = 0;
                RaiseSkillMastered(record, restoring);
            }
        }

        private void ApplyReachedGradePackages(SkillDefinition definition, RuntimeSkillRecord record, SkillGrade oldGrade, SkillGrade newGrade, bool includeLowerGrades, bool restoring)
        {
            int start = includeLowerGrades ? SkillGradeUtility.MinimumIndex : SkillGradeUtility.ToIndex(oldGrade) + 1;
            int end = SkillGradeUtility.ToIndex(newGrade);
            for (int gradeIndex = start; gradeIndex <= end; gradeIndex++)
            {
                SkillGrade grade = (SkillGrade)gradeIndex;
                string sourceId = GradeSourceId(definition.Id, grade);
                if (record.appliedGradeSourceIds.Contains(sourceId))
                {
                    continue;
                }

                foreach (SkillGradeEffectPackageDefinition package in definition.GradePackages.Where(package => package != null && package.Grade == grade))
                {
                    ApplyGradePackage(definition, record, package, sourceId, restoring);
                }

                ApplyEligibleUnlocks(definition, record, definition.AbilityUnlocks, grade, sourceId);
                record.appliedGradeSourceIds.Add(sourceId);
            }
        }

        private void ApplyGradePackage(SkillDefinition definition, RuntimeSkillRecord record, SkillGradeEffectPackageDefinition package, string sourceId, bool restoring)
        {
            if (calculatedStats != null)
            {
                foreach (SkillCalculatedStatContributionDefinition authored in package.CalculatedStatContributions)
                {
                    if (authored?.CalculatedStat == null)
                    {
                        continue;
                    }

                    RuntimeCalculatedStatContribution contribution = new RuntimeCalculatedStatContribution
                    {
                        contributionId = $"{sourceId}.{authored.CalculatedStat.Id}".ToLowerInvariant(),
                        statId = authored.CalculatedStat.Id,
                        sourceId = sourceId,
                        sourceCategory = (int)CalculatedStatContributionSourceCategory.Skill,
                        kind = (int)authored.Kind,
                        direction = (int)authored.Direction,
                        magnitude = authored.Magnitude,
                        priority = authored.Priority
                    };
                    calculatedStats.AddContribution(contribution, out _, restoring);
                }
            }

            ApplyEligibleUnlocks(definition, record, package.AbilityUnlocks, package.Grade, sourceId);
            foreach (string capabilityId in package.CapabilityUnlockIds)
            {
                if (!string.IsNullOrWhiteSpace(capabilityId) && !record.unlockedCapabilityIds.Contains(capabilityId))
                {
                    record.unlockedCapabilityIds.Add(capabilityId);
                }
            }
        }

        private void ApplyEligibleUnlocks(SkillDefinition definition, RuntimeSkillRecord record, IReadOnlyList<SkillAbilityUnlockDefinition> unlocks, SkillGrade reachedGrade, string sourceId)
        {
            foreach (SkillAbilityUnlockDefinition unlock in unlocks ?? Array.Empty<SkillAbilityUnlockDefinition>())
            {
                if (unlock?.Ability == null || !unlock.AlphaAvailable || unlock.RequiredGrade > reachedGrade)
                {
                    continue;
                }

                if (!record.unlockedAbilityIds.Contains(unlock.AbilityId))
                {
                    record.unlockedAbilityIds.Add(unlock.AbilityId);
                }

                abilities?.Grant(unlock.Ability, AbilityGrantSourceCategory.Skill, sourceId);

                if (spellLoadout != null && registry != null)
                {
                    SpellDefinition spell = registry.DefinitionsById.Values
                        .OfType<SpellDefinition>()
                        .FirstOrDefault(candidate => candidate.Ability != null && string.Equals(candidate.Ability.Id, unlock.AbilityId, StringComparison.Ordinal));
                    if (spell != null)
                    {
                        spellLoadout.LearnSpell(spell);
                    }
                }
            }
        }

        private bool Matches(SkillDefinition definition, SkillActionExecutionEvent actionEvent)
        {
            SkillNaturalLearningDefinition learning = definition.NaturalLearning;
            if (learning == null || !learning.Enabled)
            {
                return false;
            }

            if (!string.Equals(learning.QualifyingEventId, actionEvent.ActionDefinitionId, StringComparison.Ordinal))
            {
                return false;
            }

            if (learning.ActionCategory != SkillActionEventCategory.Unknown && learning.ActionCategory != actionEvent.ActionCategory)
            {
                return false;
            }

            if (learning.RequiredItemCategory != null && !ClassificationUtility.IsInCategory(actionEvent.ItemCategory, learning.RequiredItemCategory))
            {
                return false;
            }

            if (learning.RequiredItemTag != null && !ContainsTag(actionEvent.ItemTags, learning.RequiredItemTag.Id))
            {
                return false;
            }

            if (learning.RequiredMagicTag != null && !ContainsTag(actionEvent.MagicTags, learning.RequiredMagicTag.Id))
            {
                return false;
            }

            if (learning.RequiredActionTag != null && !ContainsTag(actionEvent.ActionTags, learning.RequiredActionTag.Id))
            {
                return false;
            }

            return true;
        }

        private RuntimeSkillRecord CreateLearnedSkillRecord(SkillDefinition definition, SkillGrade startingGrade, SkillAcquisitionSource source, string reason, bool restoring)
        {
            SkillGrade grade = SkillGradeUtility.Clamp(startingGrade);
            return new RuntimeSkillRecord
            {
                skillDefinitionId = definition.Id,
                currentGrade = (int)grade,
                currentXp = 0,
                lifetimeXp = 0,
                lifetimeValidUses = 0,
                acquisitionSource = (int)source,
                acquisitionReason = reason ?? string.Empty,
                acquisitionAtUtc = DateTime.UtcNow.ToString("O"),
                acquisitionAtPlaytimeSeconds = 0d,
                startingGrade = (int)grade
            };
        }

        private RuntimeSkillRecord FindLiveSkill(string skillId)
        {
            return learnedSkills.FirstOrDefault(record => string.Equals(record.skillDefinitionId, skillId, StringComparison.Ordinal));
        }

        private void EnsureConfiguredFromFallback()
        {
            if (!IsConfigured && fallbackDefinitions.Count > 0)
            {
                Configure(fallbackDefinitions, registry, calculatedStats, spellLoadout);
            }
        }

        private Scope SuppressNotifications()
        {
            notificationsSuppressed = true;
            return new Scope(this);
        }

        private void RaiseSkillsChanged(bool restoring)
        {
            if (!notificationsSuppressed)
            {
                SkillsChanged?.Invoke(this, restoring);
            }
        }

        private void RaiseHiddenProgressChanged(SkillLearningProgressRecord progress, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                HiddenProgressChanged?.Invoke(this, CloneProgress(progress), restoring);
            }
        }

        private void RaiseSkillLearned(RuntimeSkillRecord record, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                SkillLearned?.Invoke(this, CloneSkill(record), restoring);
            }
        }

        private void RaiseSkillPromoted(RuntimeSkillRecord record, SkillGrade oldGrade, SkillGrade newGrade, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                SkillPromoted?.Invoke(this, new SkillChangedEventArgs(CloneSkill(record), oldGrade, newGrade, 0, restoring));
            }
        }

        private void RaiseSkillMastered(RuntimeSkillRecord record, bool restoring)
        {
            if (!notificationsSuppressed)
            {
                SkillMastered?.Invoke(this, CloneSkill(record), restoring);
            }
        }

        private static bool ContainsTag(IReadOnlyList<TagDefinition> tags, string tagId)
        {
            return tags != null && tags.Any(tag => tag != null && string.Equals(tag.Id, tagId, StringComparison.Ordinal));
        }

        private static string GradeSourceId(string skillId, SkillGrade grade)
        {
            return $"{skillId}.grade.{SkillGradeUtility.DisplayLabel(grade).ToLowerInvariant()}";
        }

        private static string ProcessKey(string eventId, string skillId)
        {
            return $"{eventId}|{skillId}";
        }

        private static string CreateRuntimeId(string prefix)
        {
            return $"{prefix}.{Guid.NewGuid():N}".ToLowerInvariant();
        }

        private static SkillLearningProgressRecord CloneProgress(SkillLearningProgressRecord record)
        {
            return record == null
                ? null
                : new SkillLearningProgressRecord
                {
                    skillDefinitionId = record.skillDefinitionId,
                    currentHiddenCount = record.currentHiddenCount,
                    requiredCountSnapshot = record.requiredCountSnapshot,
                    qualifyingEventId = record.qualifyingEventId,
                    acquisitionState = record.acquisitionState,
                    firstProgressAtUtc = record.firstProgressAtUtc,
                    firstProgressAtPlaytimeSeconds = record.firstProgressAtPlaytimeSeconds,
                    latestProgressAtUtc = record.latestProgressAtUtc,
                    latestProgressAtPlaytimeSeconds = record.latestProgressAtPlaytimeSeconds,
                    sourceSystem = record.sourceSystem,
                    futureConditionData = record.futureConditionData
                };
        }

        private static RuntimeSkillRecord CloneSkill(RuntimeSkillRecord record)
        {
            return record == null
                ? null
                : new RuntimeSkillRecord
                {
                    skillDefinitionId = record.skillDefinitionId,
                    currentGrade = record.currentGrade,
                    currentXp = record.currentXp,
                    lifetimeXp = record.lifetimeXp,
                    lifetimeValidUses = record.lifetimeValidUses,
                    acquisitionSource = record.acquisitionSource,
                    acquisitionReason = record.acquisitionReason,
                    acquisitionAtUtc = record.acquisitionAtUtc,
                    acquisitionAtPlaytimeSeconds = record.acquisitionAtPlaytimeSeconds,
                    startingGrade = record.startingGrade,
                    lastUseAtUtc = record.lastUseAtUtc,
                    lastUseAtPlaytimeSeconds = record.lastUseAtPlaytimeSeconds,
                    promotionHistory = record.promotionHistory == null ? new List<SkillPromotionRecord>() : record.promotionHistory.Select(ClonePromotion).ToList(),
                    appliedGradeSourceIds = record.appliedGradeSourceIds == null ? new List<string>() : new List<string>(record.appliedGradeSourceIds),
                    unlockedAbilityIds = record.unlockedAbilityIds == null ? new List<string>() : new List<string>(record.unlockedAbilityIds),
                    unlockedCapabilityIds = record.unlockedCapabilityIds == null ? new List<string>() : new List<string>(record.unlockedCapabilityIds)
                };
        }

        private static SkillPromotionRecord ClonePromotion(SkillPromotionRecord record)
        {
            return record == null
                ? null
                : new SkillPromotionRecord
                {
                    fromGrade = record.fromGrade,
                    toGrade = record.toGrade,
                    promotedAtUtc = record.promotedAtUtc,
                    promotedAtPlaytimeSeconds = record.promotedAtPlaytimeSeconds,
                    source = record.source,
                    reason = record.reason
                };
        }

        private readonly struct Scope : IDisposable
        {
            private readonly CharacterSkillCollection owner;

            public Scope(CharacterSkillCollection owner)
            {
                this.owner = owner;
            }

            public void Dispose()
            {
                if (owner != null)
                {
                    owner.notificationsSuppressed = false;
                }
            }
        }
    }
}
