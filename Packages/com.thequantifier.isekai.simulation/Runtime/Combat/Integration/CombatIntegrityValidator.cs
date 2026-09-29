using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityIsekaiGame.ActorLifecycle;
using UnityIsekaiGame.CharacterSystem;
using UnityIsekaiGame.Combat.CombatState;
using UnityIsekaiGame.Combat.Contributions;
using UnityIsekaiGame.ResourceSystem;

namespace UnityIsekaiGame.Combat.Integration
{
    public static class CombatIntegrityValidator
    {
        public static CombatIntegrityReport Validate(CombatRuntimeFacade facade, GameObject actorObject)
        {
            List<CombatRuntimeDiagnostic> diagnostics = new List<CombatRuntimeDiagnostic>();
            if (facade == null)
            {
                diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Error, CombatSubsystem.Facade, "MissingFacade", "Combat runtime facade is missing."));
                return new CombatIntegrityReport(diagnostics);
            }

            CharacterSystemCoordinator character = actorObject == null ? null : actorObject.GetComponentInParent<CharacterSystemCoordinator>();
            string actorId = CombatRuntimeFacade.ResolveActorId(actorObject, character);
            CharacterResourceCollection resources = CombatRuntimeFacade.ResolveResources(actorObject, character);
            ActorLifecycleController lifecycle = actorObject == null ? null : actorObject.GetComponentInParent<ActorLifecycleController>();

            ValidateLifecycleHealth(actorId, resources, lifecycle, diagnostics);
            ValidateTransientOwners(actorId, facade, diagnostics);
            ValidateContributions(facade, diagnostics);
            ValidateTransactions(facade.LastTransactionTrace, diagnostics);
            ValidateServiceComposition(facade, diagnostics);
            return new CombatIntegrityReport(diagnostics);
        }

        private static void ValidateLifecycleHealth(string actorId, CharacterResourceCollection resources, ActorLifecycleController lifecycle, List<CombatRuntimeDiagnostic> diagnostics)
        {
            if (resources == null || lifecycle == null || !resources.TryGetResource(ResourceIds.Health, out ResourceSnapshot health))
            {
                return;
            }

            bool empty = health.Current <= health.Minimum + CharacterResourceCollection.Epsilon;
            if (lifecycle.State == ActorLifecycleState.Active && empty)
            {
                diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Error, CombatSubsystem.Lifecycle, "ActiveWithZeroHealth", "Active actor has empty Health.", actorId));
            }

            if (lifecycle.State == ActorLifecycleState.Dead && health.Current > health.Minimum + CharacterResourceCollection.Epsilon)
            {
                diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Error, CombatSubsystem.Lifecycle, "DeadWithPositiveHealth", "Dead actor has positive Health.", actorId));
            }
        }

        private static void ValidateTransientOwners(string actorId, CombatRuntimeFacade facade, List<CombatRuntimeDiagnostic> diagnostics)
        {
            if (string.IsNullOrWhiteSpace(actorId))
            {
                return;
            }

            if (facade.Defense != null && facade.Defense.TryGetActiveDefense(actorId, out var defense) && defense != null && defense.DefenderActorId != actorId)
            {
                diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Error, CombatSubsystem.Defense, "DefenseOwnerMismatch", "Active defense owner does not match actor.", actorId));
            }

            CombatStateService combatState = facade.CombatState;
            ActorCombatStateSnapshot state = combatState == null ? null : combatState.GetCombatState(actorId);
            if (state != null && state.IsInCombat)
            {
                IReadOnlyList<CombatEngagementSnapshot> engagements = combatState.GetActiveEngagements(actorId);
                if (state.ActiveEngagementCount > 0 && engagements.Count == 0)
                {
                    diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Warning, CombatSubsystem.CombatState, "MissingActiveEngagementSnapshot", "Actor reports active engagements but none were returned.", actorId));
                }
            }
        }

        private static void ValidateContributions(CombatRuntimeFacade facade, List<CombatRuntimeDiagnostic> diagnostics)
        {
            if (facade.Contributions == null)
            {
                return;
            }

            foreach (CombatContributionLedgerSnapshot ledger in facade.Contributions.GetLedgerSnapshots())
            {
                if (ledger == null)
                {
                    diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Error, CombatSubsystem.Contributions, "NullLedger", "Contribution ledger snapshot is null."));
                    continue;
                }

                foreach (CombatContributionRecord record in ledger.Records)
                {
                    if (record == null)
                    {
                        diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Error, CombatSubsystem.Contributions, "NullContributionRecord", $"Contribution ledger '{ledger.LedgerId}' contains a null record."));
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(record.SourceTransactionId))
                    {
                        diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Warning, CombatSubsystem.Contributions, "MissingContributionTransaction", $"Contribution record '{record.RecordId}' has no source transaction.", record.TargetActorId));
                    }
                }
            }
        }

        private static void ValidateTransactions(CombatTransactionTraceSnapshot trace, List<CombatRuntimeDiagnostic> diagnostics)
        {
            if (trace == null || string.IsNullOrWhiteSpace(trace.RootTransactionId))
            {
                return;
            }

            foreach (CombatRuntimeDiagnostic diagnostic in trace.Diagnostics)
            {
                diagnostics.Add(diagnostic);
            }
        }

        private static void ValidateServiceComposition(CombatRuntimeFacade facade, List<CombatRuntimeDiagnostic> diagnostics)
        {
            bool missingRequired = facade.DamageHealing == null
                || facade.AttackResolution == null
                || facade.Defense == null
                || facade.Execution == null
                || facade.CombatState == null
                || facade.OngoingEffects == null
                || facade.Reactions == null
                || facade.Contributions == null;
            if (missingRequired)
            {
                diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Error, CombatSubsystem.Facade, "IncompleteServiceComposition", "One or more Step 6 combat services are missing."));
            }

            if (facade.Reactions != null && facade.OngoingEffects == null && facade.Reactions.Registrations.Any())
            {
                diagnostics.Add(new CombatRuntimeDiagnostic(CombatIntegritySeverity.Warning, CombatSubsystem.Reactions, "ReactionOngoingEffectsMissing", "Reaction sources exist but no OngoingEffectService is configured."));
            }
        }
    }
}
