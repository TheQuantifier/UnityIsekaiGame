#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityIsekaiGame.Beings.Biology.BiologicalConditions;
using UnityIsekaiGame.Beings.Biology.Compatibility;
using UnityIsekaiGame.Beings.Biology.Hazards;
using UnityIsekaiGame.Beings.Biology.Recovery;
using UnityIsekaiGame.Beings.Biology.Transformation;
using UnityIsekaiGame.Beings.Biology.VitalProcesses;
using UnityIsekaiGame.Combat;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Combat.Reactions;
using UnityIsekaiGame.Combat.Contributions;
using UnityIsekaiGame.Contracts;
using UnityIsekaiGame.Development.Automation;
using UnityIsekaiGame.Factions;
using UnityIsekaiGame.GameData;
using UnityIsekaiGame.Inventory;
using UnityIsekaiGame.People;
using UnityIsekaiGame.Places;
using UnityIsekaiGame.Progression;
using UnityIsekaiGame.Quests;
using UnityIsekaiGame.Requirements;
using UnityIsekaiGame.Skills;
using UnityIsekaiGame.Stats;
using UnityIsekaiGame.StatusEffects;
using UnityIsekaiGame.Traits;

namespace UnityIsekaiGame.Development
{
    public sealed class PrototypeTestLabView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.055f, 0.065f, 0.075f, 0.96f);
        private static readonly Color FieldColor = new Color(0.10f, 0.12f, 0.14f, 1f);
        private static readonly Color ButtonColor = new Color(0.17f, 0.25f, 0.29f, 1f);
        private static readonly Color ActiveButtonColor = new Color(0.20f, 0.42f, 0.55f, 1f);
        private const int MaximumDynamicReportCharacters = 10000;
        private const int MaximumDynamicReportLines = 220;
        private const float MaximumDynamicReportHeight = 900f;

        private static readonly string[] SectionNames =
        {
            "Overview",
            "Player",
            "Character 5.6",
            "Body Species 7.1",
            "Body Anatomy 7.2",
            "Body Condition 7.3",
            "Vital Processes 7.4",
            "Biological Hazards 7.5",
            "Biological Compatibility 7.6",
            "Natural Recovery 7.7",
            "Transformation 7.8",
            "Biological Conditions 7.9",
            "Biology Integration 7.10",
            "Knowledge 8.1",
            "Observation 8.2",
            "History 8.3",
            "Memory 8.4",
            "Life Events 8.5",
            "Sources 8.6",
            "Sharing 8.7",
            "Access 8.8",
            "Records 8.9",
            "Integration 8.10",
            "Identity 5.1",
            "Numbers 5.4a",
            "Resources 5.4b",
            "Traits 5.5",
            "Skills 5.3",
            "Inventory",
            "Combat",
            "Defense 6.6",
            "Execution 6.7",
            "Reactions 6.8",
            "Contribution 6.9",
            "Combat Overview 6.10",
            "Combat State 6.5",
            "Lifecycle 6.3",
            "Ongoing 6.4",
            "Statuses",
            "Quests",
            "Persistence",
            "Location",
            "World Entities",
            "Automation",
            "Scenarios",
            "Diagnostics"
        };

        private PrototypeTestLabService service;
        private InputField quantityInput;
        private InputField amountInput;
        private InputField attackHitChanceInput;
        private InputField attackHitRollInput;
        private InputField attackCriticalChanceInput;
        private InputField attackCriticalRollInput;
        private InputField attackCriticalMultiplierInput;
        private InputField attackDistanceInput;
        private InputField attackMaximumRangeInput;
        private InputField ongoingAmountInput;
        private InputField ongoingIntervalInput;
        private InputField ongoingDurationInput;
        private InputField ongoingTickCountInput;
        private InputField ongoingStackInput;
        private Text overviewText;
        private Text latestOperationText;
        private Text diagnosticsText;
        private Text historyText;
        private Text locationText;
        private Text worldEntityText;
        private Text persistenceText;
        private Text persistenceIntegrationText;
        private Text characterSystemText;
        private Text bodySpeciesText;
        private Text bodyAnatomyText;
        private Text bodyConditionText;
        private Text vitalProcessesText;
        private Text biologicalHazardsText;
        private Text biologicalCompatibilityText;
        private Text biologicalRecoveryText;
        private Text bodyTransformationText;
        private Text biologicalConditionsText;
        private Text bodyBiologyIntegrationText;
        private Text knowledgeText;
        private Text observationText;
        private Text characterHistoryText;
        private Text memoryRecallText;
        private Text lifeEventsText;
        private Text informationSourcesText;
        private Text informationSharingText;
        private Text informationAccessText;
        private Text knowledgeRecordsText;
        private Text knowledgeHistoryIntegrationText;
        private Text identityProgressionText;
        private Text attributesCalculatedStatsText;
        private Text resourcesText;
        private Text lifecycleText;
        private Text defensiveActionsText;
        private Text combatExecutionText;
        private Text combatReactionText;
        private Text combatContributionText;
        private Text combatRuntimeText;
        private Text combatStateText;
        private Text ongoingEffectsText;
        private Text automationText;
        private Text traitsText;
        private Text skillsText;
        private Text itemValueText;
        private Text skillValueText;
        private Text traitValueText;
        private Text requirementValueText;
        private Text statusValueText;
        private Text roleValueText;
        private Text socialStatusValueText;
        private Text currencyValueText;
        private Text damageValueText;
        private Text defenseValueText;
        private Text combatExecutionValueText;
        private Text combatReactionValueText;
        private Text automationSuiteValueText;
        private Text automationScenarioValueText;
        private Text ongoingEffectValueText;
        private Text questValueText;
        private Text contractValueText;
        private Text placeValueText;
        private Text personValueText;
        private Text testPointValueText;
        private ScrollRect bodyScrollRect;

        private int activeSectionIndex;
        private int selectedItemIndex;
        private int selectedStatusIndex;
        private int selectedRoleIndex;
        private int selectedSocialStatusIndex;
        private int selectedCurrencyIndex;
        private int selectedDamageIndex;
        private int selectedDefenseIndex;
        private int selectedCombatExecutionIndex;
        private int selectedCombatReactionIndex;
        private int selectedAutomationSuiteIndex;
        private int selectedAutomationScenarioIndex;
        private int selectedOngoingEffectIndex;
        private int selectedQuestIndex;
        private int selectedContractIndex;
        private int selectedPlaceIndex;
        private int selectedPersonIndex;
        private int selectedTestPointIndex;
        private int selectedSkillIndex;
        private int selectedTraitIndex;
        private int selectedRequirementIndex;

        private readonly List<ItemDefinition> items = new List<ItemDefinition>();
        private readonly List<StatusEffectDefinition> statuses = new List<StatusEffectDefinition>();
        private readonly List<RoleDefinition> roles = new List<RoleDefinition>();
        private readonly List<SocialStatusDefinition> socialStatuses = new List<SocialStatusDefinition>();
        private readonly List<CurrencyDefinition> currencies = new List<CurrencyDefinition>();
        private readonly List<DamageTypeDefinition> damageTypes = new List<DamageTypeDefinition>();
        private readonly List<DefensiveActionDefinition> defensiveActions = new List<DefensiveActionDefinition>();
        private readonly List<CombatExecutionDefinition> combatExecutions = new List<CombatExecutionDefinition>();
        private readonly List<CombatReactionDefinition> combatReactions = new List<CombatReactionDefinition>();
        private readonly List<ITestLabAutomationSuite> automationSuites = new List<ITestLabAutomationSuite>();
        private readonly List<ITestLabAutomationScenario> automationScenarios = new List<ITestLabAutomationScenario>();
        private readonly List<OngoingEffectDefinition> ongoingEffects = new List<OngoingEffectDefinition>();
        private readonly List<QuestDefinition> quests = new List<QuestDefinition>();
        private readonly List<ContractDefinition> contracts = new List<ContractDefinition>();
        private readonly List<PlaceDefinition> places = new List<PlaceDefinition>();
        private readonly List<PersonDefinition> people = new List<PersonDefinition>();
        private readonly List<PrototypeTestPoint> testPoints = new List<PrototypeTestPoint>();
        private readonly List<SkillDefinition> skills = new List<SkillDefinition>();
        private readonly List<TraitDefinition> traits = new List<TraitDefinition>();
        private readonly List<RequirementSetDefinition> requirements = new List<RequirementSetDefinition>();
        private readonly List<GameObject> sectionRoots = new List<GameObject>();
        private readonly List<Button> sectionGroupButtons = new List<Button>();
        private readonly List<Button> sectionFeatureButtons = new List<Button>();
        private readonly List<SectionNavigationGroup> sectionGroups = new List<SectionNavigationGroup>();
        private readonly List<Text> dynamicTextBlocks = new List<Text>();
        private Transform sectionFeatureMenuRoot;
        private int activeSectionGroupIndex;
        private bool automationStopOnFirstFailure;
        private bool automationAutoScroll = true;
        private bool automationCancelRequested;
        private Coroutine automationRunCoroutine;
        private string automationProgressText;

        private void OnEnable()
        {
            if (service != null)
            {
                service.HistoryChanged += Refresh;
            }
        }

        private void OnDisable()
        {
            if (service != null)
            {
                service.HistoryChanged -= Refresh;
            }
        }

        public void Initialize(PrototypeTestLabService testLabService)
        {
            if (service != null)
            {
                service.HistoryChanged -= Refresh;
            }

            service = testLabService;
            if (service != null)
            {
                service.HistoryChanged += Refresh;
            }

            BuildUi();
            RefreshSelectors();
            Refresh();
            SetActiveSection(activeSectionIndex);
        }

        public void Refresh()
        {
            if (service == null)
            {
                return;
            }

            RefreshActiveSectionSummary();
            UpdateSelectorLabels();
            UpdateLatestOperation();
            UpdateHistory();
            UpdateSectionButtonStates();
        }

        private void BuildUi()
        {
            if (sectionRoots.Count > 0)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            RectTransform root = GetComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = new Vector2(14f, 14f);
            root.offsetMax = new Vector2(-14f, -14f);

            GameObject layoutRoot = CreateChild("Test Lab Layout", root, typeof(VerticalLayoutGroup));
            VerticalLayoutGroup rootLayout = layoutRoot.GetComponent<VerticalLayoutGroup>();
            rootLayout.spacing = 8f;
            rootLayout.padding = new RectOffset(10, 10, 10, 10);
            rootLayout.childControlWidth = true;
            rootLayout.childControlHeight = true;
            rootLayout.childForceExpandWidth = true;
            rootLayout.childForceExpandHeight = false;

            Image background = layoutRoot.AddComponent<Image>();
            background.color = PanelColor;

            AddHeader(layoutRoot.transform, font, "Development Actions - Editor/Development Builds Only");
            AddSectionTabs(layoutRoot.transform, font);
            bodyScrollRect = AddBodyScroll(layoutRoot.transform);
            latestOperationText = AddText(layoutRoot.transform, font, "Last Result: No operations yet.", 12, 44);
            latestOperationText.color = new Color(0.86f, 0.92f, 0.96f, 1f);

            Transform content = bodyScrollRect.content;
            Transform overviewSection = AddSection(content, "Overview Section");
            Transform playerSection = AddSection(content, "Player Section");
            Transform feature56Section = AddSection(content, "Character 5.6 Section");
            Transform bodySpeciesSection = AddSection(content, "Body Species 7.1 Section");
            Transform bodyAnatomySection = AddSection(content, "Body Anatomy 7.2 Section");
            Transform bodyConditionSection = AddSection(content, "Body Condition 7.3 Section");
            Transform vitalProcessesSection = AddSection(content, "Vital Processes 7.4 Section");
            Transform biologicalHazardsSection = AddSection(content, "Biological Hazards 7.5 Section");
            Transform biologicalCompatibilitySection = AddSection(content, "Biological Compatibility 7.6 Section");
            Transform biologicalRecoverySection = AddSection(content, "Natural Recovery 7.7 Section");
            Transform bodyTransformationSection = AddSection(content, "Transformation 7.8 Section");
            Transform biologicalConditionsSection = AddSection(content, "Biological Conditions 7.9 Section");
            Transform bodyBiologyIntegrationSection = AddSection(content, "Biology Integration 7.10 Section");
            Transform knowledgeSection = AddSection(content, "Knowledge 8.1 Section");
            Transform observationSection = AddSection(content, "Observation 8.2 Section");
            Transform characterHistorySection = AddSection(content, "History 8.3 Section");
            Transform memoryRecallSection = AddSection(content, "Memory 8.4 Section");
            Transform lifeEventsSection = AddSection(content, "Life Events 8.5 Section");
            Transform informationSourcesSection = AddSection(content, "Sources 8.6 Section");
            Transform informationSharingSection = AddSection(content, "Sharing 8.7 Section");
            Transform informationAccessSection = AddSection(content, "Access 8.8 Section");
            Transform knowledgeRecordsSection = AddSection(content, "Records 8.9 Section");
            Transform knowledgeHistoryIntegrationSection = AddSection(content, "Integration 8.10 Section");
            Transform identitySection = AddSection(content, "Identity 5.1 Section");
            Transform feature52Section = AddSection(content, "Numbers 5.4a Section");
            Transform feature54bSection = AddSection(content, "Resources 5.4b Section");
            Transform feature55Section = AddSection(content, "Traits 5.5 Section");
            Transform feature53Section = AddSection(content, "Skills 5.3 Section");
            Transform inventorySection = AddSection(content, "Inventory Section");
            Transform combatSection = AddSection(content, "Combat Section");
            Transform defensiveActionsSection = AddSection(content, "Defensive Actions Section");
            Transform combatExecutionSection = AddSection(content, "Combat Execution Section");
            Transform combatReactionSection = AddSection(content, "Combat Reactions Section");
            Transform combatContributionSection = AddSection(content, "Combat Contribution Section");
            Transform combatRuntimeSection = AddSection(content, "Combat Runtime Section");
            Transform combatStateSection = AddSection(content, "Combat State Section");
            Transform lifecycleSection = AddSection(content, "Lifecycle Section");
            Transform ongoingEffectsSection = AddSection(content, "Ongoing Effects Section");
            Transform statusSection = AddSection(content, "Statuses Section");
            Transform questSection = AddSection(content, "Quests Section");
            Transform persistenceSection = AddSection(content, "Persistence Section");
            Transform locationSection = AddSection(content, "Location Section");
            Transform worldEntitySection = AddSection(content, "World Entities Section");
            Transform automationSection = AddSection(content, "Automation Section");
            Transform scenarioSection = AddSection(content, "Scenarios Section");
            Transform diagnosticsSection = AddSection(content, "Diagnostics Section");

            BuildOverviewSection(overviewSection, font);
            BuildPlayerSection(playerSection, font);
            BuildFeature56Section(feature56Section, font);
            BuildBodySpeciesSection(bodySpeciesSection, font);
            BuildBodyAnatomySection(bodyAnatomySection, font);
            BuildBodyConditionSection(bodyConditionSection, font);
            BuildVitalProcessesSection(vitalProcessesSection, font);
            BuildBiologicalHazardsSection(biologicalHazardsSection, font);
            BuildBiologicalCompatibilitySection(biologicalCompatibilitySection, font);
            BuildBiologicalRecoverySection(biologicalRecoverySection, font);
            BuildBodyTransformationSection(bodyTransformationSection, font);
            BuildBiologicalConditionsSection(biologicalConditionsSection, font);
            BuildBodyBiologyIntegrationSection(bodyBiologyIntegrationSection, font);
            BuildKnowledgeSection(knowledgeSection, font);
            BuildObservationSection(observationSection, font);
            BuildCharacterHistorySection(characterHistorySection, font);
            BuildMemoryRecallSection(memoryRecallSection, font);
            BuildLifeEventsSection(lifeEventsSection, font);
            BuildInformationSourcesSection(informationSourcesSection, font);
            BuildInformationSharingSection(informationSharingSection, font);
            BuildInformationAccessSection(informationAccessSection, font);
            BuildKnowledgeRecordsSection(knowledgeRecordsSection, font);
            BuildKnowledgeHistoryIntegrationSection(knowledgeHistoryIntegrationSection, font);
            BuildIdentityProgressionSection(identitySection, font);
            BuildFeature52Section(feature52Section, font);
            BuildFeature54bSection(feature54bSection, font);
            BuildFeature55Section(feature55Section, font);
            BuildFeature53Section(feature53Section, font);
            BuildInventorySection(inventorySection, font);
            BuildCombatSection(combatSection, font);
            BuildDefensiveActionsSection(defensiveActionsSection, font);
            BuildCombatExecutionSection(combatExecutionSection, font);
            BuildCombatReactionSection(combatReactionSection, font);
            BuildCombatContributionSection(combatContributionSection, font);
            BuildCombatRuntimeSection(combatRuntimeSection, font);
            BuildCombatStateSection(combatStateSection, font);
            BuildLifecycleSection(lifecycleSection, font);
            BuildOngoingEffectsSection(ongoingEffectsSection, font);
            BuildStatusSection(statusSection, font);
            BuildQuestSection(questSection, font);
            BuildPersistenceSection(persistenceSection, font);
            BuildLocationSection(locationSection, font);
            BuildWorldEntitySection(worldEntitySection, font);
            BuildAutomationSection(automationSection, font);
            BuildScenarioSection(scenarioSection, font);
            BuildDiagnosticsSection(diagnosticsSection, font);
        }

        private void BuildOverviewSection(Transform parent, Font font)
        {
            itemValueText = AddSelectorRow(parent, font, "Item", () => CycleSelection(ref selectedItemIndex, items.Count, -1), () => CycleSelection(ref selectedItemIndex, items.Count, 1));
            statusValueText = AddSelectorRow(parent, font, "Status", () => CycleSelection(ref selectedStatusIndex, statuses.Count, -1), () => CycleSelection(ref selectedStatusIndex, statuses.Count, 1));
            damageValueText = AddSelectorRow(parent, font, "Damage", () => CycleSelection(ref selectedDamageIndex, damageTypes.Count, -1), () => CycleSelection(ref selectedDamageIndex, damageTypes.Count, 1));
            defenseValueText = AddSelectorRow(parent, font, "Defense", () => CycleSelection(ref selectedDefenseIndex, defensiveActions.Count, -1), () => CycleSelection(ref selectedDefenseIndex, defensiveActions.Count, 1));
            ongoingEffectValueText = AddSelectorRow(parent, font, "Ongoing", () => CycleSelection(ref selectedOngoingEffectIndex, ongoingEffects.Count, -1), () => CycleSelection(ref selectedOngoingEffectIndex, ongoingEffects.Count, 1));
            questValueText = AddSelectorRow(parent, font, "Quest", () => CycleSelection(ref selectedQuestIndex, quests.Count, -1), () => CycleSelection(ref selectedQuestIndex, quests.Count, 1));
            contractValueText = AddSelectorRow(parent, font, "Contract", () => CycleSelection(ref selectedContractIndex, contracts.Count, -1), () => CycleSelection(ref selectedContractIndex, contracts.Count, 1));
            placeValueText = AddSelectorRow(parent, font, "Place", () => CycleSelection(ref selectedPlaceIndex, places.Count, -1), () => CycleSelection(ref selectedPlaceIndex, places.Count, 1));
            personValueText = AddSelectorRow(parent, font, "Person", () => CycleSelection(ref selectedPersonIndex, people.Count, -1), () => CycleSelection(ref selectedPersonIndex, people.Count, 1));
            testPointValueText = AddSelectorRow(parent, font, "Test Point", () => CycleSelection(ref selectedTestPointIndex, testPoints.Count, -1), () => CycleSelection(ref selectedTestPointIndex, testPoints.Count, 1));
            quantityInput = AddInputRow(parent, font, "Quantity", "1");
            amountInput = AddInputRow(parent, font, "Amount", "25");
            AddButtonRow(parent, font,
                ("Refresh", Refresh),
                ("Restore Vitals", () => service.RestoreVitals()),
                ("Run Diagnostics", () => RunDiagnostics()));
            overviewText = AddText(parent, font, "Overview", 13, 220);
        }

        private void BuildPlayerSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Damage Player", () => service.DamagePlayer(GetInt(amountInput, 25))),
                ("Heal Player", () => service.HealPlayer(GetInt(amountInput, 25))),
                ("Set Health", () => service.SetHealth(GetInt(amountInput, 100))));
            AddButtonRow(parent, font,
                ("Drain Mana", () => service.DrainMana(GetFloat(amountInput, 25f))),
                ("Drain Stamina", () => service.DrainStamina(GetFloat(amountInput, 25f))),
                ("Restore Vitals", () => service.RestoreVitals()));
        }

        private void BuildFeature56Section(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Initialize", () => service.InitializeCharacterSystem()),
                ("Full Rebuild", () => service.RebuildCharacterSystem()),
                ("Integrity", () => service.ValidateCharacterSystemIntegrity()),
                ("Snapshot", () => service.SnapshotCharacterSystem()));
            AddButtonRow(parent, font,
                ("Refresh", Refresh),
                ("Diagnostics", () => RunDiagnostics()));
            characterSystemText = AddText(parent, font, "Character System not available.", 12, 420);
        }

        private void BuildBodySpeciesSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Preview Human", () => service.PreviewBodySpecies("species.human")),
                ("Assign Human", () => service.AssignBodySpecies("species.human")),
                ("Reapply", () => service.ReapplyBodySpecies()));
            AddButtonRow(parent, font,
                ("Undead Human", () => service.AssignBodySpecies("species.undead-human")),
                ("Construct", () => service.AssignBodySpecies("species.basic-construct")),
                ("Spirit", () => service.AssignBodySpecies("species.basic-spirit")));
            AddButtonRow(parent, font,
                ("Missing Species", () => service.TestMissingBodySpecies()),
                ("Stale Actor", () => service.TestStaleBodyActor()),
                ("Validate", () => service.ValidateBodyIntegrity()));
            AddButtonRow(parent, font,
                ("Save", () => service.Save()),
                ("Load", () => service.Load()));
            bodySpeciesText = AddText(parent, font, "Body runtime not available.", 12, 500);
        }

        private void BuildBodyAnatomySection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.AssignBodySpecies("species.human")),
                ("Construct", () => service.AssignBodySpecies("species.basic-construct")),
                ("Spirit", () => service.AssignBodySpecies("species.basic-spirit")));
            AddButtonRow(parent, font,
                ("Validate", () => service.ValidateAnatomyIntegrity()),
                ("Snapshot", () => service.SnapshotAnatomy()),
                ("Rebuild", () => service.RebuildAnatomy()),
                ("Stable IDs", () => service.ValidateAnatomyStableRebuild()));
            AddButtonRow(parent, font,
                ("Tail Present", () => service.SetOptionalTailPresence(true)),
                ("Tail Absent", () => service.SetOptionalTailPresence(false)),
                ("Save/Load", () => service.ValidateAnatomySaveRestore()));
            AddButtonRow(parent, font,
                ("Missing Anatomy", () => service.TestMissingAnatomyDefinition()),
                ("Circular Fixture", () => service.TestCircularAnatomyFixture()),
                ("Duplicate Fixture", () => service.TestDuplicateAnatomyNodeFixture()),
                ("Stale Actor", () => service.TestStaleBodyActor()));
            bodyAnatomyText = AddText(parent, font, "Anatomy runtime not available.", 12, 620);
        }

        private void BuildBodyConditionSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.AssignBodySpecies("species.human")),
                ("Construct", () => service.AssignBodySpecies("species.basic-construct")),
                ("Spirit", () => service.AssignBodySpecies("species.basic-spirit")),
                ("Reset Healthy", () => service.ResetBodyConditionHealthy()));
            AddButtonRow(parent, font,
                ("Validate", () => service.ValidateBodyConditionIntegrity()),
                ("Preview Arm", () => service.PreviewLocalizedStructuralDamage("injury.blunt-trauma", "part.arm.left", GetInt(amountInput, 12))),
                ("Apply Arm", () => service.ApplyLocalizedStructuralDamage("injury.blunt-trauma", "part.arm.left", GetInt(amountInput, 12))),
                ("Duplicate", () => service.ProveLocalizedDamageDuplicateProtection()));
            AddButtonRow(parent, font,
                ("Lacerate Hand", () => service.ApplyLocalizedStructuralDamage("injury.laceration", "part.hand.left", GetInt(amountInput, 14))),
                ("Fracture Leg", () => service.ApplyLocalizedStructuralDamage("injury.fracture", "part.leg.left", GetInt(amountInput, 30))),
                ("Burn Arm", () => service.ApplyLocalizedStructuralDamage("injury.burn", "part.arm.right", GetInt(amountInput, 20))),
                ("Sever Arm", () => service.ApplyLocalizedStructuralDamage("injury.severing", "part.arm.left", GetInt(amountInput, 100))));
            AddButtonRow(parent, font,
                ("Remove Injury", () => service.RemoveFirstBodyConditionInjury()),
                ("Save/Load", () => service.ValidateBodyConditionSaveRestore()),
                ("Missing Node", () => service.TestMissingConditionNode()),
                ("Incompatible", () => service.TestIncompatibleConditionInjury()));
            bodyConditionText = AddText(parent, font, "Body condition runtime not available.", 12, 620);
        }

        private void BuildVitalProcessesSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.ResetVitalProcessesHuman()),
                ("Construct", () => service.AssignBodySpecies("species.basic-construct")),
                ("Spirit", () => service.AssignBodySpecies("species.basic-spirit")),
                ("Validate", () => service.ValidateVitalProcessIntegrity()));
            AddButtonRow(parent, font,
                ("Preview Blood", () => service.PreviewVitalResourceMutation(BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, GetFloat(amountInput, 10f))),
                ("Consume Blood", () => service.ApplyVitalResourceMutation(BiologicalResourceIds.Blood, VitalResourceMutationOperation.Consume, GetFloat(amountInput, 10f))),
                ("Restore Blood", () => service.ApplyVitalResourceMutation(BiologicalResourceIds.Blood, VitalResourceMutationOperation.Restore, GetFloat(amountInput, 10f))),
                ("Duplicate", () => service.ProveVitalProcessDuplicateProtection()));
            AddButtonRow(parent, font,
                ("Consume Breath", () => service.ApplyVitalResourceMutation(BiologicalResourceIds.Breath, VitalResourceMutationOperation.Consume, GetFloat(amountInput, 10f))),
                ("Set Temp", () => service.ApplyVitalResourceMutation(BiologicalResourceIds.Temperature, VitalResourceMutationOperation.Set, GetFloat(amountInput, 37f))),
                ("Hunger", () => service.ApplyVitalResourceMutation(BiologicalResourceIds.Nutrition, VitalResourceMutationOperation.Consume, GetFloat(amountInput, 5f))),
                ("Thirst", () => service.ApplyVitalResourceMutation(BiologicalResourceIds.Hydration, VitalResourceMutationOperation.Consume, GetFloat(amountInput, 5f))));
            AddButtonRow(parent, font,
                ("Sleep Need", () => service.ApplyVitalResourceMutation(BiologicalResourceIds.SleepNeed, VitalResourceMutationOperation.Consume, GetFloat(amountInput, 5f))),
                ("Fatigue", () => service.ApplyVitalResourceMutation(BiologicalResourceIds.Fatigue, VitalResourceMutationOperation.Consume, GetFloat(amountInput, 5f))),
                ("Advance 1h", () => service.ApplyVitalProcessUpdate(3600f)),
                ("Deterministic", () => service.ValidateVitalProcessDeterministicUpdate()));
            AddButtonRow(parent, font,
                ("Lung Capacity", () => service.DamageLungAndRecalculateBreath()),
                ("Construct Blood", () => service.TestInactiveVitalResource("species.basic-construct", BiologicalResourceIds.Blood)),
                ("Spirit Breath", () => service.TestInactiveVitalResource("species.basic-spirit", BiologicalResourceIds.Breath)),
                ("Save/Load", () => service.ValidateVitalProcessSaveRestore()));
            vitalProcessesText = AddText(parent, font, "Vital process runtime not available.", 12, 680);
        }

        private void BuildBiologicalHazardsSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.ResetBiologicalHazardsHuman()),
                ("Validate", () => service.ValidateBiologicalHazardIntegrity()),
                ("Sync Vitals", () => service.SynchronizeBiologicalHazardsFromVitals()),
                ("Save/Load", () => service.ValidateBiologicalHazardSaveRestore()));
            AddButtonRow(parent, font,
                ("Bleeding", () => service.AddBleedingHazard()),
                ("2nd Bleed", () => service.AddSecondBleedingHazardSource()),
                ("Preview Tick", () => service.PreviewBiologicalHazardTick(GetFloat(amountInput, 1800f))),
                ("Tick", () => service.ApplyBiologicalHazardTick(GetFloat(amountInput, 1800f))));
            AddButtonRow(parent, font,
                ("Duplicate", () => service.ProveBiologicalHazardTickDuplicateProtection()),
                ("Suppress", () => service.SuppressBleedingHazard()),
                ("Remove Source", () => service.RemoveFirstBiologicalHazardSource()),
                ("Suffocate", () => service.AddSuffocationExposure()));
            AddButtonRow(parent, font,
                ("Heat", () => service.CreateTemperatureHazard(high: true)),
                ("Cold", () => service.CreateTemperatureHazard(high: false)),
                ("Hunger/Thirst", () => service.CreateStarvationAndDehydrationPressure()),
                ("Fatigue/Sleep", () => service.CreateFatigueAndSleepPressure()));
            AddButtonRow(parent, font,
                ("Heat Exposure", () => service.AddHeatExposure()),
                ("Cold Exposure", () => service.AddColdExposure()),
                ("Construct Blood", () => service.TestInactiveBiologicalHazardResource("species.basic-construct", BiologicalHazardIds.Bleeding)),
                ("Spirit Breath", () => service.TestInactiveBiologicalHazardResource("species.basic-spirit", BiologicalHazardIds.Suffocation)));
            biologicalHazardsText = AddText(parent, font, "Biological hazard runtime not available.", 12, 720);
        }

        private void BuildBiologicalCompatibilitySection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.ResetBiologicalCompatibilityHuman()),
                ("Validate", () => service.ValidateBiologicalCompatibilityIntegrity()),
                ("Snapshot", () => service.ProveBiologicalCompatibilitySnapshotReadOnly()),
                ("Order", () => service.ProveBiologicalCompatibilityDeterministicOrder()));
            AddButtonRow(parent, font,
                ("Bleeding", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.Bleeding, BiologicalInteractionCategory.Hazard, string.Empty)),
                ("Suffocate", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.Suffocation, BiologicalInteractionCategory.Hazard, string.Empty)),
                ("Fracture", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.Fracture, BiologicalInteractionCategory.Injury, "part.leg.left")),
                ("Disrupt", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.IncorporealDisruption, BiologicalInteractionCategory.Injury, "essence.aura")));
            AddButtonRow(parent, font,
                ("Spirit", () => service.AssignBodySpecies("species.basic-spirit")),
                ("Construct", () => service.AssignBodySpecies("species.basic-construct")),
                ("Healing", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.BiologicalHealing, BiologicalInteractionCategory.Healing, string.Empty)),
                ("Repair", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.ConstructRepair, BiologicalInteractionCategory.Repair, string.Empty)));
            AddButtonRow(parent, font,
                ("Resist", () => service.AddBiologicalCompatibilityResistance()),
                ("2nd Resist", () => service.AddSecondBiologicalCompatibilityResistance()),
                ("Vulnerable", () => service.AddBiologicalCompatibilityVulnerability()),
                ("Remove", () => service.RemoveFirstBiologicalCompatibilityRule()));
            AddButtonRow(parent, font,
                ("Immune", () => service.AddBiologicalCompatibilityImmunity()),
                ("Suppress", () => service.AddBiologicalCompatibilitySuppression()),
                ("Affinity", () => service.AddBiologicalCompatibilityAffinity()),
                ("Convert", () => service.AddBiologicalCompatibilityConversion()));
            AddButtonRow(parent, font,
                ("Absorb", () => service.AddBiologicalCompatibilityAbsorption()),
                ("Disease", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.Disease, BiologicalInteractionCategory.Disease, string.Empty)),
                ("Poison", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.Poison, BiologicalInteractionCategory.Poison, string.Empty)),
                ("Polymorph", () => service.EvaluateBiologicalCompatibility(BiologicalInteractionIds.Polymorph, BiologicalInteractionCategory.Transformation, string.Empty)));
            biologicalCompatibilityText = AddText(parent, font, "Biological compatibility runtime not available.", 12, 760);
        }

        private void BuildBiologicalRecoverySection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.ResetBiologicalRecoveryHuman()),
                ("Validate", () => service.ValidateBiologicalRecoveryIntegrity()),
                ("Resting", () => service.SetBiologicalRecoveryRestContext(RecoveryRestType.Resting)),
                ("No Rest", () => service.SetBiologicalRecoveryRestContext(RecoveryRestType.NotResting)));
            AddButtonRow(parent, font,
                ("Lacerate", () => service.ApplyRecoveryLaceration()),
                ("Preview Wound", () => service.PreviewNaturalWoundClosureRecovery()),
                ("Start Wound", () => service.StartNaturalWoundClosureRecovery()),
                ("Preview Tick", () => service.PreviewBiologicalRecoveryTick(GetFloat(amountInput, 3600f))));
            AddButtonRow(parent, font,
                ("Tick", () => service.ApplyBiologicalRecoveryTick(GetFloat(amountInput, 3600f))),
                ("Repeat Tick", () => service.ReapplyBiologicalRecoveryTick(GetFloat(amountInput, 3600f))),
                ("Duplicate", () => service.ProveBiologicalRecoveryDuplicateTick()),
                ("Save/Load", () => service.ValidateBiologicalRecoverySaveRestore()));
            AddButtonRow(parent, font,
                ("Drain Blood", () => service.DrainRecoveryBlood()),
                ("Start Blood", () => service.StartNaturalBloodRecovery()),
                ("Repair Station", () => service.SetBiologicalRecoveryRestContext(RecoveryRestType.RepairStation)),
                ("Construct Repair", () => service.StartConstructRepairRecovery()));
            biologicalRecoveryText = AddText(parent, font, "Biological recovery runtime not available.", 12, 720);
        }

        private void BuildBodyTransformationSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.AssignBodySpecies("species.human")),
                ("Validate", () => service.ValidateBodyTransformationIntegrity()),
                ("Preview Poly", () => service.PreviewTemporaryPolymorphConstruct()),
                ("Execute Poly", () => service.ExecuteTemporaryPolymorphConstruct()));
            AddButtonRow(parent, font,
                ("Revert", () => service.RevertTemporaryPolymorph()),
                ("Construct", () => service.ExecutePermanentSpeciesChangeConstruct()),
                ("Human Again", () => service.ExecutePermanentSpeciesChangeHuman()),
                ("Preview Safe", () => service.ProveTransformationPreviewNoMutation()));
            AddButtonRow(parent, font,
                ("Body Replace", () => service.PreviewBodyReplacementPlan()),
                ("Body Swap", () => service.PreviewBodySwapPlan()),
                ("Possess", () => service.PreviewPossessionPlan()),
                ("Reincarnate", () => service.PreviewReincarnationPlan()));
            AddButtonRow(parent, font,
                ("Embodiment", () => service.PreviewSpiritEmbodimentPlan()),
                ("Structure", () => service.PreviewStructureReplacement()),
                ("Duplicate", () => service.ProveTransformationDuplicateProtection()),
                ("Save/Load", () => service.ValidateTransformationSaveRestore()));
            AddButtonRow(parent, font,
                ("Suppression", () => service.TestTransformationSuppression()));
            bodyTransformationText = AddText(parent, font, "Body transformation runtime not available.", 12, 720);
        }

        private void BuildBiologicalConditionsSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.AssignBodySpecies("species.human")),
                ("Validate", () => service.ValidateBiologicalConditionIntegrity()),
                ("Preview Viral", () => service.PreviewViralExposure()),
                ("Subthreshold", () => service.ApplySubthresholdViralExposure()));
            AddButtonRow(parent, font,
                ("Apply Viral", () => service.ApplyViralExposure()),
                ("Tick", () => service.ApplyBiologicalConditionTick(GetFloat(amountInput, 600f))),
                ("Duplicate Dose", () => service.ProveBiologicalConditionDuplicateExposure()),
                ("Duplicate Tick", () => service.ProveBiologicalConditionDuplicateTick()));
            AddButtonRow(parent, font,
                ("No-Wound Reject", () => service.RejectWoundInfectionWithoutWound()),
                ("Wound Infect", () => service.ApplyWoundInfection()),
                ("Poison", () => service.ApplyPoison()),
                ("Bad Venom", () => service.RejectVenomInvalidRoute()));
            AddButtonRow(parent, font,
                ("Venom", () => service.ApplyVenom()),
                ("Fever", () => service.ApplyFever()),
                ("Intoxicate", () => service.ApplyIntoxication()),
                ("Medicine", () => service.ApplyPrototypeMedicine()));
            AddButtonRow(parent, font,
                ("Antidote", () => service.ApplyPrototypeAntidote()),
                ("Transmit", () => service.PreviewConditionTransmission()),
                ("Spirit Reject", () => service.RejectSpiritOrdinaryDisease()),
                ("Construct Reject", () => service.RejectConstructOrdinaryPoison()));
            AddButtonRow(parent, font,
                ("Save/Load", () => service.ValidateBiologicalConditionSaveRestore()));
            biologicalConditionsText = AddText(parent, font, "Biological condition runtime not available.", 12, 820);
        }

        private void BuildBodyBiologyIntegrationSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Human", () => service.AssignBodySpecies("species.human")),
                ("Inspect", () => service.InspectBodyBiologyIntegration()),
                ("Validate", () => service.ValidateBodyBiologyIntegration()),
                ("Preview Tick", () => service.PreviewBodyBiologyAdvance(GetFloat(amountInput, 60f))));
            AddButtonRow(parent, font,
                ("Advance", () => service.AdvanceBodyBiology(GetFloat(amountInput, 60f))),
                ("Duplicate", () => service.ProveBodyBiologyAdvanceDuplicateProtection()),
                ("Fever", () => service.ApplyFever()),
                ("Recovery", () => service.StartNaturalWoundClosureRecovery()));
            bodyBiologyIntegrationText = AddText(parent, font, "Biological integration facade not available.", 12, 820);
        }

        private void BuildKnowledgeSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate", () => service.ValidateKnowledgeRuntime()),
                ("Preview Injury", () => service.PreviewKnowledgeVisibleInjury()),
                ("Record Injury", () => service.RecordKnowledgeVisibleInjury()),
                ("Duplicate", () => service.ProveKnowledgeDuplicateObservation()));
            AddButtonRow(parent, font,
                ("Weak Evidence", () => service.AddWeakKnowledgeEvidence()),
                ("Strong Evidence", () => service.AddStrongKnowledgeEvidence()),
                ("Oppose", () => service.AddOpposingKnowledgeEvidence()),
                ("Misconception", () => service.CreateKnowledgeMisconception()));
            AddButtonRow(parent, font,
                ("Correct", () => service.CorrectKnowledgeMisconception()),
                ("Mark Stale", () => service.MarkFirstKnowledgeStale()),
                ("Forget", () => service.ForgetFirstKnowledgeBelief()),
                ("Share", () => service.ShareFirstKnowledgeBelief()));
            AddButtonRow(parent, font,
                ("Save/Load", () => service.ValidateKnowledgeSaveRestore()),
                ("Reset", () => service.ResetKnowledgeFixture()));
            knowledgeText = AddText(parent, font, "Knowledge runtime not available.", 12, 820);
        }

        private void BuildObservationSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate", () => service.ValidateObservationFoundation()),
                ("Preview Visual", () => service.PreviewOrdinaryVisualObservation()),
                ("Record Visual", () => service.CommitOrdinaryVisualObservation()),
                ("Duplicate", () => service.ProveObservationDuplicateProtection()));
            AddButtonRow(parent, font,
                ("Medical Exam", () => service.CommitMedicalExaminationObservation()),
                ("Diagnose", () => service.DiagnoseBiologicalConditionFoundation()),
                ("Player Filter", () => service.ProvePlayerIrrelevantObservationNotTracked()),
                ("Dev No Mutate", () => service.ProveDevelopmentObserverDoesNotMutate()));
            AddButtonRow(parent, font,
                ("NPC Track", () => service.ProveNpcFullObservationTracks()),
                ("Remote Filter", () => service.ProveRemotePlayerIrrelevantObservationNotTracked()),
                ("Repeat Bound", () => service.ProveRepeatedObservationIsBounded()),
                ("Stale Reject", () => service.RejectStaleObservationProjection()));
            AddButtonRow(parent, font,
                ("Concealment", () => service.ProveConcealmentLowersObservationQuality()),
                ("Private Reject", () => service.RejectPrivateMedicalObservationWithoutAccess()),
                ("Inactive Reject", () => service.RejectInactiveFoundationObservationMethod()));
            observationText = AddText(parent, font, "Observation runtime not available.", 12, 820);
        }

        private void BuildCharacterHistorySection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate", () => service.ValidateHistoryFoundation()),
                ("Record Event", () => service.RecordAuthoritativeHistoryEvent()),
                ("Hidden Event", () => service.RecordHiddenHistoryEvent()),
                ("Privacy Check", () => service.ProveUninformedPersonCannotQueryHiddenHistory()));
            AddButtonRow(parent, font,
                ("Witness Memory", () => service.FormWitnessHistoryMemory()),
                ("Testimony", () => service.ShareHistoricalTestimony()),
                ("False Belief", () => service.CreateIncorrectHistoricalBelief()),
                ("Correct Event", () => service.CorrectAuthoritativeHistoryEvent()));
            AddButtonRow(parent, font,
                ("Revise Belief", () => service.ReviseHistoricalBeliefWithEvidence()),
                ("Forget Memory", () => service.ForgetFirstHistoryMemory()),
                ("Body Transition", () => service.RecordBodyTransitionHistory()),
                ("Compare Views", () => service.CompareHistoryKnowledgeMemoryViews()));
            AddButtonRow(parent, font,
                ("Save/Restore", () => service.ValidateHistorySaveRestore()));
            characterHistoryText = AddText(parent, font, "History runtime not available.", 12, 900);
        }

        private void BuildMemoryRecallSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate", () => service.ValidateMemory84()),
                ("Inspect", () => service.InspectExistingMemories()),
                ("Recall", () => service.RecallPrototypeMemory()),
                ("By Subject", () => service.RecallPrototypeMemoryBySubject()));
            AddButtonRow(parent, font,
                ("With Cue", () => service.RecallPrototypeMemoryWithCue()),
                ("Reinforce", () => service.ReinforcePrototypeMemory()),
                ("False Reinforce", () => service.ReinforceFalsePrototypeMemory()),
                ("Degrade Proof", () => service.ProveMemoryDegradationIdempotence()));
            AddButtonRow(parent, font,
                ("Reduce Clarity", () => service.ReduceMemoryClarity()),
                ("Reduce Conf", () => service.ReduceMemoryConfidence()),
                ("Difficult", () => service.MakeMemoryDifficult()),
                ("Partial Forget", () => service.PartialForgetPrototypeMemory()));
            AddButtonRow(parent, font,
                ("Forget Person", () => service.ForgetMemoryParticipant()));
            AddButtonRow(parent, font,
                ("Forget Time", () => service.ForgetMemoryTimeOrLocation()),
                ("Inaccessible", () => service.MakeMemoryInaccessible()),
                ("Forgotten", () => service.MarkMemoryForgotten()),
                ("Suppress", () => service.AddMemorySuppression()));
            AddButtonRow(parent, font,
                ("Stack Proof", () => service.ProveMemorySuppressionStacking()),
                ("Remove Supp", () => service.RemoveMemorySuppression()),
                ("Expire Supp", () => service.ExpireMemorySuppression()),
                ("Recover", () => service.RecoverPrototypeMemory()),
                ("Alter", () => service.AlterPrototypeMemory()));
            AddButtonRow(parent, font,
                ("Correct", () => service.CorrectAlteredMemory()),
                ("Revisions", () => service.ShowMemoryRevisionHistory()),
                ("Conflicts", () => service.CreateConflictingMemories()),
                ("Suppress Body", () => service.SuppressPreviousBodyAssociation()));
            AddButtonRow(parent, font,
                ("Recover Body", () => service.RecoverPreviousBodyAssociation()),
                ("Compare", () => service.CompareMemoryBeliefHistory()),
                ("Save/Restore", () => service.ValidateMemory84SaveRestore()));
            memoryRecallText = AddText(parent, font, "Memory runtime not available.", 12, 980);
        }

        private void BuildLifeEventsSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate", () => service.ValidateLifeEventDefinitions()),
                ("Birth", () => service.RecordLifeEventBirthOrCreation()),
                ("Discovery", () => service.RecordLifeEventDiscovery()),
                ("Role", () => service.RecordLifeEventRoleAppointment()));
            AddButtonRow(parent, font,
                ("Title", () => service.RecordLifeEventTitleGrant()),
                ("Affiliation", () => service.RecordLifeEventAffiliationChange()),
                ("Battle", () => service.RecordLifeEventBattleParticipation()),
                ("Injury", () => service.RecordLifeEventMajorInjury()));
            AddButtonRow(parent, font,
                ("Diagnosis", () => service.RecordLifeEventDiagnosis()),
                ("Recovery", () => service.RecordLifeEventRecovery()),
                ("Crime", () => service.RecordLifeEventCrimeOrAccusation()),
                ("Ownership", () => service.RecordLifeEventOwnershipTransfer()));
            AddButtonRow(parent, font,
                ("Death", () => service.RecordLifeEventDeath()),
                ("Presumed", () => service.RecordLifeEventPresumedDeath()),
                ("Return", () => service.RecordLifeEventReturn()),
                ("Body Tx", () => service.RecordLifeEventBodyTransition()));
            AddButtonRow(parent, font,
                ("Sequence", () => service.CreateLifeEventSequence()),
                ("Link Cause", () => service.LinkLifeEventCauseAndConsequence()),
                ("Correct", () => service.CorrectLifeEventPresumedDeath()),
                ("Save/Restore", () => service.ValidateLifeEventSaveRestore()));
            AddButtonRow(parent, font,
                ("Timeline", () => service.ShowLifeEventPersonTimeline()),
                ("Public Bio", () => service.ShowLifeEventPublicBiography()),
                ("Auth Bio", () => service.ShowLifeEventAuthoritativeBiography()),
                ("Known Bio", () => service.ShowLifeEventPersonKnownBiography()));
            AddButtonRow(parent, font,
                ("Memory Bio", () => service.ShowLifeEventPersonRememberedBiography()),
                ("Milestones", () => service.ShowLifeEventMajorMilestones()),
                ("Run 8.5 Auto", () => service.RunAutomationSuite("feature.8.5.character-history-life-events", automationStopOnFirstFailure)));
            lifeEventsText = AddText(parent, font, "Life event runtime not available.", 12, 980);
        }

        private void BuildInformationSourcesSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate Defs", () => service.ValidateInformationSourceDefinitions()),
                ("Direct Obs", () => service.RegisterDirectObservationSource()),
                ("Expert", () => service.RegisterExpertSource()),
                ("Testimony", () => service.RegisterTestimonySource()));
            AddButtonRow(parent, font,
                ("Anonymous", () => service.RegisterAnonymousSource()),
                ("Official", () => service.RegisterOfficialRecordSource()),
                ("Copy", () => service.CopySource()),
                ("Translate", () => service.TranslateSource()));
            AddButtonRow(parent, font,
                ("Summarize", () => service.SummarizeSource()),
                ("Evaluate", () => service.EvaluateReliability()),
                ("Compare People", () => service.CompareTwoPersonsSourceAssessments()),
                ("Trusted", () => service.MarkSourceTrusted()));
            AddButtonRow(parent, font,
                ("Untrusted", () => service.MarkSourceUntrusted()),
                ("Authority", () => service.AddSourceDomainAuthority()),
                ("Bias", () => service.AddSourceBias()),
                ("Error Risk", () => service.AddSourceErrorRisk()));
            AddButtonRow(parent, font,
                ("Deception", () => service.AddSourceDeceptionRisk()),
                ("Age", () => service.AgeSource()),
                ("Staleness", () => service.EvaluateSourceStaleness()),
                ("Trace", () => service.TraceSourceChain()));
            AddButtonRow(parent, font,
                ("Immediate/Orig", () => service.CompareImmediateAndOriginalSource()),
                ("Dependent", () => service.TestDependentReports()),
                ("Independent", () => service.TestIndependentCorroboration()),
                ("Correct", () => service.CorrectSourceAssessment()));
            AddButtonRow(parent, font,
                ("Hide Original", () => service.HideOriginalSource()),
                ("Raw/Effective", () => service.CompareRawAndEffectiveEvidenceStrength()),
                ("Save/Restore", () => service.ValidateInformationSourceSaveRestore()),
                ("Run 8.6 Auto", () => service.RunAutomationSuite("feature.8.6.information-sources-reliability", automationStopOnFirstFailure)));
            informationSourcesText = AddText(parent, font, "Information Source runtime not available.", 12, 980);
        }

        private void BuildInformationSharingSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate Defs", () => service.ValidateInformationTransferDefinitions()),
                ("True Fact", () => service.ShareKnownTrueFact()),
                ("False Belief", () => service.ShareSincereFalseBelief()),
                ("Partial Recall", () => service.SharePartiallyRecalledEvent()));
            AddButtonRow(parent, font,
                ("Suppressed", () => service.AttemptSuppressedMemoryTransfer()),
                ("Direct Obs", () => service.ShareDirectObservation()),
                ("Expert", () => service.ShareExpertDiagnosis()),
                ("Confidence", () => service.CompareInheritedConfidenceByDomain()));
            AddButtonRow(parent, font,
                ("Anonymous", () => service.ShareAnonymousInformation()),
                ("Official", () => service.ReadOfficialRecord()),
                ("Summarize", () => service.CopyAndSummarizeTransferSource()),
                ("Lineage", () => service.TraceTransferSourceLineage()));
            AddButtonRow(parent, font,
                ("Teach Concept", () => service.TeachSemanticConcept()),
                ("Teach Procedure", () => service.TeachProcedureReference()),
                ("Demo", () => service.DemonstrateProcedure()),
                ("No Prereq", () => service.TeachWithoutPrerequisites()));
            AddButtonRow(parent, font,
                ("Clarify", () => service.ClarifyTransfer()),
                ("Reshare", () => service.ReshareTransfer()),
                ("Distort", () => service.ReshareDistortedVersion()),
                ("Omit", () => service.DeliberatelyOmitDetail()));
            AddButtonRow(parent, font,
                ("Correct", () => service.CorrectPriorTransfer()),
                ("Privacy", () => service.CreatePublicPrivateRestrictedTransfers()),
                ("Save/Restore", () => service.ValidateInformationTransferSaveRestore()),
                ("Run 8.7 Auto", () => service.RunAutomationSuite("feature.8.7.information-sharing-teaching", automationStopOnFirstFailure)));
            informationSharingText = AddText(parent, font, "Information Sharing runtime not available.", 12, 980);
        }

        private void BuildInformationAccessSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate Defs", () => service.ValidateInformationAccessDefinitions()),
                ("Public", () => service.CreatePublicInformationAccess()),
                ("Secret Deny", () => service.CreatePrivateInformationAccess()),
                ("Grant Inspect", () => service.GrantInspectInformationAccess()));
            AddButtonRow(parent, font,
                ("Grant Share", () => service.GrantShareInformationAccess()),
                ("No Reshare", () => service.AttemptNoReshareInformationAccess()),
                ("Hide Source", () => service.ProtectInformationSourceIdentity()),
                ("Reveal Source", () => service.RevealInformationSourceIdentity()));
            AddButtonRow(parent, font,
                ("Hide Existence", () => service.HideSecretExistence()),
                ("Reveal Exist", () => service.RevealSecretExistence()),
                ("Discover", () => service.DiscoverHiddenInformationAccess()),
                ("Declassify", () => service.DeclassifyInformationAccess()));
            AddButtonRow(parent, font,
                ("Audit Deny", () => service.AttemptUnauthorizedInformationAccess()),
                ("Projection", () => service.CompareInformationAccessProjections()),
                ("Adapters", () => service.ValidateInformationAccessProjectionAdapters()),
                ("Save/Restore", () => service.ValidateInformationAccessSaveRestore()),
                ("Run 8.8 Auto", () => service.RunAutomationSuite("feature.8.8.secrets-visibility-information-access", automationStopOnFirstFailure)));
            informationAccessText = AddText(parent, font, "Information Access runtime not available.", 12, 980);
        }

        private void BuildKnowledgeRecordsSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Validate Defs", () => service.ValidateKnowledgeRecordDefinitions()),
                ("Journal", () => service.CreatePersonalJournalRecord()),
                ("History", () => service.CreateHistoricalArchiveRecord()),
                ("Biography", () => service.CreateBiographyProjectionRecord()));
            AddButtonRow(parent, font,
                ("Bestiary", () => service.CreateBestiaryRecord()),
                ("Location", () => service.CreateLocationRecord()),
                ("Medical", () => service.CreateMedicalRecord()),
                ("Investigation", () => service.CreateInvestigationRecord()));
            AddButtonRow(parent, font,
                ("Correct", () => service.CreateCorrectedKnowledgeRecord()),
                ("Read Owner", () => service.ReadKnowledgeRecordAsOwner()),
                ("Deny Read", () => service.AttemptUnauthorizedKnowledgeRecordRead()),
                ("Search", () => service.SearchKnowledgeRecords()));
            AddButtonRow(parent, font,
                ("Collection", () => service.CreateKnowledgeRecordCollection()),
                ("Projection", () => service.ValidateKnowledgeRecordLiveProjectionBoundaries()),
                ("Save/Restore", () => service.ValidateKnowledgeRecordSaveRestore()),
                ("Run 8.9 Auto", () => service.RunAutomationSuite("feature.8.9.historical-records-journals-codex", automationStopOnFirstFailure)));
            knowledgeRecordsText = AddText(parent, font, "Knowledge Record runtime not available.", 12, 980);
        }

        private void BuildKnowledgeHistoryIntegrationSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Prepare", () => service.PrepareKnowledgeHistoryIntegrationFixtures()),
                ("Readiness", () => service.ValidateKnowledgeHistoryReadiness()),
                ("Validate", () => service.ValidateKnowledgeHistoryIntegration()),
                ("Definition Coverage", () => service.ShowKnowledgeHistoryDefinitionCoverage()));
            AddButtonRow(parent, font,
                ("Save Graph", () => service.ValidateKnowledgeHistorySaveCapture()),
                ("Discovery", () => service.RunKnowledgeHistoryDiscoveryFlow()),
                ("Event Memory", () => service.RunKnowledgeHistoryEventMemoryFlow()),
                ("Record Read", () => service.RunKnowledgeHistoryRecordReadingFlow()));
            AddButtonRow(parent, font,
                ("Access", () => service.RunKnowledgeHistoryAccessProjectionFlow()),
                ("Step 9 API", () => service.PreviewStep9KnowledgeContracts()),
                ("Run 8.10 Auto", () => service.RunAutomationSuite("feature.8.10.knowledge-history-integration", automationStopOnFirstFailure)));
            knowledgeHistoryIntegrationText = AddText(parent, font, "Knowledge and History Integration runtime not available.", 12, 980);
        }

        private void BuildIdentityProgressionSection(Transform parent, Font font)
        {
            roleValueText = AddSelectorRow(parent, font, "Role", () => CycleSelection(ref selectedRoleIndex, roles.Count, -1), () => CycleSelection(ref selectedRoleIndex, roles.Count, 1));
            socialStatusValueText = AddSelectorRow(parent, font, "Social", () => CycleSelection(ref selectedSocialStatusIndex, socialStatuses.Count, -1), () => CycleSelection(ref selectedSocialStatusIndex, socialStatuses.Count, 1));
            currencyValueText = AddSelectorRow(parent, font, "Currency", () => CycleSelection(ref selectedCurrencyIndex, currencies.Count, -1), () => CycleSelection(ref selectedCurrencyIndex, currencies.Count, 1));
            AddButtonRow(parent, font,
                ("Validate IDs", () => service.ValidateIdentityProgression()),
                ("Generate Origin", () => service.GenerateOrigin(GetInt(amountInput, 0))),
                ("Duplicate Proof", () => service.ProveOriginAssignmentIsOnceOnly()),
                ("Reset Identity", () => service.ResetIdentityProgression(confirmed: false)));
            AddButtonRow(parent, font,
                ("Advance Gift", () => service.AdvanceBirthGiftProgress(GetFloat(amountInput, 300f))),
                ("Awaken Gift", () => service.ForceBirthGiftAwakening()));
            AddButtonRow(parent, font,
                ("Add Role", () => service.AddRole(GetSelected(roles, selectedRoleIndex), acceptConflicts: false)),
                ("Accept Conflict", () => service.AddRole(GetSelected(roles, selectedRoleIndex), acceptConflicts: true)),
                ("Suspend Role", () => service.SuspendFirstActiveRole()),
                ("Revoke Role", () => service.RevokeFirstActiveRole()),
                ("Abandon Role", () => service.AbandonFirstActiveRole()));
            AddButtonRow(parent, font,
                ("Add Global Status", () => service.AddGlobalSocialStatus(GetSelected(socialStatuses, selectedSocialStatusIndex))),
                ("Add Place Status", () => service.AddPlaceSocialStatus(GetSelected(socialStatuses, selectedSocialStatusIndex), GetSelected(places, selectedPlaceIndex))),
                ("Resolve Status", () => service.ResolveFirstActiveSocialStatus()));
            AddButtonRow(parent, font,
                ("Add Money", () => service.AddCurrency(GetSelected(currencies, selectedCurrencyIndex), GetInt(amountInput, 25))),
                ("Spend Money", () => service.SpendCurrency(GetSelected(currencies, selectedCurrencyIndex), GetInt(amountInput, 25))));
            AddButtonRow(parent, font,
                ("Record Success", () => service.RecordSuccessfulActivity(GetFloat(amountInput, 0.5f))),
                ("Record Failure", () => service.RecordFailedActivity(GetFloat(amountInput, 0.5f))),
                ("Participation", () => service.RecordParticipation()));
            identityProgressionText = AddText(parent, font, "Identity/progression not available.", 12, 320);
        }

        private void BuildFeature52Section(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Base +Str", () => service.AddStrengthTraining()),
                ("Base +All", () => service.AddBalancedAttributeTraining()),
                ("Strength 100+", () => service.SetStrengthAboveHundred()),
                ("Invalid Base", () => service.AttemptInvalidAttributeGrowth()));
            AddButtonRow(parent, font,
                ("Add Phys Power", () => service.AddPhysicalPowerFlat()),
                ("Defense Penalty", () => service.AddPhysicalDefensePenalty()),
                ("Clear 5.4a", () => service.ClearFeature52Contributions()),
                ("Rebuild Stats", () => service.RecalculateFeature52Stats()));
            attributesCalculatedStatsText = AddText(parent, font, "Base Attributes and Calculated Stats not available.", 12, 360);
        }

        private void BuildFeature53Section(Transform parent, Font font)
        {
            skillValueText = AddSelectorRow(parent, font, "Skill", () => CycleSelection(ref selectedSkillIndex, skills.Count, -1), () => CycleSelection(ref selectedSkillIndex, skills.Count, 1));
            AddButtonRow(parent, font,
                ("Valid Use", () => service.SimulateSkillAction(GetSelected(skills, selectedSkillIndex), executed: true, succeeded: true)),
                ("Missed Use", () => service.SimulateSkillAction(GetSelected(skills, selectedSkillIndex), executed: true, succeeded: false)),
                ("Blocked", () => service.SimulateSkillAction(GetSelected(skills, selectedSkillIndex), executed: false, succeeded: false)));
            AddButtonRow(parent, font,
                ("Duplicate", () => service.TestDuplicateSkillAction(GetSelected(skills, selectedSkillIndex))),
                ("Many Uses", () => service.SimulateManySkillActions(GetSelected(skills, selectedSkillIndex), GetInt(quantityInput, 25))),
                ("Grant F", () => service.GrantSkill(GetSelected(skills, selectedSkillIndex), SkillGrade.F)));
            AddButtonRow(parent, font,
                ("Grant C", () => service.GrantSkill(GetSelected(skills, selectedSkillIndex), SkillGrade.C)),
                ("Grant A", () => service.GrantSkill(GetSelected(skills, selectedSkillIndex), SkillGrade.A)),
                ("Grant AAA", () => service.GrantSkill(GetSelected(skills, selectedSkillIndex), SkillGrade.AAA)));
            AddButtonRow(parent, font,
                ("XP +1", () => service.AwardSkillXp(GetSelected(skills, selectedSkillIndex), 1)),
                ("XP Many", () => service.AwardSkillXp(GetSelected(skills, selectedSkillIndex), GetInt(quantityInput, 25))),
                ("Rebuild", () => service.RebuildSkillEffects()),
                ("Clear Skills", () => service.ClearSkillDevelopmentState(confirmed: false)));
            skillsText = AddText(parent, font, "Skills not available.", 12, 360);
        }

        private void BuildFeature55Section(Transform parent, Font font)
        {
            traitValueText = AddSelectorRow(parent, font, "Trait", () => CycleSelection(ref selectedTraitIndex, traits.Count, -1), () => CycleSelection(ref selectedTraitIndex, traits.Count, 1));
            requirementValueText = AddSelectorRow(parent, font, "Requirement", () => CycleSelection(ref selectedRequirementIndex, requirements.Count, -1), () => CycleSelection(ref selectedRequirementIndex, requirements.Count, 1));
            AddButtonRow(parent, font,
                ("Grant Active", () => service.GrantTrait(GetSelected(traits, selectedTraitIndex), TraitLifecycleState.Active, TraitDiscoveryState.Discovered)),
                ("Grant Dormant", () => service.GrantTrait(GetSelected(traits, selectedTraitIndex), TraitLifecycleState.Dormant, TraitDiscoveryState.Undiscovered)),
                ("Duplicate", () => service.GrantTraitDuplicateProof(GetSelected(traits, selectedTraitIndex))),
                ("Second Source", () => service.GrantTraitSecondSource(GetSelected(traits, selectedTraitIndex))));
            AddButtonRow(parent, font,
                ("Remove Source", () => service.RemoveTraitTestLabSource(GetSelected(traits, selectedTraitIndex))),
                ("Activate", () => service.ActivateTrait(GetSelected(traits, selectedTraitIndex))),
                ("Suppress", () => service.SuppressTrait(GetSelected(traits, selectedTraitIndex))),
                ("Unsuppress", () => service.UnsuppressTrait(GetSelected(traits, selectedTraitIndex))));
            AddButtonRow(parent, font,
                ("Suspect", () => service.SetTraitSuspected(GetSelected(traits, selectedTraitIndex))),
                ("Discover", () => service.SetTraitDiscovered(GetSelected(traits, selectedTraitIndex))),
                ("Replace", () => service.ReplaceTrait(GetSelected(traits, selectedTraitIndex))),
                ("Rebuild", () => service.RebuildTraitEffects()));
            AddButtonRow(parent, font,
                ("Evaluate Req", () => service.EvaluateRequirement(GetSelected(requirements, selectedRequirementIndex))),
                ("Save Snapshot", () => service.SnapshotTraitsForPersistence()));
            traitsText = AddText(parent, font, "Traits not available.", 12, 380);
        }

        private void BuildFeature54bSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Reconcile", () => service.ReconcileResources()),
                ("Duplicate Event", () => service.ProveResourceDuplicateEvent()),
                ("Regen Tick", () => service.TickResourceRegeneration()),
                ("Save Snapshot", () => service.SnapshotResourcesForPersistence()));
            AddButtonRow(parent, font,
                ("Damage Health", () => service.DamagePlayer(GetInt(amountInput, 25))),
                ("Heal Health", () => service.HealPlayer(GetInt(amountInput, 25))),
                ("Drain Mana", () => service.DrainMana(GetFloat(amountInput, 25f))),
                ("Drain Stamina", () => service.DrainStamina(GetFloat(amountInput, 25f))));
            resourcesText = AddText(parent, font, "Current Resources not available.", 12, 360);
        }

        private void BuildInventorySection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Grant Item", () => service.GrantItem(GetSelected(items, selectedItemIndex), GetInt(quantityInput, 1))),
                ("Grant Stateful", () => service.GrantStatefulItem(GetSelected(items, selectedItemIndex))),
                ("Remove Item", () => service.RemoveItem(GetSelected(items, selectedItemIndex), GetInt(quantityInput, 1))));
            AddButtonRow(parent, font,
                ("Fill Inventory", () => service.FillInventory(GetSelected(items, selectedItemIndex))),
                ("Equip Selected", () => service.EquipFirstCompatible(GetSelected(items, selectedItemIndex))),
                ("Unequip All", () => service.UnequipAll(confirmed: false)),
                ("Clear Inventory", () => service.ClearInventory(confirmed: false)));
        }

        private void BuildCombatSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.2 Attack Resolution");
            attackHitChanceInput = AddInputRow(parent, font, "Hit Chance", "0.75");
            attackHitRollInput = AddInputRow(parent, font, "Hit Roll", "0.10");
            attackCriticalChanceInput = AddInputRow(parent, font, "Crit Chance", "0.25");
            attackCriticalRollInput = AddInputRow(parent, font, "Crit Roll", "0.10");
            attackCriticalMultiplierInput = AddInputRow(parent, font, "Crit Mult", "1.5");
            attackDistanceInput = AddInputRow(parent, font, "Distance", "1");
            attackMaximumRangeInput = AddInputRow(parent, font, "Max Range", "2");
            AddButtonRow(parent, font,
                ("Fresh Tx", () => service.GenerateAttackTransaction()),
                ("Preview P>E", () => service.PreviewAttackResolution(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalChanceInput, 0.25f), GetFloat(attackCriticalRollInput, 0.1f), GetFloat(attackCriticalMultiplierInput, 1.5f), GetFloat(attackDistanceInput, 1f), GetFloat(attackMaximumRangeInput, 2f), targetEnemy: true, sourcePlayer: true)),
                ("Execute P>E", () => service.ExecuteAttackResolution(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalChanceInput, 0.25f), GetFloat(attackCriticalRollInput, 0.1f), GetFloat(attackCriticalMultiplierInput, 1.5f), GetFloat(attackDistanceInput, 1f), GetFloat(attackMaximumRangeInput, 2f), targetEnemy: true, sourcePlayer: true, reuseTransaction: false)),
                ("Reuse Tx", () => service.ExecuteAttackResolution(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalChanceInput, 0.25f), GetFloat(attackCriticalRollInput, 0.1f), GetFloat(attackCriticalMultiplierInput, 1.5f), GetFloat(attackDistanceInput, 1f), GetFloat(attackMaximumRangeInput, 2f), targetEnemy: true, sourcePlayer: true, reuseTransaction: true)));
            AddButtonRow(parent, font,
                ("Preview E>P", () => service.PreviewAttackResolution(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalChanceInput, 0.25f), GetFloat(attackCriticalRollInput, 0.1f), GetFloat(attackCriticalMultiplierInput, 1.5f), GetFloat(attackDistanceInput, 1f), GetFloat(attackMaximumRangeInput, 2f), targetEnemy: false, sourcePlayer: false)),
                ("Execute E>P", () => service.ExecuteAttackResolution(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalChanceInput, 0.25f), GetFloat(attackCriticalRollInput, 0.1f), GetFloat(attackCriticalMultiplierInput, 1.5f), GetFloat(attackDistanceInput, 1f), GetFloat(attackMaximumRangeInput, 2f), targetEnemy: false, sourcePlayer: false, reuseTransaction: false)),
                ("Env>P", () => service.ExecuteEnvironmentalAttack(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitRollInput, 0.1f))),
                ("Out Range", () => service.PreviewAttackResolution(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), 0.95f, 0.1f, 0f, 0.5f, 1.5f, 999f, 1f, targetEnemy: true, sourcePlayer: true)));
            AddHeader(parent, font, "Feature 6.1 Damage/Healing");
            AddButtonRow(parent, font,
                ("Preview 6.1", () => service.PreviewPipelineDamage(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), targetPlayer: true)),
                ("Damage 6.1", () => service.ApplyPipelineDamage(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), targetPlayer: true)),
                ("Heal 6.1", () => service.ApplyPipelineHealing(GetFloat(amountInput, 25f), targetPlayer: true)),
                ("Duplicate 6.1", () => service.ProvePipelineDuplicate(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f))));
            AddButtonRow(parent, font,
                ("Damage Enemy", () => service.ApplyTypedDamage(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), targetEnemy: true, sourcePlayer: true)),
                ("Damage Player", () => service.ApplyTypedDamage(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), targetEnemy: false, sourcePlayer: false)),
                ("Defeat Enemy", () => service.DefeatEnemy(GetSelected(damageTypes, selectedDamageIndex))),
                ("Reset Enemy", () => service.ResetEnemy()));
        }

        private void BuildDefensiveActionsSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.6 Defensive Actions / Reactive Combat");
            AddButtonRow(parent, font,
                ("Preview P", () => service.PreviewDefenseActivation(GetSelected(defensiveActions, selectedDefenseIndex), targetPlayer: true)),
                ("Activate P", () => service.ActivateDefense(GetSelected(defensiveActions, selectedDefenseIndex), targetPlayer: true, reuseTransaction: false)),
                ("Reuse P", () => service.ActivateDefense(GetSelected(defensiveActions, selectedDefenseIndex), targetPlayer: true, reuseTransaction: true)),
                ("Cancel P", () => service.CancelDefense(targetPlayer: true)));
            AddButtonRow(parent, font,
                ("Preview E", () => service.PreviewDefenseActivation(GetSelected(defensiveActions, selectedDefenseIndex), targetPlayer: false)),
                ("Activate E", () => service.ActivateDefense(GetSelected(defensiveActions, selectedDefenseIndex), targetPlayer: false, reuseTransaction: false)),
                ("Reuse E", () => service.ActivateDefense(GetSelected(defensiveActions, selectedDefenseIndex), targetPlayer: false, reuseTransaction: true)),
                ("Cancel E", () => service.CancelDefense(targetPlayer: false)));
            AddButtonRow(parent, font,
                ("Preview E>P", () => service.PreviewDefensiveAttack(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalRollInput, 0.1f), targetPlayer: true)),
                ("Execute E>P", () => service.ExecuteDefensiveAttack(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalRollInput, 0.1f), targetPlayer: true, reuseTransaction: false)),
                ("Reuse E>P", () => service.ExecuteDefensiveAttack(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalRollInput, 0.1f), targetPlayer: true, reuseTransaction: true)));
            AddButtonRow(parent, font,
                ("Preview P>E", () => service.PreviewDefensiveAttack(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalRollInput, 0.1f), targetPlayer: false)),
                ("Execute P>E", () => service.ExecuteDefensiveAttack(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalRollInput, 0.1f), targetPlayer: false, reuseTransaction: false)),
                ("Reuse P>E", () => service.ExecuteDefensiveAttack(GetSelected(damageTypes, selectedDamageIndex), GetFloat(amountInput, 25f), GetFloat(attackHitChanceInput, 0.75f), GetFloat(attackHitRollInput, 0.1f), GetFloat(attackCriticalRollInput, 0.1f), targetPlayer: false, reuseTransaction: true)));
            defensiveActionsText = AddText(parent, font, "Defensive actions not available.", 12, 320);
        }

        private void BuildCombatExecutionSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.7 Combat Costs / Cooldowns / Commitments");
            combatExecutionValueText = AddSelectorRow(parent, font, "Execution", () => CycleSelection(ref selectedCombatExecutionIndex, combatExecutions.Count, -1), () => CycleSelection(ref selectedCombatExecutionIndex, combatExecutions.Count, 1));
            AddButtonRow(parent, font,
                ("Preview", () => service.PreviewCombatExecution(GetSelected(combatExecutions, selectedCombatExecutionIndex))),
                ("Begin", () => service.BeginCombatExecution(GetSelected(combatExecutions, selectedCombatExecutionIndex), reuseTransaction: false)),
                ("Reuse Begin", () => service.BeginCombatExecution(GetSelected(combatExecutions, selectedCombatExecutionIndex), reuseTransaction: true)),
                ("Commit", () => service.CommitCombatExecution(reuseTransaction: false)));
            AddButtonRow(parent, font,
                ("Reuse Commit", () => service.CommitCombatExecution(reuseTransaction: true)),
                ("Cancel", () => service.CancelCombatExecution()),
                ("Interrupt", () => service.InterruptCombatExecution()),
                ("Advance", () => service.AdvanceCombatExecutionClock(GetFloat(amountInput, 1f))));
            AddButtonRow(parent, font,
                ("Restore Clear", () => service.ClearCombatExecutionForRestore()),
                ("Snapshot", () => service.SnapshotCombatExecution()));
            combatExecutionText = AddText(parent, font, "Combat execution not available.", 12, 360);
        }

        private void BuildCombatReactionSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.8 Combat Effects / Triggers / Reactions");
            combatReactionValueText = AddSelectorRow(parent, font, "Reaction", () => CycleSelection(ref selectedCombatReactionIndex, combatReactions.Count, -1), () => CycleSelection(ref selectedCombatReactionIndex, combatReactions.Count, 1));
            AddButtonRow(parent, font,
                ("Register Player", () => service.RegisterCombatReaction(GetSelected(combatReactions, selectedCombatReactionIndex), ownerPlayer: true)),
                ("Register Enemy", () => service.RegisterCombatReaction(GetSelected(combatReactions, selectedCombatReactionIndex), ownerPlayer: false)),
                ("Clear", () => service.ClearCombatReactions()));
            AddButtonRow(parent, font,
                ("Preview Damage", () => service.PreviewCombatReactionDamage(GetSelected(combatReactions, selectedCombatReactionIndex))),
                ("Execute Damage", () => service.ExecuteCombatReactionDamage(GetSelected(combatReactions, selectedCombatReactionIndex))),
                ("Execute Critical", () => service.ExecuteCombatReactionCritical(GetSelected(combatReactions, selectedCombatReactionIndex))),
                ("Duplicate Proof", () => service.ExecuteCombatReactionDuplicateProof(GetSelected(combatReactions, selectedCombatReactionIndex))));
            combatReactionText = AddText(parent, font, "Combat reactions not available.", 12, 360);
        }

        private void BuildCombatContributionSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.9 Combat Contribution / Credit / Reward Hooks");
            AddButtonRow(parent, font,
                ("Preview", () => service.PreviewContribution(GetSelected(damageTypes, selectedDamageIndex))),
                ("Record Damage", () => service.RecordDamageContribution(GetSelected(damageTypes, selectedDamageIndex), reuseTransaction: false)),
                ("Reuse Damage", () => service.RecordDamageContribution(GetSelected(damageTypes, selectedDamageIndex), reuseTransaction: true)),
                ("Record Healing", () => service.RecordHealingContribution(reuseTransaction: false)));
            AddButtonRow(parent, font,
                ("Reuse Healing", () => service.RecordHealingContribution(reuseTransaction: true)),
                ("Prevented", () => service.RecordFullyPreventedDamageContribution(GetSelected(damageTypes, selectedDamageIndex))),
                ("Overkill", () => service.RecordOverkillContribution(GetSelected(damageTypes, selectedDamageIndex))),
                ("Ongoing", () => service.RecordOngoingDamageContribution()));
            AddButtonRow(parent, font,
                ("React Dmg", () => service.RecordReactionDamageContribution()),
                ("React Heal", () => service.RecordReactionHealingContribution()),
                ("Block", () => service.RecordDefenseContribution(CombatContributionType.SuccessfulBlock)),
                ("Parry", () => service.RecordDefenseContribution(CombatContributionType.SuccessfulParry)));
            AddButtonRow(parent, font,
                ("Dodge", () => service.RecordDefenseContribution(CombatContributionType.SuccessfulDodge)),
                ("Advance", () => service.AdvanceContributionClock(GetFloat(amountInput, 30f))),
                ("Defeat Credit", () => service.ResolveDefeatContributionCredit()),
                ("Kill Credit", () => service.ResolveKillContributionCredit()));
            AddButtonRow(parent, font,
                ("Finalize", () => service.FinalizeContributionLedger()),
                ("Clear", () => service.ClearCombatContributions()));
            combatContributionText = AddText(parent, font, "Combat contribution not available.", 12, 360);
        }

        private void BuildCombatRuntimeSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.10 Combat System Integration");
            AddButtonRow(parent, font,
                ("Refresh", Refresh),
                ("Validate", () => service.ValidateCombatRuntimeIntegrity()),
                ("Reset", () => service.ResetCombatRuntimeIntegration()),
                ("Restore Clear", () => service.SimulateCombatRuntimeRestoreClear()));
            AddButtonRow(parent, font,
                ("Preview Hit", () => service.PreviewCombatRuntimeAttack(GetSelected(damageTypes, selectedDamageIndex))),
                ("Hit", () => service.ExecuteCombatRuntimeAttack(GetSelected(damageTypes, selectedDamageIndex))),
                ("Miss", () => service.ExecuteCombatRuntimeMiss(GetSelected(damageTypes, selectedDamageIndex))),
                ("Critical", () => service.ExecuteCombatRuntimeCritical(GetSelected(damageTypes, selectedDamageIndex))));
            AddButtonRow(parent, font,
                ("Dodge Flow", () => service.ExecuteCombatRuntimeDefense(GetSelected(damageTypes, selectedDamageIndex), block: false)),
                ("Block Flow", () => service.ExecuteCombatRuntimeDefense(GetSelected(damageTypes, selectedDamageIndex), block: true)),
                ("Ongoing Tick", () => service.ExecuteCombatRuntimeOngoingDamage(GetSelected(ongoingEffects, selectedOngoingEffectIndex), GetSelected(damageTypes, selectedDamageIndex))),
                ("Reaction", () => service.ExecuteCombatRuntimeReaction(GetSelected(combatReactions, selectedCombatReactionIndex))));
            AddButtonRow(parent, font,
                ("Contribution", () => service.ExecuteCombatRuntimeContribution(GetSelected(damageTypes, selectedDamageIndex))),
                ("Engage", () => service.ExecuteExplicitCombatEngagement(reuseTransaction: false)),
                ("Merge/Split", () => service.ProveContributionEncounterSplit()));
            combatRuntimeText = AddText(parent, font, "Combat runtime integration not available.", 12, 480);
        }

        private void BuildCombatStateSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.5 Combat State / Engagements / Encounters");
            AddButtonRow(parent, font,
                ("Fresh Tx", () => service.GenerateCombatStateTransaction()),
                ("Preview", () => service.PreviewExplicitCombatEngagement()),
                ("Engage", () => service.ExecuteExplicitCombatEngagement(reuseTransaction: false)),
                ("Reuse", () => service.ExecuteExplicitCombatEngagement(reuseTransaction: true)));
            AddButtonRow(parent, font,
                ("Miss", () => service.ExecuteCombatStateAttack(GetSelected(damageTypes, selectedDamageIndex), miss: true, blocked: false, prevented: false)),
                ("Hit", () => service.ExecuteCombatStateAttack(GetSelected(damageTypes, selectedDamageIndex), miss: false, blocked: false, prevented: false)),
                ("Blocked", () => service.ExecuteCombatStateAttack(GetSelected(damageTypes, selectedDamageIndex), miss: false, blocked: true, prevented: false)),
                ("Prevented", () => service.ExecuteCombatStateAttack(GetSelected(damageTypes, selectedDamageIndex), miss: false, blocked: false, prevented: true)));
            AddButtonRow(parent, font,
                ("Advance <T", () => service.AdvanceCombatState(9.5f)),
                ("Advance T", () => service.AdvanceCombatState(0.5f)),
                ("Advance 10s", () => service.AdvanceCombatState(10f)),
                ("End Encounter", () => service.EndCurrentCombatEncounter()));
            AddButtonRow(parent, font,
                ("Exit Player", () => service.ForceCombatExit(targetEnemy: false)),
                ("Exit Enemy", () => service.ForceCombatExit(targetEnemy: true)),
                ("Kill Enemy", () => service.ExecuteDeathLifecycle(targetEnemy: true, reuseTransaction: false)),
                ("Revive Enemy", () => service.ExecuteRevivalLifecycle(targetEnemy: true, GetFloat(amountInput, 25f), reuseTransaction: false)));
            AddButtonRow(parent, font,
                ("Prep A-D", () => service.PrepareCombatStateSplitParticipants()),
                ("A-B", () => service.EngageCombatStateParticipants("A", "B")),
                ("B-C", () => service.EngageCombatStateParticipants("B", "C")),
                ("C-D", () => service.EngageCombatStateParticipants("C", "D")));
            AddButtonRow(parent, font,
                ("End B-C", () => service.EndCombatStateEngagement("B", "C", reuseTransaction: false)),
                ("Reuse Split", () => service.EndCombatStateEngagement("B", "C", reuseTransaction: true)),
                ("Process Graph", () => service.ProcessCombatStateConnectivity()),
                ("Validate", () => service.ValidateCombatStateIntegrity()));
            AddButtonRow(parent, font,
                ("Exit B", () => service.ForceCombatStateParticipantExit("B")),
                ("Exit C", () => service.ForceCombatStateParticipantExit("C")),
                ("Kill B", () => service.KillCombatStateParticipant("B")),
                ("Kill C", () => service.KillCombatStateParticipant("C")));
            combatStateText = AddText(parent, font, "Combat state not available.", 12, 320);
        }

        private void BuildLifecycleSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.3 Defeat / Recovery / Death / Revival");
            AddButtonRow(parent, font,
                ("Fresh Tx", () => service.GenerateLifecycleTransaction()),
                ("Zero P", () => service.ApplyZeroHealthLifecycleDamage(GetSelected(damageTypes, selectedDamageIndex), targetEnemy: false)),
                ("Zero E", () => service.ApplyZeroHealthLifecycleDamage(GetSelected(damageTypes, selectedDamageIndex), targetEnemy: true)),
                ("Reuse Defeat P", () => service.ExecuteDefeatLifecycle(targetEnemy: false, reuseTransaction: true)));
            AddButtonRow(parent, font,
                ("Preview Defeat P", () => service.PreviewDefeatLifecycle(targetEnemy: false)),
                ("Defeat P", () => service.ExecuteDefeatLifecycle(targetEnemy: false, reuseTransaction: false)),
                ("Preview Defeat E", () => service.PreviewDefeatLifecycle(targetEnemy: true)),
                ("Defeat E", () => service.ExecuteDefeatLifecycle(targetEnemy: true, reuseTransaction: false)));
            AddButtonRow(parent, font,
                ("Preview Recover P", () => service.PreviewRecoveryLifecycle(false, GetFloat(amountInput, 25f))),
                ("Recover P", () => service.ExecuteRecoveryLifecycle(false, GetFloat(amountInput, 25f), reuseTransaction: false)),
                ("Preview Recover E", () => service.PreviewRecoveryLifecycle(true, GetFloat(amountInput, 25f))),
                ("Recover E", () => service.ExecuteRecoveryLifecycle(true, GetFloat(amountInput, 25f), reuseTransaction: false)));
            AddButtonRow(parent, font,
                ("Preview Death P", () => service.PreviewDeathLifecycle(targetEnemy: false)),
                ("Kill P", () => service.ExecuteDeathLifecycle(targetEnemy: false, reuseTransaction: false)),
                ("Preview Death E", () => service.PreviewDeathLifecycle(targetEnemy: true)),
                ("Kill E", () => service.ExecuteDeathLifecycle(targetEnemy: true, reuseTransaction: false)));
            AddButtonRow(parent, font,
                ("Preview Revive P", () => service.PreviewRevivalLifecycle(false, GetFloat(amountInput, 25f))),
                ("Revive P", () => service.ExecuteRevivalLifecycle(false, GetFloat(amountInput, 25f), reuseTransaction: false)),
                ("Preview Revive E", () => service.PreviewRevivalLifecycle(true, GetFloat(amountInput, 25f))),
                ("Revive E", () => service.ExecuteRevivalLifecycle(true, GetFloat(amountInput, 25f), reuseTransaction: false)));
            lifecycleText = AddText(parent, font, "Lifecycle not available.", 12, 260);
        }

        private void BuildOngoingEffectsSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Feature 6.4 Ongoing Damage / Healing / Recovery");
            ongoingAmountInput = AddInputRow(parent, font, "Tick Amount", "0");
            ongoingIntervalInput = AddInputRow(parent, font, "Interval", "0");
            ongoingDurationInput = AddInputRow(parent, font, "Duration", "0");
            ongoingTickCountInput = AddInputRow(parent, font, "Tick Count", "0");
            ongoingStackInput = AddInputRow(parent, font, "Stacks", "1");
            AddButtonRow(parent, font,
                ("Fresh Tx", () => service.GenerateOngoingEffectTransaction()),
                ("Preview P", () => service.PreviewOngoingEffect(GetSelected(ongoingEffects, selectedOngoingEffectIndex), targetEnemy: false, GetFloat(ongoingAmountInput, 0f), GetFloat(ongoingIntervalInput, 0f), GetFloat(ongoingDurationInput, 0f), GetInt(ongoingTickCountInput, 0), GetInt(ongoingStackInput, 1))),
                ("Apply P", () => service.ApplyOngoingEffect(GetSelected(ongoingEffects, selectedOngoingEffectIndex), targetEnemy: false, GetFloat(ongoingAmountInput, 0f), GetFloat(ongoingIntervalInput, 0f), GetFloat(ongoingDurationInput, 0f), GetInt(ongoingTickCountInput, 0), GetInt(ongoingStackInput, 1), reuseTransaction: false)),
                ("Reuse P", () => service.ApplyOngoingEffect(GetSelected(ongoingEffects, selectedOngoingEffectIndex), targetEnemy: false, GetFloat(ongoingAmountInput, 0f), GetFloat(ongoingIntervalInput, 0f), GetFloat(ongoingDurationInput, 0f), GetInt(ongoingTickCountInput, 0), GetInt(ongoingStackInput, 1), reuseTransaction: true)));
            AddButtonRow(parent, font,
                ("Preview E", () => service.PreviewOngoingEffect(GetSelected(ongoingEffects, selectedOngoingEffectIndex), targetEnemy: true, GetFloat(ongoingAmountInput, 0f), GetFloat(ongoingIntervalInput, 0f), GetFloat(ongoingDurationInput, 0f), GetInt(ongoingTickCountInput, 0), GetInt(ongoingStackInput, 1))),
                ("Apply E", () => service.ApplyOngoingEffect(GetSelected(ongoingEffects, selectedOngoingEffectIndex), targetEnemy: true, GetFloat(ongoingAmountInput, 0f), GetFloat(ongoingIntervalInput, 0f), GetFloat(ongoingDurationInput, 0f), GetInt(ongoingTickCountInput, 0), GetInt(ongoingStackInput, 1), reuseTransaction: false)),
                ("Stack E", () => service.ApplyOngoingEffect(GetSelected(ongoingEffects, selectedOngoingEffectIndex), targetEnemy: true, GetFloat(ongoingAmountInput, 0f), GetFloat(ongoingIntervalInput, 0f), GetFloat(ongoingDurationInput, 0f), GetInt(ongoingTickCountInput, 0), GetInt(ongoingStackInput, 1), reuseTransaction: false)),
                ("Cancel E", () => service.CancelFirstOngoingEffect(targetEnemy: true, preview: false)));
            AddButtonRow(parent, font,
                ("Tick Now", () => service.ProcessOngoingEffectsNow()),
                ("Advance <1", () => service.AdvanceOngoingEffects(Mathf.Max(0.1f, GetFloat(ongoingIntervalInput, 1f) * 0.5f))),
                ("Advance 1x", () => service.AdvanceOngoingEffects(Mathf.Max(0.1f, GetFloat(ongoingIntervalInput, 1f)))),
                ("Advance 5x", () => service.AdvanceOngoingEffects(Mathf.Max(0.1f, GetFloat(ongoingIntervalInput, 1f)) * 5f)));
            AddButtonRow(parent, font,
                ("Cancel P", () => service.CancelFirstOngoingEffect(targetEnemy: false, preview: false)),
                ("Preview Cancel P", () => service.CancelFirstOngoingEffect(targetEnemy: false, preview: true)),
                ("Save Active", () => service.Save()),
                ("Load Active", () => service.Load()));
            ongoingEffectsText = AddText(parent, font, "Ongoing effects not available.", 12, 320);
        }

        private void BuildStatusSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Apply To Player", () => service.ApplyStatus(GetSelected(statuses, selectedStatusIndex), toEnemy: false)),
                ("Apply To Enemy", () => service.ApplyStatus(GetSelected(statuses, selectedStatusIndex), toEnemy: true)),
                ("Remove From Player", () => service.RemoveStatus(GetSelected(statuses, selectedStatusIndex), fromEnemy: false)),
                ("Clear Temp Statuses", () => service.ClearTemporaryStatuses()));
        }

        private void BuildQuestSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Start Quest", () => service.StartQuest(GetSelected(quests, selectedQuestIndex))),
                ("Report Talk", () => service.ReportTalk(GetSelected(people, selectedPersonIndex))),
                ("Report Reach", () => service.ReportReach(GetSelected(places, selectedPlaceIndex))),
                ("Report Defeat", () => service.ReportDefeat("prototype_enemy")));
            AddButtonRow(parent, font,
                ("Accept Contract", () => service.AcceptContract(GetSelected(contracts, selectedContractIndex))),
                ("Clear Quests", () => service.ClearQuestLog(confirmed: false)),
                ("Clear Contracts", () => service.ClearContractJournal(confirmed: false)));
        }

        private void BuildPersistenceSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Save", () => service.Save()),
                ("Load", () => service.Load()),
                ("Validate Save", () => service.ValidateSave()),
                ("Delete Save", () => service.DeleteSave(confirmed: false)));
            AddButtonRow(parent, font,
                ("Save Manual 1", () => service.SaveManualSlotOne()),
                ("Force Autosave", () => service.ForceAutosave()),
                ("Short Autosave", () => service.SetShortAutosaveInterval()));
            AddButtonRow(parent, font,
                ("Mark Dirty", () => service.MarkSaveDirty()),
                ("Mark Clean", () => service.MarkSaveClean()),
                ("Validate Backup", () => service.ValidateManualSlotOneBackup()),
                ("Load Backup", () => service.LoadManualSlotOneBackup()));
            AddButtonRow(parent, font,
                ("Fingerprint", () => service.RecordFingerprint()),
                ("Recovery Scan", () => service.RunRecoveryScan()),
                ("Promote Backup", () => service.PromoteManualSlotOneBackup(confirmed: false)),
                ("Quarantine", () => service.QuarantineManualSlotOnePrimary(confirmed: false)));
            AddButtonRow(parent, font,
                ("Clean Temps", () => service.CleanupTemporarySaves(confirmed: false)),
                ("Fail Prepare", () => service.InjectPrepareFailure()),
                ("Fail Commit", () => service.InjectCommitFailure()),
                ("Fail Audit", () => service.InjectAuditFailure()));
            persistenceText = AddText(parent, font, "Save slots not available.", 12, 260);
            persistenceIntegrationText = AddText(parent, font, "Persistence integration not available.", 12, 260);
        }

        private void BuildLocationSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Teleport To Point", () => service.Teleport(GetSelected(testPoints, selectedTestPointIndex))),
                ("Save Location", () => service.Save()),
                ("Load Location", () => service.Load()),
                ("Validate Position", () => service.ValidateCurrentLocation()));
            locationText = AddText(parent, font, "Location not available.", 13, 210);
        }

        private void BuildWorldEntitySection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Refresh", () => service.RefreshWorldEntityDiagnostics()),
                ("Spawn Persistent", () => service.SpawnPersistentWorldLoot(GetSelected(items, selectedItemIndex))),
                ("Spawn Transient", () => service.SpawnTransientWorldLoot(GetSelected(items, selectedItemIndex))));
            AddButtonRow(parent, font,
                ("Destroy Spawned", () => service.DestroyLastSpawnedWorldLoot()),
                ("Recreate Saved", () => service.RecreateDestroyedWorldLoot()),
                ("Duplicate Proof", () => service.AttemptDuplicateWorldEntityRegistration()));
            worldEntityText = AddText(parent, font, "World entities not available.", 12, 260);
        }

        private void BuildAutomationSection(Transform parent, Font font)
        {
            AddHeader(parent, font, "Automated Test Lab Scenario Runner");
            automationSuiteValueText = AddSelectorRow(parent, font, "Suite", () => CycleAutomationSuite(-1), () => CycleAutomationSuite(1));
            automationScenarioValueText = AddSelectorRow(parent, font, "Scenario", () => CycleSelection(ref selectedAutomationScenarioIndex, automationScenarios.Count, -1), () => CycleSelection(ref selectedAutomationScenarioIndex, automationScenarios.Count, 1));
            AddButtonRow(parent, font,
                ("Run Scenario", () => service.RunAutomationScenario(GetSelected(automationSuites, selectedAutomationSuiteIndex)?.SuiteId, GetSelected(automationScenarios, selectedAutomationScenarioIndex)?.ScenarioId, automationStopOnFirstFailure)),
                ("Run Suite", () => StartAutomationBatch(TestLabAutomationRunMode.CurrentSuite)),
                ("Run Quick", () => StartAutomationBatch(TestLabAutomationRunMode.AllQuickSuites)),
                ("Run All", () => StartAutomationBatch(TestLabAutomationRunMode.AllSuites)));
            AddButtonRow(parent, font,
                ("Rerun Failed", () => service.RerunFailedAutomation(automationStopOnFirstFailure)),
                ("Cancel", CancelAutomationBatch),
                ("Clear", () => service.ClearAutomationResults()),
                ("Validate", () => service.ValidateAutomationRegistration()));
            AddButtonRow(parent, font,
                ("Export JSON", () => service.ExportAutomationJsonReport()),
                ("Export MD", () => service.ExportAutomationMarkdownReport()),
                ("Stop On Fail", ToggleAutomationStopOnFirstFailure),
                ("Auto Scroll", ToggleAutomationAutoScroll));
            automationText = AddReportText(parent, font, "Automation not run.", 12, 150);
        }

        private void BuildScenarioSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Clean", () => service.RunScenario("clean", GetSelected(items, selectedItemIndex), GetSelected(quests, selectedQuestIndex), GetSelected(contracts, selectedContractIndex), GetSelected(damageTypes, selectedDamageIndex))),
                ("Combat", () => service.RunScenario("combat", GetSelected(items, selectedItemIndex), GetSelected(quests, selectedQuestIndex), GetSelected(contracts, selectedContractIndex), GetSelected(damageTypes, selectedDamageIndex))),
                ("Full Inventory", () => service.RunScenario("full-inventory", GetSelected(items, selectedItemIndex), GetSelected(quests, selectedQuestIndex), GetSelected(contracts, selectedContractIndex), GetSelected(damageTypes, selectedDamageIndex))));
            AddButtonRow(parent, font,
                ("Quest", () => service.RunScenario("quest", GetSelected(items, selectedItemIndex), GetSelected(quests, selectedQuestIndex), GetSelected(contracts, selectedContractIndex), GetSelected(damageTypes, selectedDamageIndex))),
                ("Contract", () => service.RunScenario("contract", GetSelected(items, selectedItemIndex), GetSelected(quests, selectedQuestIndex), GetSelected(contracts, selectedContractIndex), GetSelected(damageTypes, selectedDamageIndex))),
                ("Persistence", () => service.RunScenario("persistence", GetSelected(items, selectedItemIndex), GetSelected(quests, selectedQuestIndex), GetSelected(contracts, selectedContractIndex), GetSelected(damageTypes, selectedDamageIndex))));
        }

        private void BuildDiagnosticsSection(Transform parent, Font font)
        {
            AddButtonRow(parent, font,
                ("Run Diagnostics", () => RunDiagnostics()),
                ("Refresh", Refresh));
            diagnosticsText = AddText(parent, font, "Diagnostics not run.", 13, 210);
            historyText = AddText(parent, font, "No operations yet.", 12, 260);
        }

        private void RefreshSelectors()
        {
            if (service == null)
            {
                return;
            }

            SetOptions(items, service.GetDefinitions<ItemDefinition>(), ref selectedItemIndex);
            SetOptions(statuses, service.GetDefinitions<StatusEffectDefinition>(), ref selectedStatusIndex);
            SetOptions(roles, service.GetDefinitions<RoleDefinition>(), ref selectedRoleIndex);
            SetOptions(socialStatuses, service.GetDefinitions<SocialStatusDefinition>(), ref selectedSocialStatusIndex);
            SetOptions(currencies, service.GetDefinitions<CurrencyDefinition>(), ref selectedCurrencyIndex);
            SetOptions(damageTypes, service.GetDefinitions<DamageTypeDefinition>(), ref selectedDamageIndex);
            SetOptions(defensiveActions, service.GetDefinitions<DefensiveActionDefinition>(), ref selectedDefenseIndex);
            SetOptions(combatExecutions, service.GetDefinitions<CombatExecutionDefinition>(), ref selectedCombatExecutionIndex);
            SetOptions(combatReactions, service.GetDefinitions<CombatReactionDefinition>(), ref selectedCombatReactionIndex);
            SetOptions(automationSuites, service.GetAutomationSuites(), ref selectedAutomationSuiteIndex);
            RefreshAutomationScenarioOptions();
            SetOptions(ongoingEffects, service.GetDefinitions<OngoingEffectDefinition>(), ref selectedOngoingEffectIndex);
            SetOptions(quests, service.GetDefinitions<QuestDefinition>(), ref selectedQuestIndex);
            SetOptions(contracts, service.GetDefinitions<ContractDefinition>(), ref selectedContractIndex);
            SetOptions(places, service.GetDefinitions<PlaceDefinition>(), ref selectedPlaceIndex);
            SetOptions(people, service.GetDefinitions<PersonDefinition>(), ref selectedPersonIndex);
            SetOptions(testPoints, service.GetTestPoints(), ref selectedTestPointIndex);
            SetOptions(skills, service.GetDefinitions<SkillDefinition>(), ref selectedSkillIndex);
            SetOptions(traits, service.GetDefinitions<TraitDefinition>(), ref selectedTraitIndex);
            SetOptions(requirements, service.GetDefinitions<RequirementSetDefinition>(), ref selectedRequirementIndex);
        }

        private void UpdateSelectorLabels()
        {
            SetValue(itemValueText, FormatSelected(items, selectedItemIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(statusValueText, FormatSelected(statuses, selectedStatusIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(roleValueText, FormatSelected(roles, selectedRoleIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(socialStatusValueText, FormatSelected(socialStatuses, selectedSocialStatusIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(currencyValueText, FormatSelected(currencies, selectedCurrencyIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(damageValueText, FormatSelected(damageTypes, selectedDamageIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(defenseValueText, FormatSelected(defensiveActions, selectedDefenseIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(combatExecutionValueText, FormatSelected(combatExecutions, selectedCombatExecutionIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(combatReactionValueText, FormatSelected(combatReactions, selectedCombatReactionIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(automationSuiteValueText, FormatSelected(automationSuites, selectedAutomationSuiteIndex, suite => $"{suite.DisplayName} ({suite.SuiteId})"));
            SetValue(automationScenarioValueText, FormatSelected(automationScenarios, selectedAutomationScenarioIndex, scenario => $"{scenario.DisplayName} ({scenario.ScenarioId})"));
            SetValue(ongoingEffectValueText, FormatSelected(ongoingEffects, selectedOngoingEffectIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(questValueText, FormatSelected(quests, selectedQuestIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(contractValueText, FormatSelected(contracts, selectedContractIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(placeValueText, FormatSelected(places, selectedPlaceIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(personValueText, FormatSelected(people, selectedPersonIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(testPointValueText, FormatSelected(testPoints, selectedTestPointIndex, point => $"{point.DisplayName} ({point.TestPointId})"));
            SetValue(skillValueText, FormatSelected(skills, selectedSkillIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(traitValueText, FormatSelected(traits, selectedTraitIndex, PrototypeTestLabService.FormatDefinition));
            SetValue(requirementValueText, FormatSelected(requirements, selectedRequirementIndex, PrototypeTestLabService.FormatDefinition));
        }

        private void RefreshActiveSectionSummary()
        {
            if (service == null || activeSectionIndex < 0 || activeSectionIndex >= SectionNames.Length)
            {
                return;
            }

            switch (SectionNames[activeSectionIndex])
            {
                case "Overview":
                    SetValue(overviewText, service.BuildOverview());
                    break;
                case "Character 5.6":
                    SetValue(characterSystemText, service.BuildCharacterSystemSummary(developmentView: true));
                    break;
                case "Body Species 7.1":
                    SetValue(bodySpeciesText, service.BuildBodySpeciesSummary());
                    break;
                case "Body Anatomy 7.2":
                    SetValue(bodyAnatomyText, service.BuildBodyAnatomySummary());
                    break;
                case "Body Condition 7.3":
                    SetValue(bodyConditionText, service.BuildBodyConditionSummary());
                    break;
                case "Vital Processes 7.4":
                    SetValue(vitalProcessesText, service.BuildVitalProcessSummary());
                    break;
                case "Biological Hazards 7.5":
                    SetValue(biologicalHazardsText, service.BuildBiologicalHazardSummary());
                    break;
                case "Biological Compatibility 7.6":
                    SetValue(biologicalCompatibilityText, service.BuildBiologicalCompatibilitySummary());
                    break;
                case "Natural Recovery 7.7":
                    SetValue(biologicalRecoveryText, service.BuildBiologicalRecoverySummary());
                    break;
                case "Transformation 7.8":
                    SetValue(bodyTransformationText, service.BuildBodyTransformationSummary());
                    break;
                case "Biological Conditions 7.9":
                    SetValue(biologicalConditionsText, service.BuildBiologicalConditionSummary());
                    break;
                case "Biology Integration 7.10":
                    SetValue(bodyBiologyIntegrationText, service.BuildBodyBiologyIntegrationSummary());
                    break;
                case "Knowledge 8.1":
                    SetValue(knowledgeText, service.BuildKnowledgeSummary());
                    break;
                case "Observation 8.2":
                    SetValue(observationText, service.BuildObservationSummary());
                    break;
                case "History 8.3":
                    SetValue(characterHistoryText, service.BuildHistorySummary());
                    break;
                case "Memory 8.4":
                    SetValue(memoryRecallText, service.BuildMemoryRecallSummary());
                    break;
                case "Life Events 8.5":
                    SetValue(lifeEventsText, service.BuildLifeEventSummary());
                    break;
                case "Sources 8.6":
                    SetValue(informationSourcesText, service.BuildInformationSourceSummary());
                    break;
                case "Sharing 8.7":
                    SetValue(informationSharingText, service.BuildInformationTransferSummary());
                    break;
                case "Access 8.8":
                    SetValue(informationAccessText, service.BuildInformationAccessSummary());
                    break;
                case "Records 8.9":
                    SetValue(knowledgeRecordsText, service.BuildKnowledgeRecordSummary());
                    break;
                case "Integration 8.10":
                    SetValue(knowledgeHistoryIntegrationText, service.BuildKnowledgeHistoryIntegrationSummary());
                    break;
                case "Identity 5.1":
                    SetValue(identityProgressionText, service.BuildIdentityProgressionSummary());
                    break;
                case "Numbers 5.4a":
                    SetValue(attributesCalculatedStatsText, service.BuildAttributeCalculatedStatsSummary());
                    break;
                case "Resources 5.4b":
                    SetValue(resourcesText, service.BuildCurrentResourcesSummary());
                    break;
                case "Traits 5.5":
                    SetValue(traitsText, service.BuildTraitsSummary(includeHidden: true));
                    break;
                case "Skills 5.3":
                    SetValue(skillsText, service.BuildSkillsSummary(includeHidden: true));
                    break;
                case "Defense 6.6":
                    SetValue(defensiveActionsText, service.BuildDefensiveActionSummary());
                    break;
                case "Execution 6.7":
                    SetValue(combatExecutionText, service.BuildCombatExecutionSummary());
                    break;
                case "Reactions 6.8":
                    SetValue(combatReactionText, service.BuildCombatReactionSummary());
                    break;
                case "Contribution 6.9":
                    SetValue(combatContributionText, service.BuildCombatContributionSummary());
                    break;
                case "Combat Overview 6.10":
                case "Combat":
                    SetValue(combatRuntimeText, service.BuildCombatRuntimeSummary());
                    break;
                case "Combat State 6.5":
                    SetValue(combatStateText, service.BuildCombatStateSummary());
                    break;
                case "Lifecycle 6.3":
                    SetValue(lifecycleText, service.BuildLifecycleSummary());
                    break;
                case "Ongoing 6.4":
                    SetValue(ongoingEffectsText, service.BuildOngoingEffectsSummary());
                    break;
                case "Persistence":
                    SetValue(persistenceText, service.BuildSaveSlotSummary());
                    SetValue(persistenceIntegrationText, service.BuildPersistenceIntegrationSummary());
                    break;
                case "Location":
                    SetValue(locationText, service.BuildLocationSummary());
                    break;
                case "World Entities":
                    SetValue(worldEntityText, service.BuildWorldEntitySummary());
                    break;
                case "Automation":
                    SetValue(automationText, service.BuildAutomationSummary());
                    break;
                case "Diagnostics":
                    UpdateHistory();
                    break;
            }
        }

        private void UpdateHistory()
        {
            if (historyText == null || !historyText.gameObject.activeInHierarchy)
            {
                return;
            }

            StringBuilder builder = new StringBuilder();
            foreach (PrototypeTestLabOperation operation in service.History)
            {
                builder.Append(operation.Timestamp.ToString("HH:mm:ss"));
                builder.Append(operation.Succeeded ? " OK " : " FAIL ");
                builder.Append(operation.OperationName);
                builder.Append(" [");
                builder.Append(operation.Code);
                builder.Append("] ");
                builder.AppendLine(operation.Message);
            }

            historyText.text = builder.Length == 0 ? "No operations yet." : builder.ToString().TrimEnd();
        }

        private void UpdateLatestOperation()
        {
            if (latestOperationText == null)
            {
                return;
            }

            if (service == null || service.History.Count == 0)
            {
                latestOperationText.text = "Last Result: No operations yet.";
                return;
            }

            if (!string.IsNullOrWhiteSpace(automationProgressText))
            {
                latestOperationText.text = automationProgressText;
                return;
            }

            PrototypeTestLabOperation operation = service.History[0];
            string status = operation.Succeeded ? "OK" : "FAIL";
            latestOperationText.text = $"Last Result: {status} {operation.OperationName} [{operation.Code}] {operation.Message}";
        }

        private void RunDiagnostics()
        {
            if (diagnosticsText != null)
            {
                diagnosticsText.text = service.RunDiagnostics();
            }
        }

        private static string FormatSelected<T>(IReadOnlyList<T> values, int selectedIndex, Func<T, string> formatter)
            where T : class
        {
            T selected = GetSelected(values, selectedIndex);
            return selected == null ? "None" : formatter(selected);
        }

        private static void SetOptions<T>(List<T> target, IReadOnlyList<T> values, ref int selectedIndex)
            where T : class
        {
            target.Clear();
            if (values != null)
            {
                target.AddRange(values);
            }

            selectedIndex = target.Count == 0 ? 0 : Mathf.Clamp(selectedIndex, 0, target.Count - 1);
        }

        private static T GetSelected<T>(IReadOnlyList<T> values, int selectedIndex)
            where T : class
        {
            return values == null || selectedIndex < 0 || selectedIndex >= values.Count ? null : values[selectedIndex];
        }

        private static int GetInt(InputField input, int fallback)
        {
            return input != null && int.TryParse(input.text, out int value) ? value : fallback;
        }

        private static float GetFloat(InputField input, float fallback)
        {
            return input != null && float.TryParse(input.text, out float value) ? value : fallback;
        }

        private void CycleSelection(ref int selectedIndex, int count, int direction)
        {
            if (count <= 0)
            {
                return;
            }

            selectedIndex = (selectedIndex + direction) % count;
            if (selectedIndex < 0)
            {
                selectedIndex += count;
            }

            UpdateSelectorLabels();
        }

        private void CycleAutomationSuite(int direction)
        {
            if (automationSuites.Count <= 0)
            {
                return;
            }

            selectedAutomationSuiteIndex = (selectedAutomationSuiteIndex + direction) % automationSuites.Count;
            if (selectedAutomationSuiteIndex < 0)
            {
                selectedAutomationSuiteIndex += automationSuites.Count;
            }

            RefreshAutomationScenarioOptions();
            UpdateSelectorLabels();
        }

        private void RefreshAutomationScenarioOptions()
        {
            ITestLabAutomationSuite selectedSuite = GetSelected(automationSuites, selectedAutomationSuiteIndex);
            SetOptions(automationScenarios, selectedSuite == null ? Array.Empty<ITestLabAutomationScenario>() : selectedSuite.Scenarios, ref selectedAutomationScenarioIndex);
        }

        private void ToggleAutomationStopOnFirstFailure()
        {
            automationStopOnFirstFailure = !automationStopOnFirstFailure;
            if (latestOperationText != null)
            {
                latestOperationText.text = $"Automation Stop On Fail: {(automationStopOnFirstFailure ? "On" : "Off")}";
            }
        }

        private void ToggleAutomationAutoScroll()
        {
            automationAutoScroll = !automationAutoScroll;
            if (automationAutoScroll && bodyScrollRect != null)
            {
                ScrollBodyToBottom();
            }
        }

        private void StartAutomationBatch(TestLabAutomationRunMode runMode)
        {
            if (automationRunCoroutine != null)
            {
                if (latestOperationText != null)
                {
                    latestOperationText.text = "Automation is already running. Press Cancel before starting another batch.";
                }

                return;
            }

            List<(string SuiteId, string ScenarioId)> selections = BuildAutomationSelections(runMode);
            automationCancelRequested = false;
            automationRunCoroutine = StartCoroutine(RunAutomationBatch(runMode, selections));
        }

        private List<(string SuiteId, string ScenarioId)> BuildAutomationSelections(TestLabAutomationRunMode runMode)
        {
            List<(string SuiteId, string ScenarioId)> selections = new List<(string SuiteId, string ScenarioId)>();
            if (runMode == TestLabAutomationRunMode.CurrentSuite)
            {
                ITestLabAutomationSuite suite = GetSelected(automationSuites, selectedAutomationSuiteIndex);
                if (suite != null)
                {
                    foreach (ITestLabAutomationScenario scenario in suite.Scenarios)
                    {
                        selections.Add((suite.SuiteId, scenario.ScenarioId));
                    }
                }

                return selections;
            }

            bool quickOnly = runMode == TestLabAutomationRunMode.AllQuickSuites;
            foreach (ITestLabAutomationSuite suite in automationSuites)
            {
                if (suite == null || !suite.IncludeInRunAll)
                {
                    continue;
                }

                foreach (ITestLabAutomationScenario scenario in suite.Scenarios)
                {
                    if (!quickOnly || scenario.IncludeInQuickRun || scenario.Category == TestLabAutomationCategory.Quick)
                    {
                        selections.Add((suite.SuiteId, scenario.ScenarioId));
                    }
                }
            }

            return selections;
        }

        private IEnumerator RunAutomationBatch(TestLabAutomationRunMode runMode, IReadOnlyList<(string SuiteId, string ScenarioId)> selections)
        {
            service.BeginAutomationBatch(runMode);
            SetAutomationProgress($"Automation Running: 0/{selections.Count} {runMode}");
            Refresh();
            yield return null;

            for (int i = 0; i < selections.Count; i++)
            {
                if (automationCancelRequested)
                {
                    break;
                }

                (string suiteId, string scenarioId) = selections[i];
                SetAutomationProgress($"Automation Running: {i + 1}/{selections.Count} {ShortAutomationId(suiteId)}/{scenarioId}");
                Refresh();
                yield return null;

                PrototypeTestLabOperation operation = service.RunAutomationScenarioInBatch(suiteId, scenarioId, automationStopOnFirstFailure);
                SetAutomationProgress($"Automation Progress: {i + 1}/{selections.Count} {(operation.Succeeded ? "PASS" : "FAIL")} {ShortAutomationId(suiteId)}/{scenarioId}");
                Refresh();
                if (automationAutoScroll && bodyScrollRect != null)
                {
                    ScrollBodyToBottom();
                }

                if (automationStopOnFirstFailure && !operation.Succeeded)
                {
                    break;
                }

                yield return null;
            }

            service.CompleteAutomationBatch(automationCancelRequested);
            automationProgressText = string.Empty;
            Refresh();
            automationRunCoroutine = null;
            automationCancelRequested = false;
        }

        private void SetAutomationProgress(string text)
        {
            automationProgressText = text ?? string.Empty;
            UpdateLatestOperation();
        }

        private static string ShortAutomationId(string suiteId)
        {
            if (string.IsNullOrWhiteSpace(suiteId))
            {
                return "suite";
            }

            const string prefix = "feature.";
            return suiteId.StartsWith(prefix, StringComparison.Ordinal) ? suiteId.Substring(prefix.Length) : suiteId;
        }

        private void CancelAutomationBatch()
        {
            automationCancelRequested = true;
            service.CancelAutomation();
        }

        private void SetActiveSection(int sectionIndex)
        {
            if (sectionIndex < 0 || sectionIndex >= sectionRoots.Count)
            {
                return;
            }

            activeSectionIndex = sectionIndex;
            int previousGroupIndex = activeSectionGroupIndex;
            activeSectionGroupIndex = FindSectionGroupIndex(activeSectionIndex);
            if (activeSectionGroupIndex != previousGroupIndex)
            {
                RebuildSectionFeatureMenu(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            }

            for (int i = 0; i < sectionRoots.Count; i++)
            {
                sectionRoots[i].SetActive(i == activeSectionIndex);
            }

            UpdateSectionButtonStates();
            RefreshActiveSectionSummary();
            Canvas.ForceUpdateCanvases();
            if (bodyScrollRect != null)
            {
                ScrollBodyToTop();
            }
        }

        private void ScrollBodyToTop()
        {
            if (bodyScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            bodyScrollRect.StopMovement();
            bodyScrollRect.verticalNormalizedPosition = 1f;
        }

        private void ScrollBodyToBottom()
        {
            if (bodyScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            bodyScrollRect.StopMovement();
            bodyScrollRect.verticalNormalizedPosition = 0f;
        }

        private void AddSectionTabs(Transform parent, Font font)
        {
            sectionGroupButtons.Clear();
            sectionFeatureButtons.Clear();
            sectionGroups.Clear();
            sectionGroups.AddRange(BuildSectionNavigationGroups());

            GameObject groupRow = CreateRow("Test Lab Step Menus", parent, 36f);
            for (int i = 0; i < sectionGroups.Count; i++)
            {
                int groupIndex = i;
                Button button = AddButton(groupRow.transform, font, sectionGroups[i].DisplayName + " v", 10);
                button.onClick.AddListener(() => SetActiveSectionGroup(groupIndex));
                sectionGroupButtons.Add(button);
            }

            GameObject featureRow = CreateRow("Test Lab Feature Menu", parent, 36f);
            sectionFeatureMenuRoot = featureRow.transform;
            RebuildSectionFeatureMenu(font);
        }

        private void UpdateSectionButtonStates()
        {
            if (activeSectionGroupIndex < 0 || activeSectionGroupIndex >= sectionGroups.Count)
            {
                activeSectionGroupIndex = FindSectionGroupIndex(activeSectionIndex);
            }

            for (int i = 0; i < sectionGroupButtons.Count; i++)
            {
                Image image = sectionGroupButtons[i] == null ? null : sectionGroupButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    image.color = i == activeSectionGroupIndex ? ActiveButtonColor : ButtonColor;
                }
            }

            for (int i = 0; i < sectionFeatureButtons.Count; i++)
            {
                Image image = sectionFeatureButtons[i] == null ? null : sectionFeatureButtons[i].GetComponent<Image>();
                if (image != null)
                {
                    int sectionIndex = i >= 0 && activeSectionGroupIndex >= 0 && activeSectionGroupIndex < sectionGroups.Count && i < sectionGroups[activeSectionGroupIndex].SectionIndices.Count
                        ? sectionGroups[activeSectionGroupIndex].SectionIndices[i]
                        : -1;
                    image.color = sectionIndex == activeSectionIndex ? ActiveButtonColor : ButtonColor;
                }
            }
        }

        private void SetActiveSectionGroup(int groupIndex)
        {
            if (groupIndex < 0 || groupIndex >= sectionGroups.Count)
            {
                return;
            }

            activeSectionGroupIndex = groupIndex;
            RebuildSectionFeatureMenu(Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
            UpdateSectionButtonStates();
        }

        private void RebuildSectionFeatureMenu(Font font)
        {
            if (sectionFeatureMenuRoot == null || font == null)
            {
                return;
            }

            for (int i = sectionFeatureMenuRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(sectionFeatureMenuRoot.GetChild(i).gameObject);
            }

            sectionFeatureButtons.Clear();
            if (activeSectionGroupIndex < 0 || activeSectionGroupIndex >= sectionGroups.Count)
            {
                activeSectionGroupIndex = FindSectionGroupIndex(activeSectionIndex);
            }

            if (activeSectionGroupIndex < 0 || activeSectionGroupIndex >= sectionGroups.Count)
            {
                activeSectionGroupIndex = 0;
            }

            SectionNavigationGroup group = sectionGroups[activeSectionGroupIndex];
            foreach (int sectionIndex in group.SectionIndices)
            {
                if (sectionIndex < 0 || sectionIndex >= SectionNames.Length)
                {
                    continue;
                }

                int capturedIndex = sectionIndex;
                Button button = AddButton(sectionFeatureMenuRoot, font, SectionNames[sectionIndex], 10);
                button.onClick.AddListener(() => SetActiveSection(capturedIndex));
                sectionFeatureButtons.Add(button);
            }
        }

        private int FindSectionGroupIndex(int sectionIndex)
        {
            for (int i = 0; i < sectionGroups.Count; i++)
            {
                if (ContainsSection(sectionGroups[i], sectionIndex))
                {
                    return i;
                }
            }

            return 0;
        }

        private static bool ContainsSection(SectionNavigationGroup group, int sectionIndex)
        {
            if (group == null)
            {
                return false;
            }

            for (int i = 0; i < group.SectionIndices.Count; i++)
            {
                if (group.SectionIndices[i] == sectionIndex)
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<SectionNavigationGroup> BuildSectionNavigationGroups()
        {
            return new[]
            {
                Group("General Tools", "Overview", "Player", "Automation", "Scenarios", "Diagnostics"),
                Group("World Data Step 3", "Inventory", "Statuses", "Quests"),
                Group("Persistence Step 4", "Persistence", "Location", "World Entities"),
                Group("Character Step 5", "Identity 5.1", "Numbers 5.4a", "Resources 5.4b", "Traits 5.5", "Skills 5.3", "Character 5.6"),
                Group("Combat Step 6", "Combat", "Lifecycle 6.3", "Ongoing 6.4", "Combat State 6.5", "Defense 6.6", "Execution 6.7", "Reactions 6.8", "Contribution 6.9", "Combat Overview 6.10"),
                Group("Body Step 7", "Body Species 7.1", "Body Anatomy 7.2", "Body Condition 7.3", "Vital Processes 7.4", "Biological Hazards 7.5", "Biological Compatibility 7.6", "Natural Recovery 7.7", "Transformation 7.8", "Biological Conditions 7.9", "Biology Integration 7.10"),
                Group("Knowledge Step 8", "Knowledge 8.1", "Observation 8.2", "History 8.3", "Memory 8.4", "Life Events 8.5", "Sources 8.6", "Sharing 8.7", "Access 8.8", "Records 8.9", "Integration 8.10"),
                Group("Items Step 9", "Identity 9.1")
            };
        }

        private static SectionNavigationGroup Group(string displayName, params string[] sectionNames)
        {
            List<int> indices = new List<int>();
            foreach (string sectionName in sectionNames)
            {
                int index = Array.IndexOf(SectionNames, sectionName);
                if (index >= 0)
                {
                    indices.Add(index);
                }
            }

            return new SectionNavigationGroup(displayName, indices);
        }

        private sealed class SectionNavigationGroup
        {
            public SectionNavigationGroup(string displayName, IReadOnlyList<int> sectionIndices)
            {
                DisplayName = displayName ?? string.Empty;
                SectionIndices = sectionIndices ?? Array.Empty<int>();
            }

            public string DisplayName { get; }
            public IReadOnlyList<int> SectionIndices { get; }
        }

        private Transform AddSection(Transform parent, string name)
        {
            GameObject section = CreateChild(name, parent, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            VerticalLayoutGroup layout = section.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            section.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sectionRoots.Add(section);
            return section.transform;
        }

        private ScrollRect AddBodyScroll(Transform parent)
        {
            GameObject scrollObject = CreateChild("Test Lab Body", parent, typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
            scrollObject.GetComponent<Image>().color = new Color(0.025f, 0.028f, 0.035f, 0.92f);
            LayoutElement scrollLayout = scrollObject.GetComponent<LayoutElement>();
            scrollLayout.minHeight = 220f;
            scrollLayout.flexibleHeight = 1f;

            GameObject viewport = CreateChild("Viewport", scrollObject.transform, typeof(Image), typeof(Mask));
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0.025f, 0.028f, 0.035f, 0.92f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;
            RectTransform viewportRect = viewport.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(0f, 0f);
            viewportRect.offsetMax = new Vector2(-16f, 0f);

            GameObject content = CreateChild("Test Lab Body Content", viewport.transform, typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = new Vector2(10f, 0f);
            contentRect.offsetMax = new Vector2(-10f, -10f);
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 10, 34);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scrollRect = scrollObject.GetComponent<ScrollRect>();
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.18f;
            scrollRect.scrollSensitivity = 72f;
            scrollRect.verticalScrollbar = AddVerticalScrollbar(scrollObject.transform);
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scrollRect.verticalScrollbarSpacing = 2f;
            return scrollRect;
        }

        private static Scrollbar AddVerticalScrollbar(Transform parent)
        {
            GameObject scrollbarObject = CreateChild("Vertical Scrollbar", parent, typeof(Image), typeof(Scrollbar));
            Image track = scrollbarObject.GetComponent<Image>();
            track.color = new Color(0.08f, 0.09f, 0.10f, 0.95f);
            RectTransform scrollbarRect = scrollbarObject.GetComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.offsetMin = new Vector2(-14f, 4f);
            scrollbarRect.offsetMax = new Vector2(-4f, -4f);

            GameObject handleObject = CreateChild("Handle", scrollbarObject.transform, typeof(Image));
            Image handle = handleObject.GetComponent<Image>();
            handle.color = new Color(0.24f, 0.42f, 0.50f, 1f);
            RectTransform handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.offsetMin = Vector2.zero;
            handleRect.offsetMax = Vector2.zero;
            Scrollbar scrollbar = scrollbarObject.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handle;
            scrollbar.handleRect = handleRect;
            return scrollbar;
        }

        private static void AddHeader(Transform parent, Font font, string text)
        {
            Text header = AddText(parent, font, text, 16, 28, FontStyle.Bold);
            header.color = new Color(0.92f, 0.95f, 0.98f, 1f);
        }

        private static Text AddSelectorRow(Transform parent, Font font, string label, Action previous, Action next)
        {
            GameObject row = CreateRow("Selector - " + label, parent, 32f);
            Text labelText = AddText(row.transform, font, label, 12, 28, FontStyle.Bold);
            SetElement(labelText.gameObject, 78f, 28f, 0f);
            Text valueText = AddText(row.transform, font, "None", 12, 28);
            valueText.color = new Color(0.86f, 0.92f, 0.96f, 1f);
            SetElement(valueText.gameObject, 0f, 28f, 1f);
            AddFixedButton(row.transform, font, "Prev", previous, 58f);
            AddFixedButton(row.transform, font, "Next", next, 58f);
            return valueText;
        }

        private static InputField AddInputRow(Transform parent, Font font, string label, string value)
        {
            GameObject row = CreateRow("Input - " + label, parent, 32f);
            Text labelText = AddText(row.transform, font, label, 12, 28, FontStyle.Bold);
            SetElement(labelText.gameObject, 78f, 28f, 0f);
            GameObject inputObject = CreateChild(label, row.transform, typeof(Image), typeof(InputField), typeof(LayoutElement));
            inputObject.GetComponent<Image>().color = FieldColor;
            SetElement(inputObject, 0f, 30f, 1f);
            Text text = AddText(inputObject.transform, font, value, 12, 28);
            text.rectTransform.offsetMin = new Vector2(8f, 2f);
            text.rectTransform.offsetMax = new Vector2(-8f, -2f);
            InputField input = inputObject.GetComponent<InputField>();
            input.textComponent = text;
            input.text = value;
            return input;
        }

        private static void AddButtonRow(Transform parent, Font font, params (string Label, Action Action)[] buttons)
        {
            GameObject row = CreateRow("Action Row", parent, 34f);
            foreach ((string label, Action action) in buttons)
            {
                Button button = AddButton(row.transform, font, label, 11);
                button.onClick.AddListener(() => action?.Invoke());
            }
        }

        private static Button AddFixedButton(Transform parent, Font font, string label, Action action, float width)
        {
            Button button = AddButton(parent, font, label, 11);
            SetElement(button.gameObject, width, 30f, 0f);
            button.onClick.AddListener(() => action?.Invoke());
            return button;
        }

        private static Button AddButton(Transform parent, Font font, string label, int fontSize)
        {
            GameObject root = CreateChild(label, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
            root.GetComponent<Image>().color = ButtonColor;
            SetElement(root, 96f, 32f, 1f);
            Text text = AddText(root.transform, font, label, fontSize, 28, FontStyle.Bold);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;
            text.rectTransform.offsetMin = new Vector2(4f, 2f);
            text.rectTransform.offsetMax = new Vector2(-4f, -2f);
            return root.GetComponent<Button>();
        }

        private static Text AddText(Transform parent, Font font, string text, int size, float height, FontStyle style = FontStyle.Normal)
        {
            GameObject obj = CreateChild(text, parent, typeof(Text), typeof(LayoutElement));
            Text label = obj.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.text = text;
            SetElement(obj, 0f, height, 1f);
            return label;
        }

        private Text AddReportText(Transform parent, Font font, string text, int size, float minimumHeight)
        {
            GameObject obj = CreateChild("Report Text", parent, typeof(Text), typeof(LayoutElement));
            Text label = obj.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.UpperLeft;
            label.color = Color.white;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.text = text;
            SetElement(obj, 0f, minimumHeight, 1f);
            obj.GetComponent<LayoutElement>().minHeight = minimumHeight;
            dynamicTextBlocks.Add(label);
            UpdateDynamicTextHeight(label);
            return label;
        }

        private static GameObject CreateRow(string name, Transform parent, float height)
        {
            GameObject row = CreateChild(name, parent, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            SetElement(row, 0f, height, 1f);
            return row;
        }

        private static void SetElement(GameObject obj, float preferredWidth, float preferredHeight, float flexibleWidth)
        {
            LayoutElement layoutElement = obj.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = obj.AddComponent<LayoutElement>();
            }

            layoutElement.preferredWidth = preferredWidth <= 0f ? -1f : preferredWidth;
            layoutElement.preferredHeight = preferredHeight;
            layoutElement.flexibleWidth = flexibleWidth;
            layoutElement.flexibleHeight = 0f;
        }

        private void SetValue(Text text, string value)
        {
            if (text != null)
            {
                if (dynamicTextBlocks.Contains(text))
                {
                    text.text = ClampDynamicReportText(value).TrimEnd();
                    UpdateDynamicTextHeight(text);
                    return;
                }

                text.text = value;
            }
        }

        private static void UpdateDynamicTextHeight(Text text)
        {
            if (text == null)
            {
                return;
            }

            LayoutElement layout = text.GetComponent<LayoutElement>();
            if (layout == null)
            {
                return;
            }

            float minimumHeight = Mathf.Max(80f, layout.minHeight);
            int lineCount = CountLines(text.text);
            float estimatedLineHeight = Mathf.Max(12f, text.fontSize * 1.35f);
            float estimatedHeight = Mathf.Min(MaximumDynamicReportHeight, lineCount * estimatedLineHeight + 16f);
            layout.preferredHeight = Mathf.Max(minimumHeight, estimatedHeight);
        }

        private static string ClampDynamicReportText(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string trimmed = TrimToLastLines(value, MaximumDynamicReportLines);
            if (trimmed.Length <= MaximumDynamicReportCharacters)
            {
                return trimmed;
            }

            int keep = Mathf.Max(0, MaximumDynamicReportCharacters - 96);
            string tail = trimmed.Substring(trimmed.Length - keep);
            int firstLineBreak = tail.IndexOf('\n');
            if (firstLineBreak >= 0 && firstLineBreak + 1 < tail.Length)
            {
                tail = tail.Substring(firstLineBreak + 1);
            }

            return $"[Report display truncated to the newest {MaximumDynamicReportCharacters} characters. Full failure details are logged to the Console.]\n{tail}";
        }

        private static string TrimToLastLines(string value, int maximumLines)
        {
            if (maximumLines <= 0 || string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            int linesSeen = 1;
            for (int i = value.Length - 1; i >= 0; i--)
            {
                if (value[i] != '\n')
                {
                    continue;
                }

                linesSeen++;
                if (linesSeen > maximumLines)
                {
                    return $"[Report display truncated to the newest {maximumLines} lines. Full failure details are logged to the Console.]\n{value.Substring(i + 1)}";
                }
            }

            return value;
        }

        private static int CountLines(string value)
        {
            value = string.IsNullOrEmpty(value) ? string.Empty : value.TrimEnd();
            if (string.IsNullOrEmpty(value))
            {
                return 1;
            }

            int lines = 1;
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\n')
                {
                    lines++;
                }
            }

            return lines;
        }

        private static GameObject CreateChild(string name, Transform parent, params Type[] components)
        {
            GameObject obj = new GameObject(name, components);
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            return obj;
        }
    }
}
#endif
