using System;
using System.Collections.Generic;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.OngoingEffects;
using UnityIsekaiGame.Combat.Reactions;

namespace UnityIsekaiGame.Combat.Contributions
{
    [DisallowMultipleComponent]
    public sealed class CombatContributionEventBridge : MonoBehaviour
    {
        [SerializeField] private CombatContributionService contributionService;
        [SerializeField] private CombatStateService combatStateService;
        [SerializeField] private OngoingEffectService[] ongoingEffectServices;
        [SerializeField] private ActorLifecycleController[] lifecycleControllers;

        private readonly HashSet<DamageHealingService> damageHealingServices = new HashSet<DamageHealingService>();
        private readonly HashSet<AttackResolutionService> attackResolutionServices = new HashSet<AttackResolutionService>();
        private readonly HashSet<DefensiveActionService> defensiveActionServices = new HashSet<DefensiveActionService>();
        private readonly HashSet<OngoingEffectService> subscribedOngoingEffectServices = new HashSet<OngoingEffectService>();
        private readonly HashSet<CombatReactionService> reactionServices = new HashSet<CombatReactionService>();
        private readonly HashSet<ActorLifecycleController> subscribedLifecycleControllers = new HashSet<ActorLifecycleController>();
        private bool combatStateSubscribed;

        public CombatContributionService ContributionService => contributionService;

        private void Awake()
        {
            ResolveSceneReferences();
        }

        private void OnEnable()
        {
            ResolveSceneReferences();
            SubscribeSceneReferences();
        }

        private void OnDisable()
        {
            DetachAll();
        }

        public void Configure(CombatContributionService service, CombatStateService stateService = null)
        {
            if (contributionService != service)
            {
                contributionService = service == null ? contributionService : service;
            }

            if (combatStateService != stateService)
            {
                Detach(combatStateService);
                combatStateService = stateService;
            }

            Attach(combatStateService);
        }

        public void Attach(DamageHealingService service)
        {
            if (service == null || !damageHealingServices.Add(service))
            {
                return;
            }

            service.DamageResolved += OnDamageResolved;
            service.HealingResolved += OnHealingResolved;
        }

        public void Detach(DamageHealingService service)
        {
            if (service == null || !damageHealingServices.Remove(service))
            {
                return;
            }

            service.DamageResolved -= OnDamageResolved;
            service.HealingResolved -= OnHealingResolved;
        }

        public void Attach(AttackResolutionService service)
        {
            if (service == null || !attackResolutionServices.Add(service))
            {
                return;
            }

            service.AttackDamageApplied += OnAttackDamageApplied;
        }

        public void Detach(AttackResolutionService service)
        {
            if (service == null || !attackResolutionServices.Remove(service))
            {
                return;
            }

            service.AttackDamageApplied -= OnAttackDamageApplied;
        }

        public void Attach(DefensiveActionService service)
        {
            if (service == null || !defensiveActionServices.Add(service))
            {
                return;
            }

            service.AttackDodged += OnDefenseResolved;
            service.AttackParried += OnDefenseResolved;
            service.AttackBlockedByDefense += OnDefenseResolved;
        }

        public void Detach(DefensiveActionService service)
        {
            if (service == null || !defensiveActionServices.Remove(service))
            {
                return;
            }

            service.AttackDodged -= OnDefenseResolved;
            service.AttackParried -= OnDefenseResolved;
            service.AttackBlockedByDefense -= OnDefenseResolved;
        }

        public void Attach(OngoingEffectService service)
        {
            if (service == null || !subscribedOngoingEffectServices.Add(service))
            {
                return;
            }

            service.OngoingEffectTickProcessed += OnOngoingEffectTickProcessed;
        }

        public void Detach(OngoingEffectService service)
        {
            if (service == null || !subscribedOngoingEffectServices.Remove(service))
            {
                return;
            }

            service.OngoingEffectTickProcessed -= OnOngoingEffectTickProcessed;
        }

        public void Attach(CombatReactionService service)
        {
            if (service == null || !reactionServices.Add(service))
            {
                return;
            }

            service.ReactionProcessed += OnReactionProcessed;
        }

        public void Detach(CombatReactionService service)
        {
            if (service == null || !reactionServices.Remove(service))
            {
                return;
            }

            service.ReactionProcessed -= OnReactionProcessed;
        }

        public void Attach(CombatStateService service)
        {
            if (service == null || combatStateSubscribed)
            {
                return;
            }

            combatStateService = service;
            combatStateService.EncountersMerged += OnEncountersMerged;
            combatStateService.EncounterSplit += OnEncounterSplit;
            combatStateSubscribed = true;
        }

        public void Detach(CombatStateService service)
        {
            if (service == null || !combatStateSubscribed || !ReferenceEquals(service, combatStateService))
            {
                return;
            }

            service.EncountersMerged -= OnEncountersMerged;
            service.EncounterSplit -= OnEncounterSplit;
            combatStateSubscribed = false;
        }

        public void Attach(ActorLifecycleController controller)
        {
            if (controller == null || !subscribedLifecycleControllers.Add(controller))
            {
                return;
            }

            controller.DefeatProcessed += OnDefeatProcessed;
            controller.ActorDied += OnActorDied;
            controller.ActorRecovered += OnActorRecovered;
            controller.ActorRevived += OnActorRevived;
        }

        public void Detach(ActorLifecycleController controller)
        {
            if (controller == null || !subscribedLifecycleControllers.Remove(controller))
            {
                return;
            }

            controller.DefeatProcessed -= OnDefeatProcessed;
            controller.ActorDied -= OnActorDied;
            controller.ActorRecovered -= OnActorRecovered;
            controller.ActorRevived -= OnActorRevived;
        }

        public void DetachAll()
        {
            foreach (DamageHealingService service in new List<DamageHealingService>(damageHealingServices))
            {
                Detach(service);
            }

            foreach (AttackResolutionService service in new List<AttackResolutionService>(attackResolutionServices))
            {
                Detach(service);
            }

            foreach (DefensiveActionService service in new List<DefensiveActionService>(defensiveActionServices))
            {
                Detach(service);
            }

            foreach (OngoingEffectService service in new List<OngoingEffectService>(subscribedOngoingEffectServices))
            {
                Detach(service);
            }

            foreach (CombatReactionService service in new List<CombatReactionService>(reactionServices))
            {
                Detach(service);
            }

            foreach (ActorLifecycleController controller in new List<ActorLifecycleController>(subscribedLifecycleControllers))
            {
                Detach(controller);
            }

            Detach(combatStateService);
        }

        private void ResolveSceneReferences()
        {
            if (contributionService == null)
            {
                contributionService = GetComponentInParent<CombatContributionService>();
            }

            if (combatStateService == null)
            {
                combatStateService = GetComponentInParent<CombatStateService>();
            }

            if (ongoingEffectServices == null || ongoingEffectServices.Length == 0)
            {
                ongoingEffectServices = GetComponentsInChildren<OngoingEffectService>(includeInactive: true);
            }

            if (lifecycleControllers == null || lifecycleControllers.Length == 0)
            {
                lifecycleControllers = GetComponentsInChildren<ActorLifecycleController>(includeInactive: true);
            }
        }

        private void SubscribeSceneReferences()
        {
            Attach(combatStateService);
            if (ongoingEffectServices != null)
            {
                foreach (OngoingEffectService service in ongoingEffectServices)
                {
                    Attach(service);
                }
            }

            if (lifecycleControllers != null)
            {
                foreach (ActorLifecycleController controller in lifecycleControllers)
                {
                    Attach(controller);
                }
            }
        }

        private void OnDamageResolved(DamageApplicationResult result)
        {
            ContributionServiceOrNull()?.RecordDamage(result, ResolveEncounterId(result == null ? string.Empty : result.ResolvedTargetActorId), CombatContributionSourceKind.Direct);
        }

        private void OnHealingResolved(HealingApplicationResult result)
        {
            ContributionServiceOrNull()?.RecordHealing(result, ResolveEncounterId(result == null ? string.Empty : result.ResolvedTargetActorId), CombatContributionSourceKind.Direct);
        }

        private void OnAttackDamageApplied(AttackResolutionResult result)
        {
            if (result == null || result.DamageResult == null)
            {
                return;
            }

            ContributionServiceOrNull()?.RecordDamage(result.DamageResult, ResolveEncounterId(result.ResolvedTargetActorId), CombatContributionSourceKind.Attack, result.AttackTransactionId, result.DamageTransactionId);
        }

        private void OnDefenseResolved(DefenseResolutionResult result)
        {
            ContributionServiceOrNull()?.RecordDefense(result, ResolveEncounterId(result == null ? string.Empty : result.Request.DefenderActorId));
        }

        private void OnOngoingEffectTickProcessed(OngoingEffectTickResult result)
        {
            if (result == null || !result.Succeeded)
            {
                return;
            }

            if (result.DamageResult != null)
            {
                ContributionServiceOrNull()?.RecordDamage(result.DamageResult, ResolveEncounterId(result.DamageResult.ResolvedTargetActorId), CombatContributionSourceKind.OngoingEffect, result.TickTransactionId, result.TickTransactionId);
            }

            if (result.HealingResult != null)
            {
                ContributionServiceOrNull()?.RecordHealing(result.HealingResult, ResolveEncounterId(result.HealingResult.ResolvedTargetActorId), CombatContributionSourceKind.OngoingEffect, result.TickTransactionId, result.TickTransactionId);
            }
        }

        private void OnReactionProcessed(CombatReactionExecutionResult result)
        {
            string actorId = result == null || result.Source == null ? string.Empty : result.Source.OwnerActorId;
            ContributionServiceOrNull()?.RecordReaction(result, ResolveEncounterId(actorId));
        }

        private void OnDefeatProcessed(ActorLifecycleResult result)
        {
            ContributionServiceOrNull()?.ResolveDefeatCredit(result, ResolveEncounterId(result == null ? string.Empty : result.TargetActorId));
        }

        private void OnActorDied(ActorLifecycleResult result)
        {
            ContributionServiceOrNull()?.ResolveKillCredit(result, ResolveEncounterId(result == null ? string.Empty : result.TargetActorId));
        }

        private void OnActorRecovered(ActorLifecycleResult result)
        {
            RecordLifecycleSupport(result, CombatContributionType.RecoveryProvided);
        }

        private void OnActorRevived(ActorLifecycleResult result)
        {
            RecordLifecycleSupport(result, CombatContributionType.RevivalProvided);
        }

        private void OnEncountersMerged(CombatEncounterSnapshot snapshot)
        {
            ContributionServiceOrNull()?.MergeEncounterLedgers(snapshot);
        }

        private void OnEncounterSplit(CombatEncounterSplitResult result)
        {
            ContributionServiceOrNull()?.PartitionEncounterLedgers(result);
        }

        private void RecordLifecycleSupport(ActorLifecycleResult result, CombatContributionType type)
        {
            if (result == null || result.Preview || result.Duplicate || !result.Succeeded || string.IsNullOrWhiteSpace(result.SourceActorId))
            {
                return;
            }

            float actual = Mathf.Max(result.AppliedHealthRestore, 1f);
            ContributionServiceOrNull()?.RecordContribution(new CombatContributionRecordRequest(
                result.TransactionId,
                type,
                result.SourceActorId,
                string.Empty,
                result.TargetActorId,
                string.Empty,
                ResolveEncounterId(result.TargetActorId),
                result.RequestedHealthRestore,
                actual,
                0f,
                contributionService == null ? 0f : contributionService.SimulationTime,
                CombatContributionSourceKind.Lifecycle,
                result.TransactionId,
                string.Empty,
                result.PolicyId,
                string.Empty,
                preview: false,
                authorityValidated: true));
        }

        private string ResolveEncounterId(string actorId)
        {
            if (combatStateService == null || string.IsNullOrWhiteSpace(actorId))
            {
                return string.Empty;
            }

            CombatEncounterSnapshot encounter = combatStateService.GetEncounterForActor(actorId);
            return encounter == null ? string.Empty : encounter.EncounterId;
        }

        private CombatContributionService ContributionServiceOrNull()
        {
            if (contributionService == null)
            {
                contributionService = GetComponentInParent<CombatContributionService>();
            }

            return contributionService;
        }
    }
}
