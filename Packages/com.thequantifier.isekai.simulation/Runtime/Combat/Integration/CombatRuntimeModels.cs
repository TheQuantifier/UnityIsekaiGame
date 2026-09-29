using System;
using System.Collections.Generic;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Contributions;
using UnityIsekaiGame.Combat.Defense;
using UnityIsekaiGame.Combat.Execution;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.Integration
{
    public enum CombatRuntimeReadinessState
    {
        Uninitialized,
        ResolvingDependencies,
        Ready,
        Restoring,
        Invalid,
        Disposed
    }

    public enum CombatIntegritySeverity
    {
        Info,
        Warning,
        Error
    }

    public enum CombatSubsystem
    {
        Facade,
        Character,
        Resources,
        Lifecycle,
        DamageHealing,
        AttackResolution,
        Defense,
        Execution,
        OngoingEffects,
        CombatState,
        Reactions,
        Contributions,
        Persistence,
        Transactions,
        Organization
    }

    public sealed class CombatRuntimeDiagnostic
    {
        public CombatRuntimeDiagnostic(CombatIntegritySeverity severity, CombatSubsystem subsystem, string code, string message, string actorId = "", string definitionId = "", string path = "")
        {
            Severity = severity;
            Subsystem = subsystem;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            ActorId = actorId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            Path = path ?? string.Empty;
        }

        public CombatIntegritySeverity Severity { get; }
        public CombatSubsystem Subsystem { get; }
        public string Code { get; }
        public string Message { get; }
        public string ActorId { get; }
        public string DefinitionId { get; }
        public string Path { get; }
    }

    public sealed class CombatReadinessResult
    {
        public CombatReadinessResult(CombatRuntimeReadinessState state, IReadOnlyList<CombatRuntimeDiagnostic> diagnostics)
        {
            State = state;
            Diagnostics = diagnostics == null ? Array.Empty<CombatRuntimeDiagnostic>() : new List<CombatRuntimeDiagnostic>(diagnostics);
        }

        public CombatRuntimeReadinessState State { get; }
        public IReadOnlyList<CombatRuntimeDiagnostic> Diagnostics { get; }
        public bool IsReady => State == CombatRuntimeReadinessState.Ready;
    }

    public sealed class CombatSubsystemRevisionSnapshot
    {
        public CombatSubsystemRevisionSnapshot(
            long characterRevision,
            long lifecycleRevision,
            long contributionRevision,
            int activeOngoingEffects,
            int activeEngagements,
            int activeReactionSources,
            bool hasActiveDefense,
            bool hasActiveExecution,
            long aggregateRevision)
        {
            CharacterRevision = characterRevision;
            LifecycleRevision = lifecycleRevision;
            ContributionRevision = contributionRevision;
            ActiveOngoingEffects = Math.Max(0, activeOngoingEffects);
            ActiveEngagements = Math.Max(0, activeEngagements);
            ActiveReactionSources = Math.Max(0, activeReactionSources);
            HasActiveDefense = hasActiveDefense;
            HasActiveExecution = hasActiveExecution;
            AggregateRevision = aggregateRevision;
        }

        public long CharacterRevision { get; }
        public long LifecycleRevision { get; }
        public long ContributionRevision { get; }
        public int ActiveOngoingEffects { get; }
        public int ActiveEngagements { get; }
        public int ActiveReactionSources { get; }
        public bool HasActiveDefense { get; }
        public bool HasActiveExecution { get; }
        public long AggregateRevision { get; }
    }

    public sealed class CombatStatValueSnapshot
    {
        public CombatStatValueSnapshot(string statId, float value)
        {
            StatId = statId ?? string.Empty;
            Value = value;
        }

        public string StatId { get; }
        public float Value { get; }
    }

    public sealed class CombatTransactionTraceSnapshot
    {
        public CombatTransactionTraceSnapshot(
            string rootTransactionId,
            string executionTransactionId,
            string attackTransactionId,
            string defenseTransactionId,
            string damageTransactionId,
            string reactionTransactionId,
            string contributionTransactionId,
            IReadOnlyList<CombatRuntimeDiagnostic> diagnostics)
        {
            RootTransactionId = rootTransactionId ?? string.Empty;
            ExecutionTransactionId = executionTransactionId ?? string.Empty;
            AttackTransactionId = attackTransactionId ?? string.Empty;
            DefenseTransactionId = defenseTransactionId ?? string.Empty;
            DamageTransactionId = damageTransactionId ?? string.Empty;
            ReactionTransactionId = reactionTransactionId ?? string.Empty;
            ContributionTransactionId = contributionTransactionId ?? string.Empty;
            Diagnostics = diagnostics == null ? Array.Empty<CombatRuntimeDiagnostic>() : new List<CombatRuntimeDiagnostic>(diagnostics);
        }

        public string RootTransactionId { get; }
        public string ExecutionTransactionId { get; }
        public string AttackTransactionId { get; }
        public string DefenseTransactionId { get; }
        public string DamageTransactionId { get; }
        public string ReactionTransactionId { get; }
        public string ContributionTransactionId { get; }
        public IReadOnlyList<CombatRuntimeDiagnostic> Diagnostics { get; }
        public bool IsCoherent => Diagnostics.Count == 0;
    }

    public sealed class CombatOngoingEffectSnapshot
    {
        public CombatOngoingEffectSnapshot(string instanceId, string definitionId, string sourceActorId, string targetActorId, int stackCount, float remainingDuration, long revision)
        {
            InstanceId = instanceId ?? string.Empty;
            DefinitionId = definitionId ?? string.Empty;
            SourceActorId = sourceActorId ?? string.Empty;
            TargetActorId = targetActorId ?? string.Empty;
            StackCount = Math.Max(0, stackCount);
            RemainingDuration = Math.Max(0f, remainingDuration);
            Revision = revision;
        }

        public string InstanceId { get; }
        public string DefinitionId { get; }
        public string SourceActorId { get; }
        public string TargetActorId { get; }
        public int StackCount { get; }
        public float RemainingDuration { get; }
        public long Revision { get; }
    }

    public sealed class CombatReactionSourceSnapshot
    {
        public CombatReactionSourceSnapshot(string registrationId, string ownerActorId, string sourceKind, string sourceStableId, string sourceInstanceId, int sourcePriority, string definitionId, bool active)
        {
            RegistrationId = registrationId ?? string.Empty;
            OwnerActorId = ownerActorId ?? string.Empty;
            SourceKind = sourceKind ?? string.Empty;
            SourceStableId = sourceStableId ?? string.Empty;
            SourceInstanceId = sourceInstanceId ?? string.Empty;
            SourcePriority = sourcePriority;
            DefinitionId = definitionId ?? string.Empty;
            Active = active;
        }

        public string RegistrationId { get; }
        public string OwnerActorId { get; }
        public string SourceKind { get; }
        public string SourceStableId { get; }
        public string SourceInstanceId { get; }
        public int SourcePriority { get; }
        public string DefinitionId { get; }
        public bool Active { get; }
    }

    public sealed class CombatRuntimeSnapshot
    {
        public CombatRuntimeSnapshot(
            string actorId,
            string bodyId,
            string personId,
            CombatReadinessResult readiness,
            ActorLifecycleState lifecycleState,
            IReadOnlyList<ResourceSnapshot> resources,
            IReadOnlyList<CombatStatValueSnapshot> combatStats,
            DefensiveActionStateSnapshot activeDefense,
            CombatExecutionStateSnapshot activeExecution,
            IReadOnlyList<CombatOngoingEffectSnapshot> activeOngoingEffects,
            ActorCombatStateSnapshot combatState,
            IReadOnlyList<CombatEngagementSnapshot> activeEngagements,
            IReadOnlyList<string> recentOpponents,
            IReadOnlyList<CombatReactionSourceSnapshot> reactionSources,
            IReadOnlyList<CombatContributionLedgerSnapshot> contributionLedgers,
            CombatSubsystemRevisionSnapshot revisions,
            CombatTransactionTraceSnapshot lastTransactionTrace,
            IReadOnlyList<CombatRuntimeDiagnostic> diagnostics)
        {
            ActorId = actorId ?? string.Empty;
            BodyId = bodyId ?? string.Empty;
            PersonId = personId ?? string.Empty;
            Readiness = readiness ?? new CombatReadinessResult(CombatRuntimeReadinessState.Uninitialized, null);
            LifecycleState = lifecycleState;
            Resources = resources == null ? Array.Empty<ResourceSnapshot>() : new List<ResourceSnapshot>(resources);
            CombatStats = combatStats == null ? Array.Empty<CombatStatValueSnapshot>() : new List<CombatStatValueSnapshot>(combatStats);
            ActiveDefense = activeDefense;
            ActiveExecution = activeExecution;
            ActiveOngoingEffects = activeOngoingEffects == null ? Array.Empty<CombatOngoingEffectSnapshot>() : new List<CombatOngoingEffectSnapshot>(activeOngoingEffects);
            CombatState = combatState;
            ActiveEngagements = activeEngagements == null ? Array.Empty<CombatEngagementSnapshot>() : new List<CombatEngagementSnapshot>(activeEngagements);
            RecentOpponents = recentOpponents == null ? Array.Empty<string>() : new List<string>(recentOpponents);
            ReactionSources = reactionSources == null ? Array.Empty<CombatReactionSourceSnapshot>() : new List<CombatReactionSourceSnapshot>(reactionSources);
            ContributionLedgers = contributionLedgers == null ? Array.Empty<CombatContributionLedgerSnapshot>() : new List<CombatContributionLedgerSnapshot>(contributionLedgers);
            Revisions = revisions;
            LastTransactionTrace = lastTransactionTrace;
            Diagnostics = diagnostics == null ? Array.Empty<CombatRuntimeDiagnostic>() : new List<CombatRuntimeDiagnostic>(diagnostics);
        }

        public string ActorId { get; }
        public string BodyId { get; }
        public string PersonId { get; }
        public CombatReadinessResult Readiness { get; }
        public ActorLifecycleState LifecycleState { get; }
        public IReadOnlyList<ResourceSnapshot> Resources { get; }
        public IReadOnlyList<CombatStatValueSnapshot> CombatStats { get; }
        public DefensiveActionStateSnapshot ActiveDefense { get; }
        public CombatExecutionStateSnapshot ActiveExecution { get; }
        public IReadOnlyList<CombatOngoingEffectSnapshot> ActiveOngoingEffects { get; }
        public ActorCombatStateSnapshot CombatState { get; }
        public IReadOnlyList<CombatEngagementSnapshot> ActiveEngagements { get; }
        public IReadOnlyList<string> RecentOpponents { get; }
        public IReadOnlyList<CombatReactionSourceSnapshot> ReactionSources { get; }
        public IReadOnlyList<CombatContributionLedgerSnapshot> ContributionLedgers { get; }
        public CombatSubsystemRevisionSnapshot Revisions { get; }
        public CombatTransactionTraceSnapshot LastTransactionTrace { get; }
        public IReadOnlyList<CombatRuntimeDiagnostic> Diagnostics { get; }
    }

    public sealed class CombatIntegrityReport
    {
        public CombatIntegrityReport(IReadOnlyList<CombatRuntimeDiagnostic> diagnostics)
        {
            Diagnostics = diagnostics == null ? Array.Empty<CombatRuntimeDiagnostic>() : new List<CombatRuntimeDiagnostic>(diagnostics);
        }

        public IReadOnlyList<CombatRuntimeDiagnostic> Diagnostics { get; }
        public bool Passed
        {
            get
            {
                foreach (CombatRuntimeDiagnostic diagnostic in Diagnostics)
                {
                    if (diagnostic.Severity == CombatIntegritySeverity.Error)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
